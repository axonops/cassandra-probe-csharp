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
/// Production-grade resilient Cassandra client with automatic recovery capabilities.
/// Provides session/cluster recreation, circuit breakers, multi-DC support, and comprehensive monitoring.
/// 
/// Key Features:
/// - Automatic session/cluster recreation on failures
/// - Per-host circuit breakers to prevent connection storms
/// - Multi-datacenter aware with health monitoring
/// - Graceful degradation modes based on cluster health
/// - Aggressive connection recovery for failed hosts
/// - Comprehensive metrics and monitoring
/// 
/// This implementation follows Cassandra best practices and provides hooks for
/// custom retry policies and consistency level management.
/// </summary>
public class ResilientCassandraClient : IResilientCassandraClient, IDisposable
{
    private ICluster _cluster = null!;
    private ISession _session = null!;
    private readonly ILogger<ResilientCassandraClient> _logger;
    private readonly ProbeConfiguration _configuration;
    private readonly ResilientClientOptions _options;
    
    // Monitoring and recovery components
    private readonly Timer _hostMonitorTimer;        // Monitors host up/down states
    private readonly Timer _connectionRefreshTimer;   // Periodically refreshes connections
    private readonly Timer _healthCheckTimer;         // Checks overall cluster health
    private readonly ConcurrentDictionary<IPAddress, HostStateInfo> _hostStates = new();          // Tracks state per host
    private readonly ConcurrentDictionary<IPAddress, CircuitBreakerState> _circuitBreakers = new(); // Circuit breaker per host
    private readonly SemaphoreSlim _recreationLock = new(1, 1); // Prevents concurrent session recreation
    
    // Metrics
    private long _totalQueries;
    private long _failedQueries;
    private long _stateTransitions;
    private long _sessionRecreations;
    private long _clusterRecreations;
    private DateTime _startTime;
    private DateTime _lastSessionRecreation;
    private OperationMode _currentMode = OperationMode.Normal;
    
    private bool _disposed;

    public ResilientCassandraClient(
        ProbeConfiguration configuration,
        ILogger<ResilientCassandraClient> logger,
        ResilientClientOptions? options = null)
    {
        _configuration = configuration;
        _logger = logger;
        _options = options ?? ResilientClientOptions.Default;
        _startTime = DateTime.UtcNow;
        _lastSessionRecreation = DateTime.UtcNow;
        
        // Validate that LocalDatacenter is provided
        if (string.IsNullOrWhiteSpace(_options.MultiDC.LocalDatacenter))
        {
            throw new ArgumentException(
                "LocalDatacenter must be specified in ResilientClientOptions. " +
                "The resilient client requires a datacenter name to properly monitor only local DC hosts. " +
                "Example: options.MultiDC.LocalDatacenter = \"us-east-1\"");
        }
        
        _logger.LogInformation("Initializing ResilientCassandraClient for datacenter '{LocalDC}' with automatic recovery capabilities", 
            _options.MultiDC.LocalDatacenter);
        
        // Initial connection
        InitializeClusterAndSession();
        
        // Start monitoring timers
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
        
        _healthCheckTimer = new Timer(
            PerformHealthCheck,
            null,
            _options.HealthCheckInterval,
            _options.HealthCheckInterval);
        
        _logger.LogInformation(
            "Resilient client initialized with automatic recovery, circuit breakers, and multi-DC support");
    }
    
    #region Initialization and Recovery
    
