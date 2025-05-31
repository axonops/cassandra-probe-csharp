using System.Net;
using Cassandra;

namespace CassandraProbe.Core.Interfaces;

public interface IConnectionMonitor
{
    void RegisterCluster(ICluster cluster);
    ConnectionPoolStatus GetPoolStatus();
    IEnumerable<ReconnectionEvent> GetReconnectionHistory();
    event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;
}

public class ConnectionPoolStatus
{
    public int TotalConnections { get; set; }
    public int ActiveConnections { get; set; }
    public int FailedHosts { get; set; }
    public Dictionary<IPEndPoint, ReconnectionInfo> ReconnectingHosts { get; set; } = new();
}

public class ReconnectionInfo
{
    public int AttemptCount { get; set; }
    public DateTime LastAttempt { get; set; }
    public TimeSpan? NextAttemptIn { get; set; }
}

public class ReconnectionEvent
{
    public DateTime Timestamp { get; set; }
    public IPEndPoint Host { get; set; } = null!;
    public ReconnectionEventType EventType { get; set; }
    public string? Message { get; set; }
    public TimeSpan? Duration { get; set; }
}

public enum ReconnectionEventType
{
    ConnectionLost,
    ReconnectionAttempt,
    ReconnectionSuccess,
    ReconnectionFailure
}

public class ConnectionStateChangedEventArgs : EventArgs
{
    public IPEndPoint Host { get; set; } = null!;
    public ConnectionState OldState { get; set; }
    public ConnectionState NewState { get; set; }
}

public enum ConnectionState
{
    Connected,
    Disconnected,
    Reconnecting
}