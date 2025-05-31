using System.Collections.Concurrent;
using System.Net;
using Cassandra;
using CassandraProbe.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace CassandraProbe.Services;

public class ConnectionMonitor : IConnectionMonitor
{
    private readonly ILogger<ConnectionMonitor> _logger;
    private readonly ConcurrentDictionary<IPEndPoint, ConnectionState> _hostStates = new();
    private readonly ConcurrentDictionary<IPEndPoint, ReconnectionInfo> _reconnectionInfo = new();
    private readonly List<ReconnectionEvent> _reconnectionHistory = new();
    private readonly object _historyLock = new();
    private ICluster? _cluster;

    public event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;

    public ConnectionMonitor(ILogger<ConnectionMonitor> logger)
    {
        _logger = logger;
    }

    public void RegisterCluster(ICluster cluster)
    {
        _cluster = cluster;
        
        // Subscribe to cluster events
        cluster.HostAdded += OnHostAdded;
        cluster.HostRemoved += OnHostRemoved;

        // Initialize host states
        foreach (var host in cluster.AllHosts())
        {
            var endpoint = new IPEndPoint(host.Address.Address, host.Address.Port);
            _hostStates[endpoint] = host.IsUp ? ConnectionState.Connected : ConnectionState.Disconnected;
        }

        _logger.LogInformation("Connection monitor registered with cluster, tracking {Count} hosts", 
            _hostStates.Count);
    }

    public ConnectionPoolStatus GetPoolStatus()
    {
        var status = new ConnectionPoolStatus
        {
            TotalConnections = _hostStates.Count,
            ActiveConnections = _hostStates.Count(kvp => kvp.Value == ConnectionState.Connected),
            FailedHosts = _hostStates.Count(kvp => kvp.Value == ConnectionState.Disconnected),
            ReconnectingHosts = new Dictionary<IPEndPoint, ReconnectionInfo>(_reconnectionInfo)
        };

        return status;
    }

    public IEnumerable<ReconnectionEvent> GetReconnectionHistory()
    {
        lock (_historyLock)
        {
            return new List<ReconnectionEvent>(_reconnectionHistory);
        }
    }

    private void OnHostAdded(Host host)
    {
        var endpoint = new IPEndPoint(host.Address.Address, host.Address.Port);
        _hostStates[endpoint] = host.IsUp ? ConnectionState.Connected : ConnectionState.Disconnected;
    }

    private void OnHostRemoved(Host host)
    {
        var endpoint = new IPEndPoint(host.Address.Address, host.Address.Port);
        _hostStates.TryRemove(endpoint, out _);
        _reconnectionInfo.TryRemove(endpoint, out _);
    }




    private void RecordReconnectionEvent(ReconnectionEvent evt)
    {
        lock (_historyLock)
        {
            _reconnectionHistory.Add(evt);
            
            // Keep only last 1000 events
            if (_reconnectionHistory.Count > 1000)
            {
                _reconnectionHistory.RemoveRange(0, _reconnectionHistory.Count - 1000);
            }
        }
    }
}