    private void InitializeClusterAndSession()
    {
        try
        {
            _cluster = BuildResilientCluster();
            RegisterClusterEventHandlers();
            _session = _cluster.Connect();
            InitializeHostStates();
            
            _logger.LogInformation("Successfully connected to cluster: {ClusterName}", 
                _cluster.Metadata.ClusterName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to establish initial connection");
            throw new ConnectionException("Failed to connect to Cassandra cluster", ex);
        }
    }
    
    private void RegisterClusterEventHandlers()
    {
        if (_cluster == null) return;
        
        // Register topology change event handlers
        _cluster.HostAdded += OnClusterHostAdded;
        _cluster.HostRemoved += OnClusterHostRemoved;
        
        _logger.LogInformation("[RESILIENT CLIENT] Cluster topology event handlers registered");
    }
    
    private void OnClusterHostAdded(Host host)
    {
        // Only care about hosts in our local datacenter
        if (!string.Equals(host.Datacenter, _options.MultiDC.LocalDatacenter, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug("[TOPOLOGY CHANGE] Ignoring host ADDED in remote DC: {Host} DC={DC}", 
                host.Address, host.Datacenter);
            return;
        }
        
        _logger.LogInformation("[TOPOLOGY CHANGE] Host ADDED in local DC: {Host} DC={DC} Rack={Rack}", 
            host.Address, host.Datacenter, host.Rack);
        
        var hostAddress = host.Address.Address;
        
        // Add to monitoring if not already present
        if (!_hostStates.ContainsKey(hostAddress))
        {
            _hostStates[hostAddress] = new HostStateInfo
            {
                IsUp = host.IsUp,
                LastSeen = DateTime.UtcNow,
                LastStateChange = DateTime.UtcNow,
                ConsecutiveFailures = 0,
                Datacenter = host.Datacenter
            };
            
            _circuitBreakers[hostAddress] = new CircuitBreakerState(_options.CircuitBreaker);
            
            _logger.LogInformation("[RESILIENT CLIENT] Added local DC host {Host} to monitoring (State: {State})",
                hostAddress, host.IsUp ? "UP" : "DOWN");
        }
        
        // Trigger immediate health check for the new host
        Task.Run(async () => await PerformHostHealthCheck(host));
    }
    
    private void OnClusterHostRemoved(Host host)
    {
        // Only care about hosts in our local datacenter
        if (!string.Equals(host.Datacenter, _options.MultiDC.LocalDatacenter, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug("[TOPOLOGY CHANGE] Ignoring host REMOVED in remote DC: {Host} DC={DC}", 
                host.Address, host.Datacenter);
            return;
        }
        
        _logger.LogInformation("[TOPOLOGY CHANGE] Host REMOVED from local DC: {Host} DC={DC}", 
            host.Address, host.Datacenter);
        
        var hostAddress = host.Address.Address;
        
        // Remove from monitoring
        if (_hostStates.TryRemove(hostAddress, out var removedState))
        {
            _logger.LogInformation("[RESILIENT CLIENT] Removed local DC host {Host} from monitoring (was {State})",
                hostAddress, removedState.IsUp ? "UP" : "DOWN");
        }
        
        // Remove circuit breaker
        _circuitBreakers.TryRemove(hostAddress, out _);
    }
    
    private async Task<ISession> GetHealthySessionAsync()
    {
        if (_session != null && await IsSessionHealthyAsync(_session))
        {
            return _session;
        }
        
        await RecreateSessionAsync();
        return _session ?? throw new InvalidOperationException("Failed to create or recreate session");
    }
    
    private async Task<bool> IsSessionHealthyAsync(ISession session)
    {
        try
        {
            // Health check query using LOCAL_ONE for minimal latency
            // This query:
            // - Uses system.local table which is always available
            // - Is idempotent (safe to retry)
            // - Has a short timeout to fail fast
            // - Uses LOCAL_ONE to avoid cross-DC latency
            var statement = new SimpleStatement("SELECT now() FROM system.local")
                .SetIdempotence(true)
                .SetConsistencyLevel(ConsistencyLevel.LocalOne)
                .SetReadTimeoutMillis(2000);
                
            await session.ExecuteAsync(statement);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Session health check failed: {Error}", ex.Message);
            return false;
        }
    }
    
    private async Task RecreateSessionAsync()
    {
        await _recreationLock.WaitAsync();
        try
        {
            // Double-check session health after acquiring lock
            if (_session != null && await IsSessionHealthyAsync(_session))
            {
                return;
            }
            
            _logger.LogWarning("Recreating Cassandra session due to health check failure");
            
            var oldSession = _session;
            
            try
            {
                // First try to create new session with existing cluster
                _session = await _cluster.ConnectAsync();
                oldSession?.Dispose();
                
                _sessionRecreations++;
                _lastSessionRecreation = DateTime.UtcNow;
                
                _logger.LogInformation("Session successfully recreated (attempt #{Count})", _sessionRecreations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to recreate session with existing cluster, attempting cluster recreation");
                
                // If session creation fails, try recreating the entire cluster
                await RecreateClusterAsync();
            }
        }
        finally
        {
            _recreationLock.Release();
        }
    }
    
    private async Task RecreateClusterAsync()
    {
        _logger.LogWarning("Recreating entire Cassandra cluster and session");
        
        var oldCluster = _cluster;
        var oldSession = _session;
        
        try
        {
            // Create new cluster
            _cluster = BuildResilientCluster();
            
            // Re-register event handlers for the new cluster
            RegisterClusterEventHandlers();
            
            // Create new session
            _session = await _cluster.ConnectAsync();
            
            // Re-initialize monitoring
            InitializeHostStates();
            
            // Dispose old resources
            oldSession?.Dispose();
            oldCluster?.Dispose();
            
            _clusterRecreations++;
            _sessionRecreations++;
            _lastSessionRecreation = DateTime.UtcNow;
            
            _logger.LogInformation("Cluster and session successfully recreated (cluster #{ClusterCount}, session #{SessionCount})", 
                _clusterRecreations, _sessionRecreations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to recreate cluster, keeping old instances");
            _cluster = oldCluster;
            _session = oldSession;
            throw;
        }
    }
    
    #endregion
    
    #region Cluster Building
    
    private ICluster BuildResilientCluster()
    {
        var builder = Cluster.Builder();
        
        // Add contact points - these are the initial nodes to connect to
        // The driver will discover all other nodes in the cluster automatically
        foreach (var contactPoint in _configuration.ContactPoints)
        {
            var parts = contactPoint.Split(':');
            if (parts.Length == 2 && int.TryParse(parts[1], out var port))
            {
                builder.AddContactPoint(parts[0]).WithPort(port);
            }
            else
            {
                builder.AddContactPoint(contactPoint);
            }
        }
        
        // Set default port if specified
        if (_configuration.Connection.Port != 9042)
        {
            builder.WithPort(_configuration.Connection.Port);
        }
        
        // Configure aggressive reconnection for fast recovery
        // ConstantReconnectionPolicy: Attempts reconnection at fixed intervals
        // Alternative: ExponentialReconnectionPolicy for exponential backoff
        builder.WithReconnectionPolicy(new ConstantReconnectionPolicy(
            _options.ReconnectDelayMs));
        
        // Configure socket options from configuration with resilient overrides
        var socketOptions = new SocketOptions()
            .SetConnectTimeoutMillis(_configuration.Connection.ConnectionTimeoutSeconds * 1000)
            .SetReadTimeoutMillis(_configuration.Connection.RequestTimeoutSeconds * 1000)
            .SetKeepAlive(true)
            .SetTcpNoDelay(true);
            
        // Use resilient client timeouts if they're more aggressive
        if (_options.ConnectTimeoutMs < _configuration.Connection.ConnectionTimeoutSeconds * 1000)
        {
            socketOptions.SetConnectTimeoutMillis(_options.ConnectTimeoutMs);
        }
        if (_options.ReadTimeoutMs < _configuration.Connection.RequestTimeoutSeconds * 1000)
        {
            socketOptions.SetReadTimeoutMillis(_options.ReadTimeoutMs);
        }
        
        builder.WithSocketOptions(socketOptions);
        
        // Configure query options from configuration
        builder.WithQueryOptions(new QueryOptions()
            .SetConsistencyLevel(ParseConsistencyLevel(_configuration.Query.ConsistencyLevel)));
        
        // Multi-DC aware load balancing
        // TokenAwarePolicy: Routes queries to replicas that own the data
        // DCAwareRoundRobinPolicy: Prefers local DC, round-robins within DC
        var loadBalancingPolicy = CreateLoadBalancingPolicy();
        builder.WithLoadBalancingPolicy(loadBalancingPolicy);
        
        // Retry policy for transient failures
        // DefaultRetryPolicy: Basic retry logic for timeouts and unavailable
        // Alternative: Create custom IRetryPolicy for specific requirements
        builder.WithRetryPolicy(new DefaultRetryPolicy());
        
        // Speculative execution for read queries (requires idempotence)
        if (_options.EnableSpeculativeExecution)
        {
            builder.WithSpeculativeExecutionPolicy(
                new ConstantSpeculativeExecutionPolicy(
                    _options.SpeculativeDelayMs, 
                    _options.MaxSpeculativeExecutions));
        }
        
        // Authentication
        if (!string.IsNullOrEmpty(_configuration.Authentication.Username))
        {
            builder.WithCredentials(
                _configuration.Authentication.Username, 
                _configuration.Authentication.Password);
        }
        
        // SSL
        if (_configuration.Connection.UseSsl)
        {
            var sslOptions = new SSLOptions()
                .SetRemoteCertValidationCallback((sender, certificate, chain, errors) => true);
                
            if (!string.IsNullOrEmpty(_configuration.Connection.CertificatePath))
            {
                var cert = X509CertificateLoader.LoadCertificateFromFile(_configuration.Connection.CertificatePath);
                sslOptions.SetCertificateCollection(new X509CertificateCollection { cert });
            }
            
            builder.WithSSL(sslOptions);
        }
        
        return builder.Build();
    }
    
    private ILoadBalancingPolicy CreateLoadBalancingPolicy()
    {
        // Always use DC-aware policy with local datacenter
        // This ensures queries are only sent to nodes in the local DC
        var dcAwarePolicy = new DCAwareRoundRobinPolicy(_options.MultiDC.LocalDatacenter);
        
        _logger.LogInformation("Load balancing policy configured for local datacenter '{DC}'", 
            _options.MultiDC.LocalDatacenter);
        
        return new TokenAwarePolicy(dcAwarePolicy);
    }
    
    #endregion
    
    #region Host Monitoring and Circuit Breakers
    
    private void InitializeHostStates()
    {
        _hostStates.Clear();
        _circuitBreakers.Clear();
        
        var localDcHosts = _cluster.AllHosts()
            .Where(h => string.Equals(h.Datacenter, _options.MultiDC.LocalDatacenter, StringComparison.OrdinalIgnoreCase))
            .ToList();
            
        _logger.LogInformation("Initializing monitoring for {Count} hosts in local datacenter '{DC}'", 
            localDcHosts.Count, _options.MultiDC.LocalDatacenter);
        
        foreach (var host in localDcHosts)
        {
            _hostStates[host.Address.Address] = new HostStateInfo
            {
                IsUp = host.IsUp,
                LastSeen = DateTime.UtcNow,
                LastStateChange = DateTime.UtcNow,
                ConsecutiveFailures = 0,
                Datacenter = host.Datacenter
            };
            
            _circuitBreakers[host.Address.Address] = new CircuitBreakerState(_options.CircuitBreaker);
        }
        
        // Log warning if no local DC hosts found
        if (localDcHosts.Count == 0)
        {
            _logger.LogWarning("No hosts found in local datacenter '{DC}'. Available datacenters: {DCs}", 
                _options.MultiDC.LocalDatacenter,
                string.Join(", ", _cluster.AllHosts().Select(h => h.Datacenter).Distinct()));
        }
    }
    
    private void MonitorHostStates(object? state)
    {
        try
        {
            // Only monitor hosts in our local datacenter
            var localDcHosts = _cluster.AllHosts()
                .Where(h => string.Equals(h.Datacenter, _options.MultiDC.LocalDatacenter, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var stateChanges = new List<HostStateChange>();
            
            // Monitor local datacenter health only
            MonitorDatacenterHealth(localDcHosts);
            
            foreach (var host in localDcHosts)
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
                                "[HOST RECOVERY] Host {Host} in DC {DC} is now UP (was down for {Duration:F1}s)",
                                hostAddress,
                                host.Datacenter,
                                (DateTime.UtcNow - previousState.LastStateChange).TotalSeconds);
                            
                            OnHostRecovered(host);
                            
                            // Reset circuit breaker
                            if (_circuitBreakers.TryGetValue(hostAddress, out var breaker))
                            {
                                breaker.Reset();
                            }
                        }
                        else
                        {
                            _logger.LogWarning(
                                "[HOST FAILURE] Host {Host} in DC {DC} is now DOWN",
                                hostAddress,
                                host.Datacenter);
                            
                            OnHostFailed(host);
                        }
                    }
                    
                    previousState.LastSeen = DateTime.UtcNow;
                }
                else
                {
                    // New host discovered through polling (backup for event-based discovery)
                    _hostStates[hostAddress] = new HostStateInfo
                    {
                        IsUp = currentState,
                        LastSeen = DateTime.UtcNow,
                        LastStateChange = DateTime.UtcNow,
                        ConsecutiveFailures = 0,
                        Datacenter = host.Datacenter
                    };
                    
                    // Add circuit breaker if missing
                    if (!_circuitBreakers.ContainsKey(hostAddress))
                    {
                        _circuitBreakers[hostAddress] = new CircuitBreakerState(_options.CircuitBreaker);
                    }
                    
                    if (currentState)
                    {
                        _logger.LogInformation(
                            "[TOPOLOGY REFRESH] New host discovered via polling: {Host} DC={DC} (UP)",
                            hostAddress, host.Datacenter);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "[TOPOLOGY REFRESH] New host discovered via polling: {Host} DC={DC} (DOWN)",
                            hostAddress, host.Datacenter);
                    }
                }
            }
            
            // Clean up removed hosts (only considering local DC hosts)
            var currentAddresses = localDcHosts.Select(h => h.Address.Address).ToHashSet();
            var removedHosts = _hostStates.Keys.Where(k => !currentAddresses.Contains(k)).ToList();
            
            foreach (var removed in removedHosts)
            {
                if (_hostStates.TryRemove(removed, out var removedState))
                {
                    _logger.LogInformation(
                        "[TOPOLOGY REFRESH] Host {Host} removed via polling (was {State})",
                        removed, removedState.IsUp ? "UP" : "DOWN");
                    
                    // Also remove circuit breaker
                    _circuitBreakers.TryRemove(removed, out _);
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
            
            // Update operation mode based on cluster state
            UpdateOperationMode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during host state monitoring");
        }
    }
    
    private void MonitorDatacenterHealth(List<Host> localDcHosts)
    {
        // We only monitor our local datacenter
        var upHosts = localDcHosts.Count(h => h.IsUp);
        var totalHosts = localDcHosts.Count;
        
        if (totalHosts == 0)
        {
            _logger.LogCritical("No hosts configured in local datacenter '{DC}'!", 
                _options.MultiDC.LocalDatacenter);
        }
        else if (upHosts == 0)
        {
            _logger.LogCritical("Local datacenter '{DC}' is completely DOWN! All {Total} hosts are unavailable!", 
                _options.MultiDC.LocalDatacenter, totalHosts);
        }
        else if (upHosts < totalHosts / 2)
        {
            _logger.LogWarning("Local datacenter '{DC}' is DEGRADED: only {Up}/{Total} hosts available", 
                _options.MultiDC.LocalDatacenter, upHosts, totalHosts);
        }
        else if (upHosts < totalHosts)
        {
            _logger.LogInformation("Local datacenter '{DC}' status: {Up}/{Total} hosts available", 
                _options.MultiDC.LocalDatacenter, upHosts, totalHosts);
        }
    }
    
    private void UpdateOperationMode()
    {
        var metrics = GetMetrics();
        var previousMode = _currentMode;
        
        // Determine operation mode based on cluster health
        // This provides a clear escalation path as the cluster degrades
        
        if (metrics.UpHosts == 0)
        {
            // No hosts available - complete outage
            _currentMode = OperationMode.Emergency;
        }
        else if (metrics.UpHosts < metrics.TotalHosts / 2)
        {
            // Less than half the hosts available - high risk of data loss
            // Only allow reads to prevent split-brain scenarios
            _currentMode = OperationMode.ReadOnly;
        }
        else if (metrics.SuccessRate < 0.9 || metrics.UpHosts < metrics.TotalHosts)
        {
            // Some hosts down or high failure rate - degraded but operational
            // Consider implementing consistency level adjustments here
            _currentMode = OperationMode.Degraded;
        }
        else
        {
            // All hosts up and success rate > 90% - healthy cluster
            _currentMode = OperationMode.Normal;
        }
        
        if (_currentMode != previousMode)
        {
            _logger.LogWarning("Operation mode changed from {Previous} to {Current}", 
                previousMode, _currentMode);
        }
    }
    
    private void OnHostFailed(Host host)
    {
        // Only care about failures in our local datacenter
        if (!string.Equals(host.Datacenter, _options.MultiDC.LocalDatacenter, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug("Ignoring host failure in remote DC: {Host} DC={DC}", 
                host.Address, host.Datacenter);
            return;
        }
        
        // Additional recovery actions when local DC host fails
        Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5));
            
            // Check local DC availability
            var localDCHosts = _cluster.AllHosts()
                .Where(h => string.Equals(h.Datacenter, _options.MultiDC.LocalDatacenter, StringComparison.OrdinalIgnoreCase))
                .ToList();
                
            var upInLocalDC = localDCHosts.Count(h => h.IsUp);
            var totalInLocalDC = localDCHosts.Count;
            
            if (upInLocalDC == 0 && totalInLocalDC > 0)
            {
                _logger.LogCritical("All {Total} hosts in local datacenter '{DC}' are DOWN!", 
                    totalInLocalDC, _options.MultiDC.LocalDatacenter);
            }
            else if (upInLocalDC < totalInLocalDC)
            {
                _logger.LogWarning("{Up}/{Total} hosts remain UP in local datacenter '{DC}'",
                    upInLocalDC, totalInLocalDC, _options.MultiDC.LocalDatacenter);
            }
        });
    }
    
