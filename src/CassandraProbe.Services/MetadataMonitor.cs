using System.Collections.Concurrent;
using Cassandra;
using CassandraProbe.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace CassandraProbe.Services;

public class MetadataMonitor
{
    private readonly ISessionManager _sessionManager;
    private readonly ILogger<MetadataMonitor> _logger;
    private readonly TimeSpan _pollingInterval;
    private readonly ConcurrentDictionary<string, int> _keyspaceTableCounts = new();
    private int _lastHostCount;
    private string? _lastClusterName;

    public MetadataMonitor(
        ISessionManager sessionManager,
        ILogger<MetadataMonitor> logger)
    {
        _sessionManager = sessionManager;
        _logger = logger;
        _pollingInterval = TimeSpan.FromMinutes(1); // Default to 1 minute
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = Task.Run(async () => await MonitorMetadataAsync(cancellationToken), cancellationToken);
        return Task.CompletedTask;
    }

    private async Task MonitorMetadataAsync(CancellationToken cancellationToken)
    {
        // Wait for cluster to be initialized
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var cluster = _sessionManager.GetCluster();
                if (cluster != null)
                {
                    // Log initial metadata
                    LogClusterMetadata(cluster, "Initial cluster metadata");
                    break;
                }
                await Task.Delay(1000, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting initial cluster metadata");
                await Task.Delay(5000, cancellationToken);
            }
        }

        // Periodically log metadata
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_pollingInterval, cancellationToken);
                
                var cluster = _sessionManager.GetCluster();
                if (cluster != null)
                {
                    LogClusterMetadata(cluster, "Periodic metadata update");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during periodic metadata logging");
            }
        }
    }

    public void LogClusterMetadataAfterEvent(string eventType)
    {
        try
        {
            var cluster = _sessionManager.GetCluster();
            if (cluster != null)
            {
                LogClusterMetadata(cluster, $"Metadata after {eventType} event");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging metadata after {EventType} event", eventType);
        }
    }

    private void LogClusterMetadata(ICluster cluster, string context)
    {
        try
        {
            var metadata = cluster.Metadata;
            var hosts = cluster.AllHosts().ToList();
            var keyspaces = metadata.GetKeyspaces();

            // Basic cluster info
            _logger.LogInformation("[CLUSTER METADATA] {Context}: Cluster={ClusterName}, Hosts={HostCount}, Keyspaces={KeyspaceCount}",
                context, metadata.ClusterName, hosts.Count, keyspaces.Count);

            // Host details if count changed
            if (hosts.Count != _lastHostCount)
            {
                _lastHostCount = hosts.Count;
                foreach (var host in hosts.OrderBy(h => h.Datacenter).ThenBy(h => h.Address.ToString()))
                {
                    _logger.LogInformation("[CLUSTER METADATA] Host: {Address} DC={Datacenter} Rack={Rack} State={State} Version={Version}",
                        host.Address, host.Datacenter, host.Rack, 
                        host.IsUp ? "UP" : "DOWN", host.CassandraVersion);
                }
            }

            // Detect schema changes by tracking table counts per keyspace
            var schemaChanged = false;
            foreach (var keyspace in keyspaces.Where(k => !k.StartsWith("system", StringComparison.OrdinalIgnoreCase)))
            {
                var tables = metadata.GetTables(keyspace);
                var tableCount = tables?.Count() ?? 0;
                
                if (_keyspaceTableCounts.TryGetValue(keyspace, out var previousCount))
                {
                    if (tableCount != previousCount)
                    {
                        schemaChanged = true;
                        _logger.LogInformation("[CLUSTER METADATA] Schema change detected in keyspace {Keyspace}: Tables {OldCount} -> {NewCount}",
                            keyspace, previousCount, tableCount);
                    }
                }
                _keyspaceTableCounts[keyspace] = tableCount;
            }

            if (schemaChanged)
            {
                LogDetailedSchema(metadata);
            }

            // Cluster name change detection
            if (_lastClusterName != null && _lastClusterName != metadata.ClusterName)
            {
                _logger.LogWarning("[CLUSTER METADATA] Cluster name changed from {OldName} to {NewName}",
                    _lastClusterName, metadata.ClusterName);
            }
            _lastClusterName = metadata.ClusterName;

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving cluster metadata");
        }
    }

    private void LogDetailedSchema(Metadata metadata)
    {
        try
        {
            _logger.LogInformation("[CLUSTER METADATA] Detailed schema information:");
            
            foreach (var keyspace in metadata.GetKeyspaces().Where(k => !k.StartsWith("system", StringComparison.OrdinalIgnoreCase)))
            {
                var keyspaceDef = metadata.GetKeyspace(keyspace);
                if (keyspaceDef != null)
                {
                    _logger.LogInformation("[CLUSTER METADATA] Keyspace {Name}: Replication={ReplicationClass}, DurableWrites={DurableWrites}",
                        keyspace, keyspaceDef.StrategyClass, keyspaceDef.DurableWrites);
                    
                    var tables = metadata.GetTables(keyspace);
                    if (tables != null)
                    {
                        foreach (var table in tables)
                        {
                            var tableDef = metadata.GetTable(keyspace, table);
                            if (tableDef != null)
                            {
                                _logger.LogInformation("[CLUSTER METADATA]   Table {Keyspace}.{Table}: Columns={ColumnCount}",
                                    keyspace, table, tableDef.TableColumns.Length);
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging detailed schema");
        }
    }
}