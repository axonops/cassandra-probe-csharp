using CassandraProbe.Core.Configuration;
using CassandraProbe.Core.Models;

namespace CassandraProbe.Core.Interfaces;

public interface IClusterDiscovery
{
    Task<ClusterTopology> DiscoverAsync(ProbeConfiguration config);
    Task<IEnumerable<HostProbe>> GetHostsAsync();
}