    private void OnHostRecovered(Host host)
    {
        // Aggressive connection refresh for recovered host
        Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(2));
            await AggressiveConnectionRefresh(host);
        });
    }
    
    #endregion
    
    #region Connection Management
    
    private async void RefreshConnections(object? state)
    {
        try
        {
            _logger.LogDebug("[CONNECTION REFRESH] Starting periodic connection refresh");
            
            var session = await GetHealthySessionAsync();
            
            // Force metadata refresh
            await session.ExecuteAsync(new SimpleStatement("SELECT key FROM system.local"));
            
            // Test only local DC host connections
            var localDcHosts = _cluster.AllHosts()
                .Where(h => string.Equals(h.Datacenter, _options.MultiDC.LocalDatacenter, StringComparison.OrdinalIgnoreCase))
                .ToList();
                
            var tasks = localDcHosts.Select(h => TestHostConnection(h, aggressive: false)).ToArray();
            var results = await Task.WhenAll(tasks);
            
            var successCount = results.Count(r => r);
            var totalCount = results.Length;
            
            _logger.LogInformation(
                "[CONNECTION REFRESH] Completed: {Success}/{Total} hosts healthy",
                successCount, totalCount);
            
            // Perform aggressive refresh for recovered hosts
            var recoveredHosts = _hostStates
                .Where(kvp => kvp.Value.IsUp && kvp.Value.ConsecutiveFailures > 0)
                .Select(kvp => kvp.Key)
                .ToList();
                
            foreach (var hostAddress in recoveredHosts)
            {
                var host = _cluster.AllHosts().FirstOrDefault(h => h.Address.Address.Equals(hostAddress));
                if (host != null)
                {
                    await AggressiveConnectionRefresh(host);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during connection refresh");
        }
    }
    
    private async Task PerformHostHealthCheck(Host host)
    {
        try
        {
            var statement = new SimpleStatement("SELECT now() FROM system.local")
                .SetHost(host)
                .SetIdempotence(true)
                .SetConsistencyLevel(ConsistencyLevel.LocalOne)
                .SetReadTimeoutMillis(2000);
                
            await _session.ExecuteAsync(statement);
            
            // Host is healthy
            if (_hostStates.TryGetValue(host.Address.Address, out var state))
            {
                state.LastSeen = DateTime.UtcNow;
                if (!state.IsUp)
                {
                    state.IsUp = true;
                    state.LastStateChange = DateTime.UtcNow;
                    _logger.LogInformation("[HOST HEALTH CHECK] Host {Host} is now UP", host.Address);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Host {Host} health check failed: {Error}", 
                host.Address, ex.Message);
        }
    }
    
    private async Task AggressiveConnectionRefresh(Host host)
    {
        _logger.LogInformation("Performing aggressive connection refresh for recovered host {Host}", host.Address);
        
        var tasks = new List<Task>();
        
        // Force multiple connections to refresh the pool
        // This helps ensure stale connections are replaced with fresh ones
        // after a host recovers from a failure
        for (int i = 0; i < _options.ConnectionsPerHost; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    // Target specific host to force connection creation
                    // Using LOCAL_ONE for fast response
                    var statement = new SimpleStatement("SELECT now() FROM system.local")
                        .SetHost(host)
                        .SetIdempotence(true)
                        .SetConsistencyLevel(ConsistencyLevel.LocalOne)
                        .SetReadTimeoutMillis(2000);
                        
                    await _session.ExecuteAsync(statement);
                }
                catch (Exception ex)
                {
                    // Individual connection failures are expected during recovery
                    _logger.LogDebug("Aggressive refresh query {Index} failed: {Error}", i, ex.Message);
                }
            }));
        }
        
        await Task.WhenAll(tasks);
        
        // Reset consecutive failures if host is responding
        if (_hostStates.TryGetValue(host.Address.Address, out var state))
        {
            state.ConsecutiveFailures = 0;
        }
    }
    
    private async Task<bool> TestHostConnection(Host host, bool aggressive = false)
    {
        try
        {
            var stopwatch = Stopwatch.StartNew();
            
            var statement = new SimpleStatement("SELECT now() FROM system.local")
                .SetHost(host)
                .SetIdempotence(true)
                .SetConsistencyLevel(ConsistencyLevel.LocalOne)
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
            
            // Update circuit breaker
            if (_circuitBreakers.TryGetValue(host.Address.Address, out var breaker))
            {
                breaker.RecordSuccess();
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
            
            // Update circuit breaker
            if (_circuitBreakers.TryGetValue(host.Address.Address, out var breaker))
            {
                breaker.RecordFailure();
            }
            
            return false;
        }
    }
    
    private async void PerformHealthCheck(object? state)
    {
        try
        {
            // Check if we need session/cluster recreation
            if (!await IsHealthyAsync())
            {
                _logger.LogWarning("Health check failed, attempting recovery");
                await RecreateSessionAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during health check");
        }
    }
    
    #endregion
    
    #region Query Execution with Circuit Breakers
    
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
            // Step 1: Check operation mode and apply restrictions
            // This may block writes in ReadOnly mode or all queries in Emergency mode
            ApplyOperationModeRestrictions(statement);
            
            // Step 2: Get a healthy session, creating new one if necessary
            // This handles automatic session/cluster recreation
            var session = await GetHealthySessionAsync();
            
            // Step 3: Apply circuit breaker logic
            // This prevents queries to known-bad hosts
            statement = ApplyCircuitBreakerLogic(statement);
            
            // Step 4: Apply retry policy with exponential backoff
            // This handles transient failures gracefully
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
                return await session.ExecuteAsync(statement);
            });
            
            stopwatch.Stop();
            
            if (stopwatch.ElapsedMilliseconds > _options.SlowQueryThresholdMs)
            {
                _logger.LogWarning(
                    "Slow query detected: {Duration}ms - {Query}",
                    stopwatch.ElapsedMilliseconds,
                    statement.ToString());
            }
            
            // Record success for circuit breaker
            RecordQuerySuccess(statement);
            
            return result;
        }
        catch (Exception ex)
        {
            _failedQueries++;
            
            // Record failure for circuit breaker
            RecordQueryFailure(statement, ex);
            
            _logger.LogError(ex, 
                "Query failed after all retry attempts: {Query}",
                statement.ToString());
            throw;
        }
    }
    
    public async Task<RowSet> ExecuteIdempotentAsync(string cql, params object[] values)
    {
        var statement = new SimpleStatement(cql, values)
            .SetIdempotence(true);
        
        return await ExecuteAsync(statement);
    }
    
    private void ApplyOperationModeRestrictions(IStatement statement)
    {
        switch (_currentMode)
        {
            case OperationMode.Emergency:
                throw new InvalidOperationException(
                    "Cluster is in emergency state - no queries allowed. All hosts are down.");
                
            case OperationMode.ReadOnly:
                if (!IsReadQuery(statement))
                {
                    throw new InvalidOperationException(
                        "Cluster is in read-only mode due to degraded state. Only SELECT queries are allowed.");
                }
                break;
                
            case OperationMode.Degraded:
                // In degraded mode, we still allow queries but don't modify consistency level
                // Users should implement their own retry policies if they need consistency level changes
                _logger.LogDebug("Cluster is in degraded state but consistency level remains unchanged");
                
                /* OPTION: Automatic consistency level downgrade for better availability
                 * Uncomment this block if you want automatic consistency downgrade in degraded mode.
                 * This trades consistency for availability during partial outages.
                 * 
                 * WARNING: This may violate your consistency requirements. Only enable if:
                 * - Your application can tolerate eventual consistency
                 * - You understand the implications for your data model
                 * - You've tested the behavior under various failure scenarios
                 * 
                if (statement.ConsistencyLevel == ConsistencyLevel.Quorum || 
                    statement.ConsistencyLevel == ConsistencyLevel.LocalQuorum ||
                    statement.ConsistencyLevel == ConsistencyLevel.All)
                {
                    // Downgrade to LOCAL_ONE for better availability
                    // Note: This only affects this specific query, not the default consistency
                    statement.SetConsistencyLevel(ConsistencyLevel.LocalOne);
                    _logger.LogWarning("Automatically downgraded consistency level from {Original} to LOCAL_ONE due to degraded cluster state", 
                        statement.ConsistencyLevel);
                }
                */
                break;
        }
    }
    
    private IStatement ApplyCircuitBreakerLogic(IStatement statement)
    {
        // Check if statement targets a specific host
        var targetHost = GetTargetHost(statement);
        if (targetHost != null)
        {
            if (_circuitBreakers.TryGetValue(targetHost.Address.Address, out var breaker))
            {
                if (breaker.State == CircuitState.Open)
                {
                    _logger.LogDebug("Circuit breaker OPEN for host {Host}, routing to different host", 
                        targetHost.Address);
                    
                    // Remove host preference to let driver choose another host
                    statement = RemoveHostPreference(statement);
                }
            }
        }
        
        return statement;
    }
    
    private Host? GetTargetHost(IStatement statement)
    {
        // This would need to use reflection or other means to determine target host
        // For now, return null (no specific host targeted)
        return null;
    }
    
    private IStatement RemoveHostPreference(IStatement statement)
    {
        // Create new statement without host preference
        if (statement is SimpleStatement simple)
        {
            return new SimpleStatement(simple.QueryString, simple.QueryValues);
        }
        return statement;
    }
    
    private void RecordQuerySuccess(IStatement statement)
    {
        // Record success for all involved hosts
        // This is simplified - in reality would need to track which host served the query
        foreach (var breaker in _circuitBreakers.Values)
        {
            if (breaker.State == CircuitState.HalfOpen)
            {
                breaker.RecordSuccess();
            }
        }
    }
    
    private void RecordQueryFailure(IStatement statement, Exception ex)
    {
        // Record failure for specific host if identifiable
        if (IsHostSpecificError(ex, out var hostAddress))
        {
            if (_circuitBreakers.TryGetValue(hostAddress, out var breaker))
            {
                breaker.RecordFailure();
                
                if (breaker.State == CircuitState.Open)
                {
                    _logger.LogWarning("Circuit breaker opened for host {Host} after {Failures} consecutive failures",
                        hostAddress, breaker.ConsecutiveFailures);
                }
            }
        }
    }
    
    private bool IsHostSpecificError(Exception ex, out IPAddress hostAddress)
    {
        hostAddress = IPAddress.None;
        // This would need to parse exception details to identify specific host
        // For now, return false
        return false;
    }
    
    private bool IsReadQuery(IStatement statement)
    {
        if (statement is SimpleStatement simple)
        {
            var query = simple.QueryString.Trim().ToUpperInvariant();
            return query.StartsWith("SELECT");
        }
        return false;
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
        
        var dcMetrics = _hostStates
            .GroupBy(kvp => kvp.Value.Datacenter)
            .ToDictionary(
                g => g.Key ?? "unknown",
                g => new DatacenterMetrics
                {
                    TotalHosts = g.Count(),
                    UpHosts = g.Count(kvp => kvp.Value.IsUp),
                    AverageFailures = g.Average(kvp => kvp.Value.ConsecutiveFailures)
                });
        
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
            SessionRecreations = _sessionRecreations,
            ClusterRecreations = _clusterRecreations,
            LastSessionRecreation = _lastSessionRecreation,
            CurrentOperationMode = _currentMode.ToString(),
            DatacenterMetrics = dcMetrics,
            HostStates = _hostStates.ToDictionary(
                kvp => kvp.Key.ToString(),
                kvp => new HostMetrics
                {
                    IsUp = kvp.Value.IsUp,
                    ConsecutiveFailures = kvp.Value.ConsecutiveFailures,
                    LastStateChange = kvp.Value.LastStateChange,
                    LastHealthCheck = kvp.Value.LastHealthCheck,
                    LastHealthCheckDuration = kvp.Value.LastHealthCheckDuration,
                    CircuitBreakerState = _circuitBreakers.TryGetValue(kvp.Key, out var cb) 
                        ? cb.State.ToString() 
                        : "Unknown"
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
            
            // Check if current mode allows queries
            if (_currentMode == OperationMode.Emergency)
            {
                return false;
            }
            
            // Try a simple query
            var session = await GetHealthySessionAsync();
            await session.ExecuteAsync(new SimpleStatement("SELECT now() FROM system.local")
                .SetConsistencyLevel(ConsistencyLevel.LocalOne)
                .SetReadTimeoutMillis(2000));
                
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
        _healthCheckTimer?.Dispose();
        
        // Unregister event handlers before disposing cluster
        if (_cluster != null)
        {
            _cluster.HostAdded -= OnClusterHostAdded;
            _cluster.HostRemoved -= OnClusterHostRemoved;
        }
        
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
        public string? Datacenter { get; set; }
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
            _ => ConsistencyLevel.LocalQuorum
        };
    }
    
    #endregion
}

