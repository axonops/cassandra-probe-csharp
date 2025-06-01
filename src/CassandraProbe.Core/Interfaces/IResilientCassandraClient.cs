using Cassandra;

namespace CassandraProbe.Core.Interfaces;

/// <summary>
/// Interface for a resilient Cassandra client with enhanced connection recovery
/// </summary>
public interface IResilientCassandraClient : IDisposable
{
    /// <summary>
    /// Execute a CQL query with automatic retry, failover, and recovery
    /// </summary>
    Task<RowSet> ExecuteAsync(string cql, params object[] values);
    
    /// <summary>
    /// Execute a statement with automatic retry, failover, and recovery
    /// </summary>
    Task<RowSet> ExecuteAsync(IStatement statement);
    
    /// <summary>
    /// Execute an idempotent query with speculative execution enabled
    /// </summary>
    Task<RowSet> ExecuteIdempotentAsync(string cql, params object[] values);
    
    /// <summary>
    /// Get current metrics for monitoring
    /// </summary>
    ResilientClientMetrics GetMetrics();
    
    /// <summary>
    /// Check if the client is healthy and can execute queries
    /// </summary>
    Task<bool> IsHealthyAsync();
}

/// <summary>
/// Metrics for monitoring the resilient client
/// </summary>
public class ResilientClientMetrics
{
    public long TotalQueries { get; set; }
    public long FailedQueries { get; set; }
    public double SuccessRate { get; set; }
    public long StateTransitions { get; set; }
    public int UpHosts { get; set; }
    public int TotalHosts { get; set; }
    public TimeSpan Uptime { get; set; }
    public Dictionary<string, HostMetrics> HostStates { get; set; } = new();
}

public class HostMetrics
{
    public bool IsUp { get; set; }
    public int ConsecutiveFailures { get; set; }
    public DateTime LastStateChange { get; set; }
    public DateTime? LastHealthCheck { get; set; }
    public TimeSpan? LastHealthCheckDuration { get; set; }
}