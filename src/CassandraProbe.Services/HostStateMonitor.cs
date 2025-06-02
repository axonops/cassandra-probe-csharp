using System.Collections.Concurrent;
using System.Net;
using Cassandra;
using CassandraProbe.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace CassandraProbe.Services;

public class HostStateMonitor
{
    private readonly ISessionManager _sessionManager;
    private readonly IConnectionMonitor _connectionMonitor;
    private readonly ILogger<HostStateMonitor> _logger;
    private readonly TimeSpan _pollingInterval;
    private readonly ConcurrentDictionary<IPEndPoint, bool> _hostStates = new();
    private MetadataMonitor? _metadataMonitor;

    public HostStateMonitor(
        ISessionManager sessionManager,
        IConnectionMonitor connectionMonitor,
        ILogger<HostStateMonitor> logger)
    {
        _sessionManager = sessionManager;
        _connectionMonitor = connectionMonitor;
        _logger = logger;
        _pollingInterval = TimeSpan.FromSeconds(10); // Poll every 10 seconds
    }

    public void SetMetadataMonitor(MetadataMonitor monitor)
    {
        _metadataMonitor = monitor;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = Task.Run(async () => await MonitorHostStatesAsync(cancellationToken), cancellationToken);
        return Task.CompletedTask;
    }

    private async Task MonitorHostStatesAsync(CancellationToken cancellationToken)
    {
        // Wait for cluster to be initialized
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var cluster = _sessionManager.GetCluster();
                if (cluster != null)
                {
                    // Initialize host states
                    foreach (var host in cluster.AllHosts())
                    {
                        var endpoint = new IPEndPoint(host.Address.Address, host.Address.Port);
                        _hostStates[endpoint] = host.IsUp;
                    }
                    _logger.LogInformation("Host state monitoring initialized with {Count} hosts", _hostStates.Count);
                    break;
                }
                await Task.Delay(1000, cancellationToken);
            }
            catch (TaskCanceledException)
            {
                // Expected when cancellation is requested during shutdown
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing host state monitor");
                await Task.Delay(5000, cancellationToken);
            }
        }

        // Monitor host state changes
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_pollingInterval, cancellationToken);
                
                var cluster = _sessionManager.GetCluster();
                if (cluster != null)
                {
                    CheckForHostStateChanges(cluster);
                }
            }
            catch (TaskCanceledException)
            {
                // Expected when cancellation is requested during shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during host state monitoring");
            }
        }
    }

    private void CheckForHostStateChanges(ICluster cluster)
    {
        try
        {
            var currentHosts = cluster.AllHosts().ToList();
            
            foreach (var host in currentHosts)
            {
                var endpoint = new IPEndPoint(host.Address.Address, host.Address.Port);
                var currentState = host.IsUp;
                
                if (_hostStates.TryGetValue(endpoint, out var previousState))
                {
                    if (currentState != previousState)
                    {
                        // State changed
                        _hostStates[endpoint] = currentState;
                        
                        if (currentState)
                        {
                            _logger.LogInformation("[CLUSTER EVENT] Node UP detected: {Address} DC={Datacenter}",
                                host.Address, host.Datacenter);
                            _connectionMonitor.RecordHostUp(host);
                            _metadataMonitor?.LogClusterMetadataAfterEvent("HostUp");
                        }
                        else
                        {
                            _logger.LogWarning("[CLUSTER EVENT] Node DOWN detected: {Address} DC={Datacenter}",
                                host.Address, host.Datacenter);
                            _connectionMonitor.RecordHostDown(host);
                            _metadataMonitor?.LogClusterMetadataAfterEvent("HostDown");
                        }
                    }
                }
                else
                {
                    // New host not in our tracking (shouldn't happen if HostAdded events work properly)
                    _hostStates[endpoint] = currentState;
                    _logger.LogInformation("[CLUSTER EVENT] New host detected during polling: {Address} State={State}",
                        host.Address, currentState ? "UP" : "DOWN");
                }
            }
            
            // Check for removed hosts
            var currentEndpoints = currentHosts.Select(h => new IPEndPoint(h.Address.Address, h.Address.Port)).ToHashSet();
            var removedEndpoints = _hostStates.Keys.Where(ep => !currentEndpoints.Contains(ep)).ToList();
            
            foreach (var endpoint in removedEndpoints)
            {
                _hostStates.TryRemove(endpoint, out _);
                _logger.LogInformation("[CLUSTER EVENT] Host no longer in cluster during polling: {Address}", endpoint);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking host state changes");
        }
    }
}