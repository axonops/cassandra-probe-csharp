using Cassandra;
using Moq;
using System.Net;

namespace CassandraProbe.TestHelpers;

public static class MockSessionBuilder
{
    public static Mock<ISession> CreateMockSession()
    {
        var sessionMock = new Mock<ISession>();
        var clusterMock = new Mock<ICluster>();
        var metadataMock = new Mock<Metadata>();
        var allHostsMock = new Mock<ICollection<Host>>();
        
        // Setup basic session behavior
        sessionMock.Setup(s => s.Cluster).Returns(clusterMock.Object);
        clusterMock.Setup(c => c.Metadata).Returns(metadataMock.Object);
        metadataMock.Setup(m => m.AllHosts()).Returns(allHostsMock.Object);
        
        // Setup hosts
        var hosts = CreateMockHosts();
        allHostsMock.Setup(h => h.GetEnumerator()).Returns(hosts.GetEnumerator());
        allHostsMock.Setup(h => h.Count).Returns(hosts.Count);
        
        return sessionMock;
    }

    public static Mock<ISession> CreateMockSessionWithQuery(string query, object result)
    {
        var sessionMock = CreateMockSession();
        var rowSetMock = new Mock<RowSet>();
        
        sessionMock.Setup(s => s.ExecuteAsync(It.IsAny<IStatement>()))
            .ReturnsAsync((IStatement stmt) =>
            {
                if (stmt is SimpleStatement simpleStmt && 
                    simpleStmt.QueryString != null && 
                    simpleStmt.QueryString.Contains(query))
                {
                    return rowSetMock.Object;
                }
                throw new InvalidOperationException($"Unexpected query");
            });
        
        return sessionMock;
    }

    public static Mock<ICluster> CreateMockCluster(string clusterName = "TestCluster")
    {
        var clusterMock = new Mock<ICluster>();
        var metadataMock = new Mock<Metadata>();
        
        clusterMock.Setup(c => c.Metadata).Returns(metadataMock.Object);
        metadataMock.Setup(m => m.ClusterName).Returns(clusterName);
        
        return clusterMock;
    }

    private static List<Host> CreateMockHosts()
    {
        var hosts = new List<Host>();
        
        // Note: Host class in Cassandra driver doesn't have public constructor
        // In real tests, you would need to use reflection or other techniques
        // This is a simplified example
        
        return hosts;
    }
}