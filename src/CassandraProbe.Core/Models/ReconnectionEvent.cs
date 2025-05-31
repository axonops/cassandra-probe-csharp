using System.Net;

namespace CassandraProbe.Core.Models;

public enum ReconnectionEventType
{
    AttemptStarted,
    Success,
    Failed,
    ConnectionLost,
    ReconnectionAttempt,
    ReconnectionSuccess
}

public class ReconnectionEvent
{
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public ReconnectionEventType EventType { get; init; }
    public IPEndPoint Host { get; init; } = null!;
    public string? Details { get; init; }
    public string? Message { get; init; }
    public TimeSpan? Duration { get; init; }
}