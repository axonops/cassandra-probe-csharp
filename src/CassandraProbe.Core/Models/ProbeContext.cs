using CassandraProbe.Core.Configuration;
using Microsoft.Extensions.Logging;

namespace CassandraProbe.Core.Models;

public class ProbeContext
{
    // Core properties used by the implementation
    public string SessionId { get; init; } = Guid.NewGuid().ToString();
    public DateTime StartTime { get; init; } = DateTime.UtcNow;
    
    // Configuration property for compatibility with implementation
    public ProbeConfiguration Configuration { get; set; } = new();
    
    // Additional properties needed by ProbeOrchestrator
    public ILogger? Logger { get; set; }
    public CancellationToken CancellationToken { get; set; } = CancellationToken.None;
    
    // Lists for test compatibility
    public List<HostProbe> Hosts { get; set; } = new();
    public List<ProbeType> ProbeTypes { get; set; } = new();
    
    // Query-specific settings for tests
    public string? ConsistencyLevel { get; set; }
    public TimeSpan QueryTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan SocketTimeout { get; set; } = TimeSpan.FromSeconds(5);
    public bool EnableTracing { get; set; }
    public string TestQuery { get; set; } = "SELECT key FROM system.local";
    public int MaxRetries { get; set; } = 3;
}