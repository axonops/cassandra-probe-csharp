using CassandraProbe.Core.Models;

namespace CassandraProbe.Core.Interfaces;

public interface IProbeAction
{
    string Name { get; }
    ProbeType Type { get; }
    Task<ProbeResult> ExecuteAsync(HostProbe host, ProbeContext context);
}