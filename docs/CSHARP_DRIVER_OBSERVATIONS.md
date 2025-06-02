# Connection Recovery Observations with the C# Driver

## Overview

During our work with the DataStax C# driver for Apache Cassandra, we've encountered scenarios where applications struggle to recover from cluster topology changes. While the exact causes aren't always clear, we've documented our observations and the workarounds we've developed.

**Related Resources:**
- [C# Driver API Documentation](https://docs.datastax.com/en/latest-csharp-driver-api/api/Cassandra.ICluster.html)
- [Java Driver Host.StateListener](https://docs.datastax.com/en/drivers/java/2.1/com/datastax/driver/core/Host.StateListener.html) - Different approach in Java
- [Stack Overflow Discussions](https://stackoverflow.com/questions/tagged/datastax-csharp-driver+connection)

**Some Historical Issues We've Found:**
- CSHARP-252: "Metadata.HostEvent is not firing when a node is Down"
- CSHARP-878: "ControlConnection attempts to connect to DOWN nodes"
- CSHARP-802: "Session.Warmup should mark host as down if no connection can be opened"

## Event Model Differences We've Observed

### Events Available in C# Driver (v3.x)
- `ICluster.HostAdded` - New node joins the cluster
- `ICluster.HostRemoved` - Node permanently removed from cluster

### What We've Noticed
In our testing, we've observed that the C# driver doesn't expose host state transition events (when nodes go up/down) to application code, unlike the Java driver which provides a `Host.StateListener` interface. While the driver appears to track these states internally, we haven't found a way to subscribe to these state changes in our applications.

This difference in architecture may contribute to the recovery behaviors we've observed, though we can't be certain this is the root cause.

## Recovery Challenges We've Encountered

In our experience, the lack of accessible host state change events has led to several challenges:

1. **Delayed failure detection** - We've seen applications continue attempting to use failed nodes for several seconds
2. **Recovery timing issues** - After nodes come back online, applications don't always resume using them promptly
3. **Connection pool behavior** - The connection pools don't always refresh as we'd expect after topology changes

## What We've Seen During Rolling Restarts

Here's a typical scenario we've encountered:

```
1. Node A goes down for restart
   - Application continues sending requests to Node A
   - Requests fail with timeout/connection errors

2. After several seconds
   - The driver eventually stops using the failed node
   - But this can take 5-10 seconds in our observations

3. Node A comes back online
   - We've seen cases where applications don't immediately resume using the node
   - Sometimes manual intervention or application restart is needed

4. Load distribution
   - Even after recovery, load may remain unbalanced
   - Some nodes receive more traffic than others
```

### What Others Have Reported

We're not alone in these observations. Community discussions mention:
- Similar delays during failover scenarios
- `NoHostAvailableException` errors in seemingly healthy clusters
- Inconsistent recovery behaviors across different environments

## Potential Causes of Recovery Issues

Based on our testing, these factors may contribute to recovery problems:

### 1. Connection Pool Behavior

In our testing, we've noticed the connection pools don't always behave as expected:
- Dead connections remaining in the pool until they're actually used
- No background health checking of idle connections
- Failed requests when the application tries to use these stale connections

### 2. Failover Timing

We've observed that without immediate failure notifications:
- Requests may continue going to failed nodes until timeouts occur
- This can add several seconds of latency to affected queries
- The exact behavior seems to vary based on load and configuration

### 3. Recovery Timing

Similarly, when nodes come back online:
- Traffic doesn't always resume immediately
- Load distribution may remain unbalanced
- In some cases, we've needed to restart applications to fully recover

## Workarounds We've Tried

### 1. Custom Host State Monitoring

Since we can't subscribe to host state change events, we've implemented polling-based monitoring:

**What we've found helpful:**
- Polling host states every 5-10 seconds gives reasonable detection times
- Logging state changes helps understand what's happening
- Can trigger recovery actions when changes are detected

**Trade-offs to consider:**
- Adds some CPU overhead for the polling
- Detection isn't instant (depends on polling interval)
- May still miss rapid state changes

```csharp
public class HostStateMonitor
{
    private readonly ICluster _cluster;
    private readonly Dictionary<IPAddress, bool> _hostStates;
    
    public async Task MonitorHostStates()
    {
        while (true)
        {
            foreach (var host in _cluster.AllHosts())
            {
                var wasUp = _hostStates.GetValueOrDefault(host.Address);
                var isUp = host.IsUp;
                
                if (wasUp && !isUp)
                {
                    // Host went down - trigger custom logic
                    OnHostDown(host);
                }
                else if (!wasUp && isUp)
                {
                    // Host came up - trigger custom logic
                    OnHostUp(host);
                }
                
                _hostStates[host.Address] = isUp;
            }
            
            await Task.Delay(TimeSpan.FromSeconds(10));
        }
    }
}
```

### 2. Tuning Reconnection Settings

We've found that adjusting timeout and reconnection policies can help:

**What seems to work:**
- Using constant reconnection (every 1-2 seconds) instead of exponential backoff
- Shorter timeouts to detect issues faster
- But be careful - too aggressive and you'll see false positives

**Our experience:**
- These settings help but don't completely solve recovery issues
- The right values depend heavily on your network and cluster setup

```csharp
var cluster = Cluster.Builder()
    .WithReconnectionPolicy(new ConstantReconnectionPolicy(1000)) // Try every second
    .WithSocketOptions(new SocketOptions()
        .SetConnectTimeoutMillis(3000)  // Fail fast
        .SetReadTimeoutMillis(5000))     // Detect dead connections quickly
    .Build();
```

### 3. Periodic Connection Testing

We've implemented periodic health checks to combat stale connections:

**Our approach:**
- Test each host with a lightweight query every minute
- Force metadata refresh to update the driver's view
- This seems to help identify dead connections sooner

**Results:**
- Reduces (but doesn't eliminate) stale connection issues
- Adds some overhead but improves overall reliability

```csharp
public class ConnectionRefresher
{
    private readonly ICluster _cluster;
    private readonly ISession _session;
    
    public async Task RefreshConnections()
    {
        // Force metadata refresh
        await _session.RefreshSchemaAsync();
        
        // Execute a lightweight query on each host to test connectivity
        foreach (var host in _cluster.AllHosts())
        {
            try
            {
                var statement = new SimpleStatement("SELECT key FROM system.local")
                    .SetHost(host);
                await _session.ExecuteAsync(statement);
            }
            catch
            {
                // Host is likely down, connection will be marked as dead
            }
        }
    }
}
```

### 4. Speculative Execution for Read Queries

For read-heavy workloads, speculative execution has helped mask some issues:

**How it helps in our case:**
- Sends queries to multiple nodes if the first is slow
- Can work around a slow/failing node without waiting for timeout
- But only works for idempotent queries

**Considerations:**
- Increases load on your cluster
- Must be certain queries are truly idempotent
- More of a band-aid than a solution

```csharp
var cluster = Cluster.Builder()
    .WithSpeculativeExecutionPolicy(new ConstantSpeculativeExecutionPolicy(
        delay: 100,        // Try another node after 100ms
        maxSpeculativeExecutions: 2))
    .Build();
```

### 5. Implement Circuit Breaker Pattern

The circuit breaker pattern prevents your application from repeatedly trying to use failed nodes. It "opens" the circuit after detecting failures, avoiding the node for a period before attempting recovery.

**Why this helps:**
- Stops wasting time on known-bad nodes
- Reduces timeout delays during failures
- Provides graceful degradation
- Allows time for nodes to recover before retry

**How it works:**
1. **Closed state**: Normal operation, requests flow through
2. **Open state**: After N failures, reject requests immediately
3. **Half-open state**: After cooldown, try one request to test recovery

**Implementation tips:**
- Track failures per host, not globally
- Use time-based or count-based failure thresholds
- Log state transitions for monitoring
- Consider using existing libraries like Polly

```csharp
public class CassandraCircuitBreaker
{
    private readonly Dictionary<IPAddress, CircuitState> _circuits = new();
    
    public async Task<RowSet> ExecuteWithCircuitBreaker(IStatement statement)
    {
        var host = GetTargetHost(statement);
        var circuit = _circuits.GetOrAdd(host, new CircuitState());
        
        if (circuit.IsOpen)
        {
            // Skip this host, try another
            statement.SetHost(GetAlternateHost());
        }
        
        try
        {
            var result = await _session.ExecuteAsync(statement);
            circuit.RecordSuccess();
            return result;
        }
        catch (Exception ex)
        {
            circuit.RecordFailure();
            throw;
        }
    }
}
```

## Alternative Approaches

If you're experiencing persistent issues with direct driver connections, consider:

1. **Implement custom monitoring** - Like our resilient client approach
2. **Use a proxy layer** - Some teams have success with connection pooling proxies
3. **HTTP-based APIs** - REST or GraphQL interfaces can provide more stable connections
4. **Test thoroughly** - Use tools like Cassandra Probe to understand your specific failure patterns

## Example: Complete Recovery Solution

This example combines all the workarounds into a production-ready client that addresses the recovery behaviors we've observed.

**Key features of this solution:**
- **Host monitoring**: Polls every 5 seconds to detect state changes
- **Connection refresh**: Tests connections every minute to prevent staleness  
- **Aggressive timeouts**: Fails fast to detect issues quickly
- **Speculative execution**: Tries alternate nodes for slow queries
- **Idempotent queries**: Safe for retry and speculation

**Usage pattern:**
```csharp
// Create resilient client once at application startup
var client = new ResilientCassandraClient(new[] { "node1", "node2", "node3" }, logger);

// SAFE: SELECT queries are always idempotent
var result = await client.ExecuteSelectAsync("SELECT * FROM my_table WHERE id = ?", id);

// SAFE: UPDATE that sets specific values (not incrementing)
await client.ExecuteWriteAsync(
    "UPDATE users SET email = ? WHERE id = ?", 
    isIdempotent: true,  // Safe - overwrites with specific value
    email, userId);

// UNSAFE: Never mark counter updates as idempotent
await client.ExecuteWriteAsync(
    "UPDATE counters SET views = views + 1 WHERE page_id = ?", 
    isIdempotent: false,  // Would double-count on retry!
    pageId);
```

**Complete implementation:**
```csharp
public class ResilientCassandraClient : IDisposable
{
    private readonly ICluster _cluster;
    private readonly ISession _session;
    private readonly Timer _monitorTimer;
    private readonly Timer _refreshTimer;
    private readonly Dictionary<IPAddress, HostState> _hostStates = new();
    private readonly ILogger _logger;
    
    public ResilientCassandraClient(string[] contactPoints, ILogger logger)
    {
        _logger = logger;
        
        _cluster = Cluster.Builder()
            .AddContactPoints(contactPoints)
            
            // RECONNECTION POLICY: Controls how the driver attempts to reconnect to nodes
            // ExponentialReconnectionPolicy: Starts at baseDelay, doubles each attempt up to maxDelay
            // - baseDelay: 1000ms (1 second) - Initial wait before first reconnection attempt
            // - maxDelay: 30000ms (30 seconds) - Maximum wait between attempts
            // Alternative: ConstantReconnectionPolicy(1000) - Always wait 1 second between attempts
            .WithReconnectionPolicy(new ExponentialReconnectionPolicy(
                baseDelay: 1000,    // Start reconnecting after 1 second
                maxDelay: 30000))   // Cap at 30 seconds between attempts
            
            // SOCKET OPTIONS: Network-level configuration
            .WithSocketOptions(new SocketOptions()
                // Connect timeout: How long to wait for initial TCP connection
                // 3000ms is aggressive but good for local/low-latency networks
                // Increase to 5000-10000ms for cross-region deployments
                .SetConnectTimeoutMillis(3000)
                
                // Read timeout: How long to wait for query response
                // 5000ms works for most queries, increase for analytical workloads
                // This helps detect dead connections quickly
                .SetReadTimeoutMillis(5000)
                
                // TCP KeepAlive: Prevents firewalls from closing idle connections
                // Critical for long-lived connections through NAT/firewalls
                .SetKeepAlive(true)
                
                // Optional: Configure TCP NoDelay for low-latency requirements
                .SetTcpNoDelay(true))
            
            // QUERY OPTIONS: Default query behavior
            .WithQueryOptions(new QueryOptions()
                // Consistency level: Balances consistency vs availability
                // LocalQuorum: Good default, survives single node failures
                // Options: ONE (fastest), QUORUM, ALL (strongest), LOCAL_ONE, LOCAL_QUORUM
                .SetConsistencyLevel(ConsistencyLevel.LocalQuorum)
                
                // WARNING: DO NOT SET DEFAULT IDEMPOTENCE TO TRUE!
                // .SetDefaultIdempotence(true) // DANGEROUS - assumes all queries are idempotent
                // Instead, set idempotence per query to enable retries and speculation safely
                
                // Optional: Set default timeout per query (overrides socket timeout)
                .SetDefaultTimeout(5000))
            
            // LOAD BALANCING POLICY: How queries are distributed across nodes
            // TokenAwarePolicy: Routes queries to nodes owning the data (reduces latency)
            // Wraps DCAwareRoundRobinPolicy: Prefers local datacenter, round-robins within DC
            .WithLoadBalancingPolicy(new TokenAwarePolicy(
                new DCAwareRoundRobinPolicy(
                    // Optionally specify local DC, otherwise auto-detected
                    localDc: null,
                    // How many nodes in remote DCs to use as fallback
                    usedHostsPerRemoteDc: 2)))
            
            // RETRY POLICY: Handles transient failures
            // DefaultRetryPolicy: Retries with same consistency on certain errors
            // Alternative: DowngradingConsistencyRetryPolicy - Tries lower consistency
            .WithRetryPolicy(new DefaultRetryPolicy())
            
            // SPECULATIVE EXECUTION: Send query to multiple nodes for lower latency
            // ConstantSpeculativeExecutionPolicy: Fixed delay before speculation
            // - delay: 200ms - If no response in 200ms, try another node
            // - maxSpeculativeExecutions: 2 - Try up to 2 additional nodes
            // CRITICAL: Only works for queries marked as idempotent!
            // Without SetIdempotence(true) on a query, speculation is disabled
            .WithSpeculativeExecutionPolicy(
                new ConstantSpeculativeExecutionPolicy(
                    delay: 200,  // p99 latency is a good value here
                    maxSpeculativeExecutions: 2))
            
            .Build();
        
        _session = _cluster.Connect();
        _logger.LogInformation("Cassandra client initialized with {Count} contact points", contactPoints.Length);
        
        // Initialize host state tracking
        foreach (var host in _cluster.AllHosts())
        {
            _hostStates[host.Address.Address] = new HostState 
            { 
                IsUp = host.IsUp, 
                LastSeen = DateTime.UtcNow 
            };
        }
        
        // Start host monitoring - Check every 5 seconds
        // Adjust based on your failure detection requirements
        // Lower = faster detection but more overhead
        _monitorTimer = new Timer(MonitorHosts, null, 
            dueTime: TimeSpan.Zero,           // Start immediately
            period: TimeSpan.FromSeconds(5));  // Check every 5 seconds
        
        // Periodic connection refresh - Every 60 seconds
        // Prevents stale connections from accumulating
        // Increase interval if you have many nodes (to reduce overhead)
        _refreshTimer = new Timer(RefreshConnections, null,
            dueTime: TimeSpan.FromMinutes(1),  // First refresh after 1 minute
            period: TimeSpan.FromMinutes(1));   // Then every minute
    }
    
    private void MonitorHosts(object? state)
    {
        try
        {
            foreach (var host in _cluster.AllHosts())
            {
                var currentState = host.IsUp;
                var hostAddress = host.Address.Address;
                
                if (_hostStates.TryGetValue(hostAddress, out var previousState))
                {
                    // Detect state transitions
                    if (previousState.IsUp && !currentState)
                    {
                        _logger.LogWarning("Host {Host} transitioned to DOWN state", hostAddress);
                        
                        // CUSTOM RECOVERY LOGIC HERE
                        // Examples:
                        // - Notify monitoring system
                        // - Adjust application behavior
                        // - Trigger failover procedures
                        // - Update circuit breakers
                        
                        OnHostDown(host);
                    }
                    else if (!previousState.IsUp && currentState)
                    {
                        _logger.LogInformation("Host {Host} transitioned to UP state", hostAddress);
                        
                        // CUSTOM RECOVERY LOGIC HERE
                        // Examples:
                        // - Re-enable traffic to this node
                        // - Reset circuit breakers
                        // - Rebalance load
                        
                        OnHostUp(host);
                    }
                    
                    // Update state
                    previousState.IsUp = currentState;
                    previousState.LastSeen = DateTime.UtcNow;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error monitoring host states");
        }
    }
    
    private async void RefreshConnections(object? state)
    {
        try
        {
            // Force metadata refresh - Updates cluster topology information
            // This ensures we have the latest view of the cluster
            await _session.RefreshSchemaAsync();
            
            // Test each host with a lightweight query
            // This forces the driver to validate/refresh connections
            var tasks = _cluster.AllHosts().Select(host => TestHostConnection(host));
            
            var results = await Task.WhenAll(tasks);
            
            var successCount = results.Count(r => r);
            _logger.LogDebug("Connection refresh completed: {Success}/{Total} hosts responding", 
                successCount, results.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Connection refresh failed");
        }
    }
    
    private async Task<bool> TestHostConnection(Host host)
    {
        try
        {
            // Use a simple, fast query that works on any Cassandra version
            // SetHost() forces execution on specific node
            // SetIdempotence() makes it safe for retries
            var statement = new SimpleStatement("SELECT now() FROM system.local")
                .SetHost(host)
                .SetIdempotence(true)
                .SetConsistencyLevel(ConsistencyLevel.One)  // Use ONE for health checks
                .SetTimeout(2000);  // Short timeout for health checks
            
            await _session.ExecuteAsync(statement);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Host {Host} health check failed: {Error}", 
                host.Address, ex.Message);
            return false;
        }
    }
    
    // Execute queries - MUST specify idempotence per query type
    public async Task<RowSet> ExecuteAsync(string cql, params object[] values)
    {
        var statement = new SimpleStatement(cql, values);
        // DO NOT set idempotence here - let caller decide based on query type
        
        return await _session.ExecuteAsync(statement);
    }
    
    // Execute SELECT query - safe to mark as idempotent
    public async Task<RowSet> ExecuteSelectAsync(string cql, params object[] values)
    {
        var statement = new SimpleStatement(cql, values)
            .SetIdempotence(true);  // SELECT queries are always idempotent
        
        return await _session.ExecuteAsync(statement);
    }
    
    // Execute INSERT/UPDATE with idempotence decision
    public async Task<RowSet> ExecuteWriteAsync(string cql, bool isIdempotent, params object[] values)
    {
        var statement = new SimpleStatement(cql, values)
            .SetIdempotence(isIdempotent);  // Caller must determine safety
        
        return await _session.ExecuteAsync(statement);
    }
    
    /* IDEMPOTENCE GUIDELINES:
     * 
     * ALWAYS IDEMPOTENT (safe to retry/speculate):
     * - SELECT queries
     * - INSERT with IF NOT EXISTS
     * - UPDATE with specific primary key (no counters, no collections with +=/-=)
     * - DELETE with specific primary key
     * 
     * NEVER IDEMPOTENT (unsafe to retry):
     * - Counter updates (counter = counter + 1)
     * - List append/prepend operations (list = list + ['item'])
     * - INSERT without IF NOT EXISTS (could create duplicates)
     * - Any query using non-deterministic functions (now(), uuid())
     * 
     * EXAMPLE USAGE:
     * // Safe - SELECT is always idempotent
     * var users = await client.ExecuteSelectAsync("SELECT * FROM users WHERE id = ?", userId);
     * 
     * // Safe - UPDATE with specific key, no counters
     * await client.ExecuteWriteAsync(
     *     "UPDATE users SET name = ? WHERE id = ?", 
     *     isIdempotent: true,  // Safe because it sets specific value
     *     name, userId);
     * 
     * // UNSAFE - Counter update
     * await client.ExecuteWriteAsync(
     *     "UPDATE stats SET views = views + 1 WHERE id = ?", 
     *     isIdempotent: false,  // NOT safe - would double-count on retry
     *     statId);
     * 
     * // UNSAFE - List append
     * await client.ExecuteWriteAsync(
     *     "UPDATE events SET log = log + ? WHERE id = ?", 
     *     isIdempotent: false,  // NOT safe - would append multiple times
     *     newEvent, eventId);
     */
    
    // Override these methods with your application-specific logic
    protected virtual void OnHostDown(Host host)
    {
        // Example implementations:
        // - Remove host from application-level routing
        // - Alert operations team
        // - Activate disaster recovery procedures
    }
    
    protected virtual void OnHostUp(Host host)
    {
        // Example implementations:
        // - Add host back to routing pool
        // - Reset error counters
        // - Rebalance application load
    }
    
    public void Dispose()
    {
        _monitorTimer?.Dispose();
        _refreshTimer?.Dispose();
        _session?.Dispose();
        _cluster?.Dispose();
    }
    
    private class HostState
    {
        public bool IsUp { get; set; }
        public DateTime LastSeen { get; set; }
    }
```

## Our Approach: The Resilient Client

Based on our experiences, we've developed a resilient client implementation that addresses the recovery issues we've encountered. See the **[Resilient Client Implementation](RESILIENT_CLIENT_IMPLEMENTATION.md)** for details.

Our implementation adds:
- Periodic host state monitoring
- Proactive connection health checks
- Configurable retry strategies
- Metrics to understand recovery behavior

To see how our approach handles these scenarios:

```bash
# Run the resilience demonstration
./cassandra-probe --contact-points node1,node2,node3 --resilient-client
```

## Testing Your Recovery Implementation

Use Cassandra Probe to validate your workarounds:

```bash
# Monitor cluster during rolling restart
./cassandra-probe --contact-points node1,node2,node3 -i 5 --connection-events

# Test scenarios:
# 1. Stop a node - verify your app detects it within polling interval
# 2. Restart the node - verify your app reconnects
# 3. Kill connections - verify refresh logic works
# 4. Simulate network partition - verify circuit breaker activates
```

**What to look for:**
- Detection time: How long before your app notices node is down?
- Recovery time: How long after node returns before traffic resumes?
- Error rate: Are queries failing or being rerouted successfully?
- Connection pool state: Are stale connections being refreshed?

## Summary

Based on our observations, the C# driver behaves differently than expected during cluster topology changes. The absence of accessible host state change events appears to contribute to recovery challenges. While we can't definitively explain why these behaviors occur, implementing additional monitoring and recovery logic has improved reliability in our applications.

## Recent Driver Changes

### Multi-Datacenter Failover (v3.x+)
The C# driver has deprecated automatic DC failover functionality. The `usedHostsPerRemoteDc` parameter in `DCAwareRoundRobinPolicy` is now obsolete and will be removed in future versions. Applications must now implement their own DC failover logic based on their specific requirements. This change reflects the driver team's position that DC failover decisions require application context that the driver cannot provide.