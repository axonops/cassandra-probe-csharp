using System.Net;
using Cassandra;
using CassandraProbe.Core.Configuration;
using CassandraProbe.Core.Interfaces;
using CassandraProbe.Core.Models;
using Microsoft.Extensions.Logging;

namespace CassandraProbe.Services;

public class ClusterDiscoveryService : IClusterDiscovery
{
    private readonly ISessionManager _sessionManager;
    private readonly ILogger<ClusterDiscoveryService> _logger;
    private ClusterTopology? _topology;

    public ClusterDiscoveryService(
        ISessionManager sessionManager,
        ILogger<ClusterDiscoveryService> logger)
    {
        _sessionManager = sessionManager;
        _logger = logger;
    }

    public async Task<ClusterTopology> DiscoverAsync(ProbeConfiguration config)
    {
        _logger.LogInformation("Starting cluster discovery...");

        var session = await _sessionManager.GetSessionAsync();
        var cluster = _sessionManager.GetCluster();
        
        if (cluster == null)
            throw new InvalidOperationException("Cluster not initialized");

        var topology = new ClusterTopology
        {
            ClusterName = cluster.Metadata.ClusterName
        };

        // Query local node information
        var localNode = await QueryLocalNode(session);
        if (localNode != null)
        {
            topology.Hosts.Add(localNode);
            AddToDatacenterMap(topology, localNode);
        }

        // Query peer nodes
        var peers = await QueryPeerNodes(session);
        foreach (var peer in peers)
        {
            topology.Hosts.Add(peer);
            AddToDatacenterMap(topology, peer);
        }

        _logger.LogInformation("Discovered {Count} nodes in cluster '{ClusterName}'", 
            topology.TotalHosts, topology.ClusterName);
        _logger.LogInformation("Nodes by status - Up: {Up}, Down: {Down}", 
            topology.UpHosts, topology.DownHosts);

        _topology = topology;
        return topology;
    }

    public async Task<IEnumerable<HostProbe>> GetHostsAsync()
    {
        if (_topology == null)
            throw new InvalidOperationException("Cluster discovery has not been performed yet");

        return await Task.FromResult(_topology.Hosts);
    }

    private async Task<HostProbe?> QueryLocalNode(ISession session)
    {
        try
        {
            var query = "SELECT host_id, data_center, rack, release_version, rpc_address, listen_address FROM system.local";
            var result = await session.ExecuteAsync(new SimpleStatement(query));
            var row = result.FirstOrDefault();

            if (row == null)
                return null;

            var rpcAddress = row.GetValue<IPAddress?>("rpc_address");
            var listenAddress = row.GetValue<IPAddress?>("listen_address");
            var address = rpcAddress ?? listenAddress;

            if (address == null)
                return null;

            var cluster = _sessionManager.GetCluster();
            var host = cluster?.AllHosts().FirstOrDefault(h => h.Address.Address.Equals(address));

            return new HostProbe
            {
                HostId = row.GetValue<Guid>("host_id").ToString(),
                Address = address,
                Datacenter = row.GetValue<string>("data_center") ?? "Unknown",
                Rack = row.GetValue<string>("rack") ?? "Unknown",
                CassandraVersion = row.GetValue<string>("release_version") ?? "Unknown",
                Status = host?.IsUp ?? false ? HostStatus.Up : HostStatus.Down,
                LastSeen = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to query local node information");
            return null;
        }
    }

    private async Task<List<HostProbe>> QueryPeerNodes(ISession session)
    {
        var peers = new List<HostProbe>();

        try
        {
            var query = "SELECT peer, host_id, data_center, rack, release_version, rpc_address FROM system.peers";
            var result = await session.ExecuteAsync(new SimpleStatement(query));
            var cluster = _sessionManager.GetCluster();

            foreach (var row in result)
            {
                var peer = row.GetValue<IPAddress?>("peer");
                var rpcAddress = row.GetValue<IPAddress?>("rpc_address");
                var address = rpcAddress ?? peer;

                if (address == null)
                    continue;

                var host = cluster?.AllHosts().FirstOrDefault(h => h.Address.Address.Equals(address));

                var hostProbe = new HostProbe
                {
                    HostId = row.GetValue<Guid?>("host_id")?.ToString(),
                    Address = address,
                    Datacenter = row.GetValue<string>("data_center") ?? "Unknown",
                    Rack = row.GetValue<string>("rack") ?? "Unknown", 
                    CassandraVersion = row.GetValue<string>("release_version") ?? "Unknown",
                    Status = host?.IsUp ?? false ? HostStatus.Up : HostStatus.Down,
                    LastSeen = DateTime.UtcNow
                };

                peers.Add(hostProbe);
            }

            _logger.LogInformation("Discovered {Count} peer nodes", peers.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to query peer nodes");
        }

        return peers;
    }

    private void AddToDatacenterMap(ClusterTopology topology, HostProbe host)
    {
        if (!topology.DatacenterHosts.ContainsKey(host.Datacenter))
        {
            topology.DatacenterHosts[host.Datacenter] = new List<HostProbe>();
        }
        
        topology.DatacenterHosts[host.Datacenter].Add(host);
    }
}