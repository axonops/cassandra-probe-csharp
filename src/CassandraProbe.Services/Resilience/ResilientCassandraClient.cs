using System.Collections.Concurrent;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Diagnostics;
using Cassandra;
using CassandraProbe.Core.Configuration;
using CassandraProbe.Core.Exceptions;
using CassandraProbe.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;

namespace CassandraProbe.Services.Resilience;

/// <summary>
/// Production-grade resilient Cassandra client with enhanced connection recovery.
/// Provides automatic failure detection, transparent recovery, and comprehensive monitoring.
/// </summary>
public class ResilientCassandraClient : IResilientCassandraClient, IDisposable
{
    private readonly ICluster _cluster;
    private readonly ISession _session;
    private readonly ILogger<ResilientCassandraClient> _logger;
    private readonly ResilientClientOptions _options;
    
    // Monitoring components
    private readonly Timer _hostMonitorTimer;
    private readonly Timer _connectionRefreshTimer;
    private readonly ConcurrentDictionary<IPAddress, HostStateInfo> _hostStates = new();
    // Circuit breakers removed - simplified implementation for Polly 8.x compatibility
    
    // Metrics
    private long _totalQueries;
    private long _failedQueries;
    private long _stateTransitions;
    private DateTime _startTime;
    
    private bool _disposed;