/// <summary>
/// Enhanced configuration options for the resilient client
/// </summary>
public class ResilientClientOptions
{
    // Monitoring intervals
    public TimeSpan HostMonitoringInterval { get; set; } = TimeSpan.FromSeconds(5);
    public TimeSpan ConnectionRefreshInterval { get; set; } = TimeSpan.FromMinutes(1);
    public TimeSpan HealthCheckInterval { get; set; } = TimeSpan.FromSeconds(30);
    
    // Timeouts
    public int ConnectTimeoutMs { get; set; } = 3000;
    public int ReadTimeoutMs { get; set; } = 5000;
    public long ReconnectDelayMs { get; set; } = 1000;
    
    // Retry behavior
    public int MaxRetryAttempts { get; set; } = 3;
    public int RetryBaseDelayMs { get; set; } = 100;
    public int RetryMaxDelayMs { get; set; } = 1000;
    
    // Speculative execution
    public bool EnableSpeculativeExecution { get; set; } = true;
    public int SpeculativeDelayMs { get; set; } = 200;
    public int MaxSpeculativeExecutions { get; set; } = 2;
    
    // Connection pool
    public int ConnectionsPerHost { get; set; } = 2;
    
    // Query monitoring
    public int SlowQueryThresholdMs { get; set; } = 1000;
    
