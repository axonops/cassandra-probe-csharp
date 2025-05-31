using CassandraProbe.Core.Configuration;
using CassandraProbe.Core.Models;

namespace CassandraProbe.Core.Interfaces;

public interface IProbeOrchestrator
{
    Task<ProbeSession> ExecuteProbesAsync(ProbeConfiguration config);
    event EventHandler<ProbeCompletedEventArgs>? ProbeCompleted;
}

public class ProbeCompletedEventArgs : EventArgs
{
    public ProbeResult Result { get; set; } = null!;
}