    public ResilientCassandraClient(
        ProbeConfiguration configuration,
        ILogger<ResilientCassandraClient> logger,
        ResilientClientOptions? options = null)
    {
        _logger = logger;
        _options = options ?? ResilientClientOptions.Default;
        _startTime = DateTime.UtcNow;
        
        _logger.LogInformation("Initializing ResilientCassandraClient with enhanced failure handling");
        
        // Build cluster with resilient configuration
        _cluster = BuildResilientCluster(configuration);
        
        try
        {
            _session = _cluster.Connect();
            _logger.LogInformation("Resilient client connected successfully to cluster: {ClusterName}", 
                _cluster.Metadata.ClusterName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to establish initial connection");
            throw new ConnectionException("Failed to connect to Cassandra cluster", ex);
        }
        
        // Initialize monitoring
        InitializeHostStates();
        
        // Start background monitoring
        _hostMonitorTimer = new Timer(
            MonitorHostStates, 
            null, 
            TimeSpan.Zero, 
            _options.HostMonitoringInterval);
        
        _connectionRefreshTimer = new Timer(
            RefreshConnections, 
            null, 
            _options.ConnectionRefreshInterval, 
            _options.ConnectionRefreshInterval);
        
        _logger.LogInformation(
            "Resilient client initialized with host monitoring every {MonitorInterval}s and connection refresh every {RefreshInterval}s",
            _options.HostMonitoringInterval.TotalSeconds,
            _options.ConnectionRefreshInterval.TotalSeconds);
    }
    
    #region Cluster Building
    
    private ICluster BuildResilientCluster(ProbeConfiguration configuration)
    {
        var builder = Cluster.Builder();
        
        // Add contact points
        foreach (var contactPoint in configuration.ContactPoints)
        {
            builder.AddContactPoint(contactPoint);
        }
        
        // Configure aggressive reconnection for fast recovery
        builder.WithReconnectionPolicy(new ConstantReconnectionPolicy(
            _options.ReconnectDelayMs));
        
        // Configure socket options from configuration with resilient overrides
        var socketOptions = new SocketOptions()
            .SetConnectTimeoutMillis(configuration.Connection.ConnectionTimeoutSeconds * 1000)
            .SetReadTimeoutMillis(configuration.Connection.RequestTimeoutSeconds * 1000)
            .SetKeepAlive(true)
            .SetTcpNoDelay(true);
            
        // Use resilient client timeouts if they're more aggressive
        if (_options.ConnectTimeoutMs < configuration.Connection.ConnectionTimeoutSeconds * 1000)
        {
            socketOptions.SetConnectTimeoutMillis(_options.ConnectTimeoutMs);
        }
        if (_options.ReadTimeoutMs < configuration.Connection.RequestTimeoutSeconds * 1000)
        {
            socketOptions.SetReadTimeoutMillis(_options.ReadTimeoutMs);
        }
        
        builder.WithSocketOptions(socketOptions);
        
        // Configure query options from configuration
        builder.WithQueryOptions(new QueryOptions()
            .SetConsistencyLevel(ParseConsistencyLevel(configuration.Query.ConsistencyLevel)));
        
        // Load balancing with token awareness (same as standard client)
        builder.WithLoadBalancingPolicy(new TokenAwarePolicy(
            new DCAwareRoundRobinPolicy()));
        
        // Retry policy for transient failures
        builder.WithRetryPolicy(new DefaultRetryPolicy());
        
        // Speculative execution for read queries (requires idempotence)
        if (_options.EnableSpeculativeExecution)
        {
            builder.WithSpeculativeExecutionPolicy(
                new ConstantSpeculativeExecutionPolicy(
                    _options.SpeculativeDelayMs, 
                    _options.MaxSpeculativeExecutions));
        }
        
        // Authentication if provided
        if (!string.IsNullOrEmpty(configuration.Authentication.Username))
        {
            builder.WithCredentials(
                configuration.Authentication.Username, 
                configuration.Authentication.Password);
        }
        
        // SSL if enabled
        if (configuration.Connection.UseSsl)
        {
            var sslOptions = new SSLOptions();
            builder.WithSSL(sslOptions);
        }
        
        // Authentication
        if (!string.IsNullOrEmpty(configuration.Authentication.Username) && 
            !string.IsNullOrEmpty(configuration.Authentication.Password))
        {
            builder.WithCredentials(configuration.Authentication.Username, configuration.Authentication.Password);
            _logger.LogInformation("Using username/password authentication");
        }
        
        // SSL Configuration
        if (configuration.Connection.UseSsl)
        {
            var sslOptions = new SSLOptions()
                .SetRemoteCertValidationCallback((sender, certificate, chain, errors) => true);
                
            if (!string.IsNullOrEmpty(configuration.Connection.CertificatePath))
            {
                var cert = X509CertificateLoader.LoadCertificateFromFile(configuration.Connection.CertificatePath);
                sslOptions.SetCertificateCollection(new X509CertificateCollection { cert });
            }
            
            builder.WithSSL(sslOptions);
            _logger.LogInformation("SSL/TLS enabled for connections");
        }
        
        return builder.Build();
    }
    
    #endregion
    
    #region Host Monitoring
    
    private void InitializeHostStates()
    {
        foreach (var host in _cluster.AllHosts())
        {
            _hostStates[host.Address.Address] = new HostStateInfo
            {
                IsUp = host.IsUp,
                LastSeen = DateTime.UtcNow,
                LastStateChange = DateTime.UtcNow,
                ConsecutiveFailures = 0
            };
            
            if (host.IsUp)
            {
                _logger.LogInformation("Initialized host state for {Host}: UP", host.Address);
            }
            else
            {
                _logger.LogWarning("Initialized host state for {Host}: DOWN", host.Address);
            }
        }
    }
    
    private void MonitorHostStates(object? state)
    {
        try
        {
            var hosts = _cluster.AllHosts().ToList();
            var stateChanges = new List<HostStateChange>();
            
            foreach (var host in hosts)
            {
                var currentState = host.IsUp;
                var hostAddress = host.Address.Address;
                
                if (_hostStates.TryGetValue(hostAddress, out var previousState))
                {
                    if (previousState.IsUp != currentState)
                    {
                        // State transition detected
                        _stateTransitions++;
                        previousState.IsUp = currentState;
                        previousState.LastStateChange = DateTime.UtcNow;
                        
                        stateChanges.Add(new HostStateChange
                        {
                            Host = host,
                            WasUp = !currentState,
                            IsUp = currentState,
                            Timestamp = DateTime.UtcNow
                        });
                        
                        if (currentState)
                        {
                            _logger.LogInformation(
                                "[RESILIENT CLIENT] Host {Host} is now UP (was down for {Duration:F1}s)",
                                hostAddress,
                                (DateTime.UtcNow - previousState.LastStateChange).TotalSeconds);
                            
                            OnHostRecovered(host);
                        }
                        else
                        {
                            _logger.LogWarning(
                                "[RESILIENT CLIENT] Host {Host} is now DOWN",
                                hostAddress);
                            
                            OnHostFailed(host);
                        }
                    }
                    
                    previousState.LastSeen = DateTime.UtcNow;
                }
                else
                {
                    // New host discovered
                    _hostStates[hostAddress] = new HostStateInfo
                    {
                        IsUp = currentState,
                        LastSeen = DateTime.UtcNow,
                        LastStateChange = DateTime.UtcNow
                    };
                    
                    if (currentState)
                    {
                        _logger.LogInformation(
                            "[RESILIENT CLIENT] New host discovered: {Host} (UP)",
                            hostAddress);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "[RESILIENT CLIENT] New host discovered: {Host} (DOWN)",
                            hostAddress);
                    }
                }
            }
            
            // Clean up removed hosts
            var currentAddresses = hosts.Select(h => h.Address.Address).ToHashSet();
            var removedHosts = _hostStates.Keys.Where(k => !currentAddresses.Contains(k)).ToList();
            
            foreach (var removed in removedHosts)
            {
                if (_hostStates.TryRemove(removed, out _))
                {
                    _logger.LogInformation(
                        "[RESILIENT CLIENT] Host {Host} removed from cluster",
                        removed);
                }
            }
            
            // Log summary if there were changes
            if (stateChanges.Any())
            {
                var upCount = _hostStates.Count(kvp => kvp.Value.IsUp);
                var totalCount = _hostStates.Count;
                
                _logger.LogInformation(
                    "[RESILIENT CLIENT] Cluster state: {Up}/{Total} hosts UP, {Changes} state changes detected",
                    upCount, totalCount, stateChanges.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during host state monitoring");
        }
    }
    
    private void OnHostFailed(Host host)
    {
        // Log host failure
        _logger.LogDebug("Host {Host} failed, retry policies will handle failures", host.Address);
    }
    
    private void OnHostRecovered(Host host)
    {
        // Test the recovered host
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(2)); // Give it time to fully start
            await TestHostConnection(host);
        });
    }
    
    #endregion
    
    #region Connection Management
    
    private async void RefreshConnections(object? state)
    {
        try
        {
            _logger.LogDebug("[CONNECTION REFRESH] Starting periodic connection refresh");
            
            // Force metadata refresh by executing a lightweight query
            await _session.ExecuteAsync(new SimpleStatement("SELECT key FROM system.local"));
            
            // Test each host connection
            var tasks = _cluster.AllHosts().Select(TestHostConnection).ToArray();
            var results = await Task.WhenAll(tasks);
            
            var successCount = results.Count(r => r);
            var totalCount = results.Length;
            
            _logger.LogInformation(
                "[CONNECTION REFRESH] Completed: {Success}/{Total} hosts healthy",
                successCount, totalCount);
            
            // Log any unhealthy hosts
            var unhealthyHosts = _cluster.AllHosts()
                .Zip(results, (host, healthy) => new { host, healthy })
                .Where(x => !x.healthy)
                .Select(x => x.host.Address)
                .ToList();
            
            if (unhealthyHosts.Any())
            {
                _logger.LogWarning(
                    "[CONNECTION REFRESH] Unhealthy hosts: {Hosts}",
                    string.Join(", ", unhealthyHosts));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during connection refresh");
        }
    }
    
    private async Task<bool> TestHostConnection(Host host)
    {
        try
        {
            var stopwatch = Stopwatch.StartNew();
            
            // Use a lightweight query to test the connection
            var statement = new SimpleStatement("SELECT now() FROM system.local")
                .SetHost(host)
                .SetIdempotence(true)
                .SetConsistencyLevel(ConsistencyLevel.One)
                .SetReadTimeoutMillis(2000);
            
            await _session.ExecuteAsync(statement);
            
            stopwatch.Stop();
            
            _logger.LogDebug(
                "Host {Host} health check succeeded in {Duration}ms",
                host.Address, stopwatch.ElapsedMilliseconds);
            
            // Update host state
            if (_hostStates.TryGetValue(host.Address.Address, out var state))
            {
                state.ConsecutiveFailures = 0;
                state.LastHealthCheck = DateTime.UtcNow;
                state.LastHealthCheckDuration = stopwatch.Elapsed;
            }
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                "Host {Host} health check failed: {Error}",
                host.Address, ex.Message);
            
            // Update failure count
            if (_hostStates.TryGetValue(host.Address.Address, out var state))
            {
                state.ConsecutiveFailures++;
                state.LastHealthCheck = DateTime.UtcNow;
            }
            
            return false;
        }
    }
    
    #endregion
    
    #region Query Execution
    
    public async Task<RowSet> ExecuteAsync(string cql, params object[] values)
    {
        return await ExecuteAsync(new SimpleStatement(cql, values));
    }
    
    public async Task<RowSet> ExecuteAsync(IStatement statement)
    {
        var stopwatch = Stopwatch.StartNew();
        _totalQueries++;
        
        try
        {
            // Apply retry policy
            var retryPolicy = Policy
                .Handle<Exception>(ex => IsRetryableException(ex))
                .WaitAndRetryAsync(
                    retryCount: _options.MaxRetryAttempts,
                    sleepDurationProvider: attempt => TimeSpan.FromMilliseconds(
                        Math.Min(_options.RetryBaseDelayMs * Math.Pow(2, attempt - 1), 
                                _options.RetryMaxDelayMs)),
                    onRetry: (exception, delay, attempt, context) =>
                    {
                        _logger.LogWarning(
                            "Query retry attempt {Attempt}/{Max} after {Delay}ms due to: {Error}",
                            attempt, _options.MaxRetryAttempts, delay.TotalMilliseconds, 
                            exception.Message);
                    });
            
            var result = await retryPolicy.ExecuteAsync(async () =>
            {
                return await _session.ExecuteAsync(statement);
            });
            
            stopwatch.Stop();
            
            if (stopwatch.ElapsedMilliseconds > _options.SlowQueryThresholdMs)
            {
                _logger.LogWarning(
                    "Slow query detected: {Duration}ms - {Query}",
                    stopwatch.ElapsedMilliseconds,
                    statement.ToString());
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _failedQueries++;
            _logger.LogError(ex, 
                "Query failed after all retry attempts: {Query}",
                statement.ToString());
            throw;
        }
    }
    
    public async Task<RowSet> ExecuteIdempotentAsync(string cql, params object[] values)
    {
        var statement = new SimpleStatement(cql, values)
            .SetIdempotence(true); // Enable speculative execution
        
        return await ExecuteAsync(statement);
    }
    
    private bool IsRetryableException(Exception ex)
    {
        return ex is OperationTimedOutException ||
               ex is NoHostAvailableException ||
               ex is ReadTimeoutException ||
               ex is WriteTimeoutException ||
               ex is UnavailableException ||
               (ex is Cassandra.QueryExecutionException qee && qee.Message.Contains("timeout"));
    }
    
    #endregion
    
    #region Metrics and Diagnostics
    
    public ResilientClientMetrics GetMetrics()
    {
        var upHosts = _hostStates.Count(kvp => kvp.Value.IsUp);
        var totalHosts = _hostStates.Count;
        
        return new ResilientClientMetrics
        {
            TotalQueries = _totalQueries,
            FailedQueries = _failedQueries,
            SuccessRate = _totalQueries > 0 ? 
                (double)(_totalQueries - _failedQueries) / _totalQueries : 0,
            StateTransitions = _stateTransitions,
            UpHosts = upHosts,
            TotalHosts = totalHosts,
            Uptime = DateTime.UtcNow - _startTime,
            HostStates = _hostStates.ToDictionary(
                kvp => kvp.Key.ToString(),
                kvp => new HostMetrics
                {
                    IsUp = kvp.Value.IsUp,
                    ConsecutiveFailures = kvp.Value.ConsecutiveFailures,
                    LastStateChange = kvp.Value.LastStateChange,
                    LastHealthCheck = kvp.Value.LastHealthCheck,
                    LastHealthCheckDuration = kvp.Value.LastHealthCheckDuration
                })
        };
    }
    
    public async Task<bool> IsHealthyAsync()
    {
        try
        {
            // Check if we have at least one healthy host
            var healthyHosts = _hostStates.Count(kvp => kvp.Value.IsUp);
            if (healthyHosts == 0)
            {
                return false;
            }
            
            // Try a simple query
            await ExecuteAsync("SELECT now() FROM system.local");
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    #endregion
    
    #region Disposal
    
    public void Dispose()
    {
        if (_disposed) return;
        
        _logger.LogInformation("Disposing ResilientCassandraClient");
        
        _hostMonitorTimer?.Dispose();
        _connectionRefreshTimer?.Dispose();
        
        _session?.Dispose();
        _cluster?.Dispose();
        
        _disposed = true;
        GC.SuppressFinalize(this);
    }
    
    #endregion
    
    #region Helper Classes
    
    private class HostStateInfo
    {
        public bool IsUp { get; set; }
        public DateTime LastSeen { get; set; }
        public DateTime LastStateChange { get; set; }
        public int ConsecutiveFailures { get; set; }
        public DateTime? LastHealthCheck { get; set; }
        public TimeSpan? LastHealthCheckDuration { get; set; }
    }
    
    private class HostStateChange
    {
        public Host Host { get; set; } = null!;
        public bool WasUp { get; set; }
        public bool IsUp { get; set; }
        public DateTime Timestamp { get; set; }
    }
    
    private ConsistencyLevel ParseConsistencyLevel(string level)
    {
        return level.ToUpperInvariant() switch
        {
            "ANY" => ConsistencyLevel.Any,
            "ONE" => ConsistencyLevel.One,
            "TWO" => ConsistencyLevel.Two,
            "THREE" => ConsistencyLevel.Three,
            "QUORUM" => ConsistencyLevel.Quorum,
            "ALL" => ConsistencyLevel.All,
            "LOCAL_QUORUM" => ConsistencyLevel.LocalQuorum,
            "EACH_QUORUM" => ConsistencyLevel.EachQuorum,
            "LOCAL_ONE" => ConsistencyLevel.LocalOne,
            _ => ConsistencyLevel.LocalQuorum // Default to LocalQuorum if not specified
        };
    }
    
    #endregion
}

/// <summary>
/// Configuration options for the resilient client
/// </summary>
public class ResilientClientOptions
{
    public TimeSpan HostMonitoringInterval { get; set; } = TimeSpan.FromSeconds(5);
    public TimeSpan ConnectionRefreshInterval { get; set; } = TimeSpan.FromMinutes(1);
    
    public int ConnectTimeoutMs { get; set; } = 3000;
    public int ReadTimeoutMs { get; set; } = 5000;
    public int QueryTimeoutMs { get; set; } = 5000;
    public long ReconnectDelayMs { get; set; } = 1000;
    
    
    public int MaxRetryAttempts { get; set; } = 3;
    public int RetryBaseDelayMs { get; set; } = 100;
    public int RetryMaxDelayMs { get; set; } = 1000;
    
    public bool EnableSpeculativeExecution { get; set; } = true;
    public int SpeculativeDelayMs { get; set; } = 200;
    public int MaxSpeculativeExecutions { get; set; } = 2;
    
    public int SlowQueryThresholdMs { get; set; } = 1000;
    
    public static ResilientClientOptions Default => new();
}

