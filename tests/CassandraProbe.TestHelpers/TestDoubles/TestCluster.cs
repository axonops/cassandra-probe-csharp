using System.Net;
using Cassandra;

namespace CassandraProbe.TestHelpers.TestDoubles;

/// <summary>
/// A simple test cluster for unit testing without complex Cassandra driver dependencies
/// </summary>
public class TestCluster
{
    private readonly List<TestHost> _hosts = new();
    private readonly string _clusterName;
    
    public TestCluster(string clusterName = "TestCluster")
    {
        _clusterName = clusterName;
    }
    
    public string ClusterName => _clusterName;
    
    public IReadOnlyList<TestHost> Hosts => _hosts;
    
    public void AddHost(TestHost host)
    {
        _hosts.Add(host);
    }
    
    public void AddHost(string ip, int port = 9042, bool isUp = true)
    {
        _hosts.Add(TestHost.CreateHost(ip, port, isUp));
    }
    
    public TestHost? GetHost(IPEndPoint endpoint)
    {
        return _hosts.FirstOrDefault(h => h.Address.Equals(endpoint));
    }
    
    public void RemoveHost(IPEndPoint endpoint)
    {
        _hosts.RemoveAll(h => h.Address.Equals(endpoint));
    }
    
    public static TestCluster CreateSingleNodeCluster()
    {
        var cluster = new TestCluster();
        cluster.AddHost(TestHost.CreateLocalHost());
        return cluster;
    }
    
    public static TestCluster CreateMultiNodeCluster(int nodeCount = 3)
    {
        var cluster = new TestCluster();
        for (int i = 1; i <= nodeCount; i++)
        {
            cluster.AddHost($"10.0.0.{i}", 9042, true);
        }
        return cluster;
    }
}