    // Multi-DC configuration
    public MultiDCConfiguration MultiDC { get; set; } = new();
    
    // Circuit breaker configuration
    public CircuitBreakerOptions CircuitBreaker { get; set; } = new();
    
    public static ResilientClientOptions Default => new();
}

/// <summary>
/// Multi-datacenter configuration
/// </summary>
public class MultiDCConfiguration
{
    public string? LocalDatacenter { get; set; }
    
    [Obsolete("DC failover should be handled at the application level. This property is no longer used.")]
    public int UsedHostsPerRemoteDc { get; set; } = 2;
    
    public bool AllowRemoteDCsForLocalConsistencyLevel { get; set; } = false;
}

/// <summary>
/// Circuit breaker configuration
/// </summary>
public class CircuitBreakerOptions
{
    public int FailureThreshold { get; set; } = 5;
    public TimeSpan OpenDuration { get; set; } = TimeSpan.FromSeconds(30);
    public int SuccessThresholdInHalfOpen { get; set; } = 2;
}

/// <summary>
/// Circuit breaker state tracking
/// </summary>
public class CircuitBreakerState
{
    private readonly CircuitBreakerOptions _options;
    private int _consecutiveFailures;
    private int _consecutiveSuccesses;
    private DateTime _lastFailureTime;
    private DateTime _openedTime;
    private readonly object _lock = new();
    
