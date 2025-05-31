namespace CassandraProbe.Core.Models;

public class ProbeResult
{
    public HostProbe Host { get; set; } = null!;
    public ProbeType ProbeType { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public TimeSpan Duration { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> Metadata { get; set; } = new();

    public static ProbeResult CreateSuccess(HostProbe host, ProbeType type, TimeSpan duration)
    {
        return new ProbeResult
        {
            Host = host,
            ProbeType = type,
            Success = true,
            Duration = duration
        };
    }

    public static ProbeResult CreateFailure(HostProbe host, ProbeType type, string error, TimeSpan duration)
    {
        return new ProbeResult
        {
            Host = host,
            ProbeType = type,
            Success = false,
            ErrorMessage = error,
            Duration = duration
        };
    }

    public static ProbeResult Timeout(HostProbe host, ProbeType type)
    {
        return new ProbeResult
        {
            Host = host,
            ProbeType = type,
            Success = false,
            ErrorMessage = "Operation timed out",
            Duration = TimeSpan.Zero
        };
    }
}

public enum ProbeType
{
    Socket,
    Ping,
    CqlQuery,
    NativePort,
    StoragePort
}