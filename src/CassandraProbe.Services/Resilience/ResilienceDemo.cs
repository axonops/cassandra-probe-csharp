using System.Diagnostics;
using Cassandra;
using CassandraProbe.Core.Configuration;
using CassandraProbe.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace CassandraProbe.Services.Resilience;

/// <summary>
/// Demonstrates the difference between standard Cassandra client and resilient client
/// Shows how the resilient client handles failures and recovers automatically
/// </summary>
public class ResilienceDemo
{
    private readonly ILogger<ResilienceDemo> _logger;
    private readonly ProbeConfiguration _configuration;
    private readonly ISessionManager _standardSessionManager;
    private readonly IResilientCassandraClient _resilientClient;
    private readonly Timer _executionTimer;
    private bool _isRunning;

    public ResilienceDemo(
        ILogger<ResilienceDemo> logger,
        ProbeConfiguration configuration,
        ISessionManager standardSessionManager,
        IResilientCassandraClient resilientClient)
    {
        _logger = logger;
        _configuration = configuration;
        _standardSessionManager = standardSessionManager;
        _resilientClient = resilientClient;
        
        // Execute queries every 2 seconds to demonstrate continuous operation
        _executionTimer = new Timer(ExecuteTestQueries, null, Timeout.Infinite, Timeout.Infinite);
    }

    public async Task StartDemoAsync()
    {
        _logger.LogInformation("=== RESILIENCE DEMONSTRATION STARTED ===");
        _logger.LogInformation("This demo shows how the resilient client handles failures automatically");
        _logger.LogInformation("Try stopping/starting Cassandra nodes to see the difference");
        _logger.LogInformation("Press Ctrl+C to stop the demonstration");
        _logger.LogInformation("");
        
        _isRunning = true;
        
        // Get initial cluster state
        await LogClusterState();
        
        // Start executing queries
        _executionTimer.Change(TimeSpan.Zero, TimeSpan.FromSeconds(2));
        
        // Monitor for user cancellation
        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };
        