    public CircuitState State { get; private set; } = CircuitState.Closed;
    public int ConsecutiveFailures => _consecutiveFailures;
    
    public CircuitBreakerState(CircuitBreakerOptions options)
    {
        _options = options;
    }
    
    public void RecordSuccess()
    {
        lock (_lock)
        {
            _consecutiveFailures = 0;
            
            if (State == CircuitState.HalfOpen)
            {
                _consecutiveSuccesses++;
                if (_consecutiveSuccesses >= _options.SuccessThresholdInHalfOpen)
                {
                    State = CircuitState.Closed;
                    _consecutiveSuccesses = 0;
                }
            }
        }
    }
    
    public void RecordFailure()
    {
        lock (_lock)
        {
            _consecutiveFailures++;
            _lastFailureTime = DateTime.UtcNow;
            _consecutiveSuccesses = 0;
            
            if (State == CircuitState.Closed && _consecutiveFailures >= _options.FailureThreshold)
            {
                State = CircuitState.Open;
                _openedTime = DateTime.UtcNow;
            }
            else if (State == CircuitState.HalfOpen)
            {
                State = CircuitState.Open;
                _openedTime = DateTime.UtcNow;
            }
        }
    }
    
    public void Reset()
    {
        lock (_lock)
        {
            State = CircuitState.Closed;
            _consecutiveFailures = 0;
            _consecutiveSuccesses = 0;
        }
    }
    
    public void CheckState()
    {
        lock (_lock)
        {
            if (State == CircuitState.Open && 
                DateTime.UtcNow - _openedTime > _options.OpenDuration)
            {
                State = CircuitState.HalfOpen;
            }
        }
    }
}

/// <summary>
/// Circuit breaker states
/// </summary>
public enum CircuitState
{
    Closed,    // Normal operation
    Open,      // Failing, reject requests
    HalfOpen   // Testing recovery
}

/// <summary>
/// Operation modes for graceful degradation
/// </summary>
public enum OperationMode
{
    Normal,     // All operations allowed
    Degraded,   // Reduced consistency levels
    ReadOnly,   // Only SELECT queries allowed
    Emergency   // No queries allowed
}
