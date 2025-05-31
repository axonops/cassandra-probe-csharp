using System.Net;
using Cassandra;

namespace CassandraProbe.TestHelpers.TestDoubles;

/// <summary>
/// A test double for Cassandra Host that can be used in unit tests
/// </summary>
public class TestHost
{
    public IPEndPoint Address { get; }
    public bool IsUp { get; set; }
    public string Datacenter { get; }
    public string Rack { get; }
    public Guid HostId { get; }

    public TestHost(IPEndPoint address, bool isUp = true, string datacenter = "datacenter1", string rack = "rack1")
    {
        Address = address;
        IsUp = isUp;
        Datacenter = datacenter;
        Rack = rack;
        HostId = Guid.NewGuid();
    }

    public static TestHost CreateLocalHost(int port = 9042)
    {
        return new TestHost(new IPEndPoint(IPAddress.Loopback, port));
    }

    public static TestHost CreateHost(string ip, int port = 9042, bool isUp = true)
    {
        return new TestHost(new IPEndPoint(IPAddress.Parse(ip), port), isUp);
    }
}