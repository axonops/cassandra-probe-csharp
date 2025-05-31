using System.Net;
using CassandraProbe.Core.Models;

namespace CassandraProbe.TestHelpers;

public static class TestHostBuilder
{
    public static HostProbe CreateHost(string ip = "10.0.0.1", int port = 9042, HostStatus status = HostStatus.Up)
    {
        return new HostProbe
        {
            Address = IPAddress.Parse(ip),
            NativePort = port,
            StoragePort = 7000,
            Datacenter = "dc1",
            Rack = "rack1",
            Status = status,
            CassandraVersion = "4.1.0",
            HostId = Guid.NewGuid().ToString(),
            LastSeen = DateTime.UtcNow
        };
    }

    public static List<HostProbe> CreateHostCluster(int count, string baseIp = "10.0.0", int startFrom = 1)
    {
        var hosts = new List<HostProbe>();
        for (int i = 0; i < count; i++)
        {
            var ip = $"{baseIp}.{startFrom + i}";
            hosts.Add(CreateHost(ip, status: i % 3 == 0 ? HostStatus.Down : HostStatus.Up));
        }
        return hosts;
    }

    public static ClusterTopology CreateTopology(string clusterName = "TestCluster", int hostCount = 3)
    {
        return new ClusterTopology
        {
            ClusterName = clusterName,
            Hosts = CreateHostCluster(hostCount),
            Datacenters = new List<string> { "dc1" }
        };
    }
}