        try
        {
            await Task.Delay(Timeout.Infinite, cts.Token);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("\n=== STOPPING RESILIENCE DEMONSTRATION ===");
        }
        finally
        {
            _isRunning = false;
            _executionTimer.Change(Timeout.Infinite, Timeout.Infinite);
            
            // Log final metrics
            await LogFinalMetrics();
        }
    }

    private async void ExecuteTestQueries(object? state)
    {
        if (!_isRunning) return;
        
        // Execute with standard client
        await ExecuteWithStandardClient();
        
        // Execute with resilient client
        await ExecuteWithResilientClient();
        
        _logger.LogInformation("---");
    }

    private async Task ExecuteWithStandardClient()
    {
        try
        {
            var stopwatch = Stopwatch.StartNew();
            var session = await _standardSessionManager.GetSessionAsync();
            
            var result = await session.ExecuteAsync(
                new SimpleStatement("SELECT key, cluster_name FROM system.local")
                    .SetIdempotence(true));
            
            stopwatch.Stop();
            
            var row = result.FirstOrDefault();
            if (row != null)
            {
                _logger.LogInformation(
                    "[STANDARD CLIENT] ✓ Query succeeded in {Duration}ms - Cluster: {Cluster}",
                    stopwatch.ElapsedMilliseconds,
                    row.GetValue<string>("cluster_name"));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                "[STANDARD CLIENT] ✗ Query failed: {Error}",
                GetErrorMessage(ex));
        }
    }

    private async Task ExecuteWithResilientClient()
    {
        try
        {
            var stopwatch = Stopwatch.StartNew();
            
            var result = await _resilientClient.ExecuteIdempotentAsync(
                "SELECT key, cluster_name FROM system.local");
            
            stopwatch.Stop();
            
            var row = result.FirstOrDefault();
            if (row != null)
            {
                _logger.LogInformation(
                    "[RESILIENT CLIENT] ✓ Query succeeded in {Duration}ms - Cluster: {Cluster}",
                    stopwatch.ElapsedMilliseconds,
                    row.GetValue<string>("cluster_name"));
            }
            
            // Log current metrics periodically
            if (Random.Shared.Next(10) == 0) // 10% chance
            {
                var metrics = _resilientClient.GetMetrics();
                _logger.LogInformation(
                    "[RESILIENT CLIENT] Metrics: {Up}/{Total} hosts up, Success rate: {Rate:P1}, State changes: {Changes}",
                    metrics.UpHosts,
                    metrics.TotalHosts,
                    metrics.SuccessRate,
                    metrics.StateTransitions);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                "[RESILIENT CLIENT] ✗ Query failed after all retries: {Error}",
                GetErrorMessage(ex));
        }
    }

    private Task LogClusterState()
    {
        try
        {
            var cluster = _standardSessionManager.GetCluster();
            if (cluster != null)
            {
                var hosts = cluster.AllHosts().ToList();
                _logger.LogInformation(
                    "Initial cluster state: {Total} hosts, {Up} up, {Down} down",
                    hosts.Count,
                    hosts.Count(h => h.IsUp),
                    hosts.Count(h => !h.IsUp));
                
                foreach (var host in hosts.OrderBy(h => h.Address.ToString()))
                {
                    _logger.LogInformation(
                        "  Host {Address}: {State} (DC: {DC}, Rack: {Rack})",
                        host.Address,
                        host.IsUp ? "UP" : "DOWN",
                        host.Datacenter,
                        host.Rack);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging cluster state");
        }
        return Task.CompletedTask;
    }

    private Task LogFinalMetrics()
    {
        try
        {
            var metrics = _resilientClient.GetMetrics();
            
            _logger.LogInformation("");
            _logger.LogInformation("=== FINAL METRICS ===");
            _logger.LogInformation("Total queries: {Total}", metrics.TotalQueries);
            _logger.LogInformation("Failed queries: {Failed}", metrics.FailedQueries);
            _logger.LogInformation("Success rate: {Rate:P2}", metrics.SuccessRate);
            _logger.LogInformation("Host state transitions: {Transitions}", metrics.StateTransitions);
            _logger.LogInformation("Uptime: {Uptime:g}", metrics.Uptime);
            
            _logger.LogInformation("");
            _logger.LogInformation("Host states:");
            foreach (var (host, state) in metrics.HostStates.OrderBy(kvp => kvp.Key))
            {
                _logger.LogInformation(
                    "  {Host}: {State}, Failures: {Failures}, Last change: {LastChange:s}",
                    host,
                    state.IsUp ? "UP" : "DOWN",
                    state.ConsecutiveFailures,
                    state.LastStateChange);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging final metrics");
        }
        return Task.CompletedTask;
    }

    private string GetErrorMessage(Exception ex)
    {
        return ex switch
        {
            NoHostAvailableException => "No hosts available",
            OperationTimedOutException => "Operation timed out",
            ReadTimeoutException => "Read timeout",
            WriteTimeoutException => "Write timeout",
            UnavailableException => "Not enough replicas available",
            _ => ex.Message
        };
    }
}

/// <summary>
/// Shows key resilience scenarios and how they're handled
/// </summary>
public static class ResilienceScenarios
{
    public static void LogScenarios(ILogger logger)
    {
        logger.LogInformation("");
        logger.LogInformation("=== RESILIENCE SCENARIOS ===");
        logger.LogInformation("");
        
        logger.LogInformation("1. SINGLE NODE FAILURE");
        logger.LogInformation("   Standard Client: Continues sending queries to failed node, experiences timeouts");
        logger.LogInformation("   Resilient Client: Detects failure within 5s, routes around failed node");
        logger.LogInformation("");
        
        logger.LogInformation("2. ROLLING RESTART");
        logger.LogInformation("   Standard Client: Timeouts on each restarting node, no automatic recovery");
        logger.LogInformation("   Resilient Client: Handles each node independently, maintains service");
        logger.LogInformation("");
        
        logger.LogInformation("3. COMPLETE CLUSTER OUTAGE");
        logger.LogInformation("   Standard Client: All queries fail, manual restart required");
        logger.LogInformation("   Resilient Client: Circuit breakers prevent cascading failures, auto-recovery when cluster returns");
        logger.LogInformation("");
        
        logger.LogInformation("4. NETWORK FLAPPING");
        logger.LogInformation("   Standard Client: Repeated connection failures, poor performance");
        logger.LogInformation("   Resilient Client: Circuit breakers stabilize behavior, prevents connection storms");
        logger.LogInformation("");
        
        logger.LogInformation("5. SLOW NODE");
        logger.LogInformation("   Standard Client: Queries wait for slow node, high latency");
        logger.LogInformation("   Resilient Client: Speculative execution uses faster nodes");
        logger.LogInformation("");
        
        logger.LogInformation("Try these scenarios while the demo is running:");
        logger.LogInformation("- docker stop <cassandra-node>");
        logger.LogInformation("- docker start <cassandra-node>");
        logger.LogInformation("- docker pause <cassandra-node>");
        logger.LogInformation("- docker unpause <cassandra-node>");
        logger.LogInformation("");
    }
}