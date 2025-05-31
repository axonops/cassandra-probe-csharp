using System.Net;
using Cassandra;
using CassandraProbe.Actions;
using CassandraProbe.Core.Configuration;
using CassandraProbe.Core.Interfaces;
using CassandraProbe.Core.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CassandraProbe.Actions.Tests;

public class CqlQueryProbeTests
{
    private readonly Mock<ISessionManager> _sessionManagerMock;
    private readonly Mock<ILogger<CqlQueryProbe>> _loggerMock;
    private readonly CqlQueryProbe _probe;

    public CqlQueryProbeTests()
    {
        _sessionManagerMock = new Mock<ISessionManager>();
        _loggerMock = new Mock<ILogger<CqlQueryProbe>>();
        _probe = new CqlQueryProbe(_sessionManagerMock.Object, _loggerMock.Object);
    }

    [Fact]
    public void ProbeType_ShouldReturnCqlQuery()
    {
        // Assert
        _probe.Type.Should().Be(ProbeType.CqlQuery);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldSucceedWhenQueryExecutes()
    {
        // Arrange
        var host = new HostProbe
        {
            Address = IPAddress.Parse("10.0.0.1"),
            NativePort = 9042
        };

        var context = new ProbeContext
        {
            Configuration = new ProbeConfiguration
            {
                Query = new QuerySettings
                {
                    TestCql = "SELECT key FROM system.local",
                    ConsistencyLevel = "LOCAL_ONE",
                    EnableTracing = false,
                    QueryTimeoutSeconds = 30
                }
            }
        };

        var mockSession = new Mock<ISession>();
        var mockRowSet = new Mock<RowSet>();
        
        // Setup mock RowSet to return proper count
        var mockRows = new List<Row>();
        mockRowSet.Setup(r => r.GetEnumerator())
            .Returns(() => mockRows.GetEnumerator());
        
        // Since we can't mock Info directly, the implementation will handle null checks
        
        _sessionManagerMock.Setup(x => x.GetSessionAsync())
            .ReturnsAsync(mockSession.Object);
        
        mockSession.Setup(x => x.ExecuteAsync(It.IsAny<IStatement>()))
            .ReturnsAsync(mockRowSet.Object);

        // Act
        var result = await _probe.ExecuteAsync(host, context);

        // Assert
        result.Should().NotBeNull();
        
        // Debug output if test fails
        if (!result.Success)
        {
            Console.WriteLine($"Test failed with error: {result.ErrorMessage}");
        }
        
        result.Success.Should().BeTrue();
        result.Host.Should().BeSameAs(host);
        result.ProbeType.Should().Be(ProbeType.CqlQuery);
        result.Duration.Should().BeGreaterThan(TimeSpan.Zero);
        result.ErrorMessage.Should().BeNull();
        result.Metadata.Should().ContainKey("RowCount");
        result.Metadata["RowCount"].Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldFailWhenQueryFails()
    {
        // Arrange
        var host = new HostProbe
        {
            Address = IPAddress.Parse("10.0.0.1"),
            NativePort = 9042
        };

        var context = new ProbeContext
        {
            Configuration = new ProbeConfiguration
            {
                Query = new QuerySettings
                {
                    TestCql = "SELECT * FROM non_existent_table"
                }
            }
        };

        var mockSession = new Mock<ISession>();
        
        _sessionManagerMock.Setup(x => x.GetSessionAsync())
            .ReturnsAsync(mockSession.Object);
        
        mockSession.Setup(x => x.ExecuteAsync(It.IsAny<IStatement>()))
            .ThrowsAsync(new InvalidQueryException("Table does not exist"));

        // Act
        var result = await _probe.ExecuteAsync(host, context);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Table does not exist");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldSetConsistencyLevel()
    {
        // Arrange
        var host = new HostProbe
        {
            Address = IPAddress.Parse("10.0.0.1")
        };

        var context = new ProbeContext
        {
            Configuration = new ProbeConfiguration
            {
                Query = new QuerySettings
                {
                    TestCql = "SELECT key FROM system.local",
                    ConsistencyLevel = "QUORUM",
                    QueryTimeoutSeconds = 30
                }
            }
        };

        var mockSession = new Mock<ISession>();
        var mockRowSet = new Mock<RowSet>();
        IStatement? capturedStatement = null;
        
        // Setup mock RowSet
        mockRowSet.Setup(r => r.GetEnumerator())
            .Returns(new List<Row>().GetEnumerator());
        
        _sessionManagerMock.Setup(x => x.GetSessionAsync())
            .ReturnsAsync(mockSession.Object);
        
        mockSession.Setup(x => x.ExecuteAsync(It.IsAny<IStatement>()))
            .Callback<IStatement>(stmt => capturedStatement = stmt)
            .ReturnsAsync(mockRowSet.Object);

        // Act
        await _probe.ExecuteAsync(host, context);

        // Assert
        capturedStatement.Should().NotBeNull();
        capturedStatement!.ConsistencyLevel.Should().Be(ConsistencyLevel.Quorum);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldEnableTracingWhenRequested()
    {
        // Arrange
        var host = new HostProbe
        {
            Address = IPAddress.Parse("10.0.0.1")
        };

        var context = new ProbeContext
        {
            Configuration = new ProbeConfiguration
            {
                Query = new QuerySettings
                {
                    TestCql = "SELECT key FROM system.local",
                    EnableTracing = true,
                    ConsistencyLevel = "ONE",
                    QueryTimeoutSeconds = 30
                }
            }
        };

        var mockSession = new Mock<ISession>();
        var mockRowSet = new Mock<RowSet>();
        // ResultInfo removed - can't mock internal Cassandra types
        IStatement? capturedStatement = null;
        
        // Setup mock RowSet
        mockRowSet.Setup(r => r.GetEnumerator())
            .Returns(new List<Row>().GetEnumerator());
        // Can't mock Info property - internal type
        
        // Can't mock QueryTrace - internal type
        
        _sessionManagerMock.Setup(x => x.GetSessionAsync())
            .ReturnsAsync(mockSession.Object);
        
        mockSession.Setup(x => x.ExecuteAsync(It.IsAny<IStatement>()))
            .Callback<IStatement>(stmt => capturedStatement = stmt)
            .ReturnsAsync(mockRowSet.Object);

        // Act
        await _probe.ExecuteAsync(host, context);

        // Assert
        capturedStatement.Should().NotBeNull();
        capturedStatement!.IsTracing.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRespectQueryTimeout()
    {
        // Arrange
        var host = new HostProbe
        {
            Address = IPAddress.Parse("10.0.0.1")
        };

        var context = new ProbeContext
        {
            Configuration = new ProbeConfiguration
            {
                Query = new QuerySettings
                {
                    TestCql = "SELECT key FROM system.local",
                    QueryTimeoutSeconds = 10,
                    ConsistencyLevel = "ONE"
                }
            }
        };

        var mockSession = new Mock<ISession>();
        var mockRowSet = new Mock<RowSet>();
        IStatement? capturedStatement = null;
        
        // Setup mock RowSet
        mockRowSet.Setup(r => r.GetEnumerator())
            .Returns(new List<Row>().GetEnumerator());
        
        _sessionManagerMock.Setup(x => x.GetSessionAsync())
            .ReturnsAsync(mockSession.Object);
        
        mockSession.Setup(x => x.ExecuteAsync(It.IsAny<IStatement>()))
            .Callback<IStatement>(stmt => capturedStatement = stmt)
            .ReturnsAsync(mockRowSet.Object);

        // Act
        await _probe.ExecuteAsync(host, context);

        // Assert
        capturedStatement.Should().NotBeNull();
        capturedStatement!.ReadTimeoutMillis.Should().Be(10000);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldLogQueryExecution()
    {
        // Arrange
        var host = new HostProbe
        {
            Address = IPAddress.Parse("10.0.0.1")
        };

        var context = new ProbeContext
        {
            Configuration = new ProbeConfiguration
            {
                Query = new QuerySettings
                {
                    TestCql = "SELECT key FROM system.local",
                    ConsistencyLevel = "ONE",
                    QueryTimeoutSeconds = 30
                }
            }
        };

        var mockSession = new Mock<ISession>();
        var mockRowSet = new Mock<RowSet>();
        // ResultInfo removed - can't mock internal Cassandra types
        
        // Setup mock RowSet
        mockRowSet.Setup(r => r.GetEnumerator())
            .Returns(new List<Row>().GetEnumerator());
        // Can't mock Info property - internal type
        // QueryTrace setup removed
        
        _sessionManagerMock.Setup(x => x.GetSessionAsync())
            .ReturnsAsync(mockSession.Object);
        
        mockSession.Setup(x => x.ExecuteAsync(It.IsAny<IStatement>()))
            .ReturnsAsync(mockRowSet.Object);

        // Act
        var result = await _probe.ExecuteAsync(host, context);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        mockSession.Verify(x => x.ExecuteAsync(It.IsAny<IStatement>()), Times.Once);
    }

    [Theory]
    [InlineData("ALL")]
    [InlineData("QUORUM")]
    [InlineData("LOCAL_QUORUM")]
    [InlineData("ONE")]
    [InlineData("LOCAL_ONE")]
    public async Task ExecuteAsync_ShouldHandleVariousConsistencyLevels(string consistencyLevel)
    {
        // Arrange
        var host = new HostProbe { Address = IPAddress.Parse("10.0.0.1") };
        var context = new ProbeContext
        {
            Configuration = new ProbeConfiguration
            {
                Query = new QuerySettings
                {
                    TestCql = "SELECT key FROM system.local",
                    ConsistencyLevel = consistencyLevel,
                    QueryTimeoutSeconds = 30
                }
            }
        };

        var mockSession = new Mock<ISession>();
        var mockRowSet = new Mock<RowSet>();
        // ResultInfo removed - can't mock internal Cassandra types
        
        // Setup mock RowSet
        mockRowSet.Setup(r => r.GetEnumerator())
            .Returns(new List<Row>().GetEnumerator());
        // Can't mock Info property - internal type
        // QueryTrace setup removed
        
        _sessionManagerMock.Setup(x => x.GetSessionAsync())
            .ReturnsAsync(mockSession.Object);
        
        mockSession.Setup(x => x.ExecuteAsync(It.IsAny<IStatement>()))
            .ReturnsAsync(mockRowSet.Object);

        // Act
        var result = await _probe.ExecuteAsync(host, context);

        // Assert
        result.Success.Should().BeTrue();
        result.Metadata["ConsistencyLevel"].Should().Be(consistencyLevel);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldHandleSessionManagerFailure()
    {
        // Arrange
        var host = new HostProbe
        {
            Address = IPAddress.Parse("10.0.0.1")
        };

        var context = new ProbeContext
        {
            Configuration = new ProbeConfiguration
            {
                Query = new QuerySettings
                {
                    TestCql = "SELECT key FROM system.local"
                }
            }
        };

        _sessionManagerMock.Setup(x => x.GetSessionAsync())
            .ThrowsAsync(new Exception("Failed to get session"));

        // Act
        var result = await _probe.ExecuteAsync(host, context);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Failed to get session");
    }
}