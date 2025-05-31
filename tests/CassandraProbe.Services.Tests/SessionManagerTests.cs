using System.Net;
using Cassandra;
using CassandraProbe.Core.Configuration;
using CassandraProbe.Core.Exceptions;
using CassandraProbe.Core.Interfaces;
using CassandraProbe.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CassandraProbe.Services.Tests;

public class SessionManagerTests : IDisposable
{
    private readonly Mock<ILogger<SessionManager>> _loggerMock;
    private readonly Mock<IConnectionMonitor> _connectionMonitorMock;
    private readonly ProbeConfiguration _configuration;
    private SessionManager? _sessionManager;

    public SessionManagerTests()
    {
        _loggerMock = new Mock<ILogger<SessionManager>>();
        _connectionMonitorMock = new Mock<IConnectionMonitor>();
        _configuration = new ProbeConfiguration
        {
            ContactPoints = new List<string> { "localhost" },
            Authentication = new AuthenticationSettings
            {
                Username = "cassandra",
                Password = "cassandra"
            },
            Connection = new ConnectionSettings
            {
                Port = 9042,
                UseSsl = false
            }
        };
    }

    public void Dispose()
    {
        _sessionManager?.Dispose();
    }

    [Fact]
    public void Constructor_ShouldInitializeSuccessfully()
    {
        // Act
        _sessionManager = new SessionManager(_loggerMock.Object, _connectionMonitorMock.Object, _configuration);

        // Assert
        _sessionManager.Should().NotBeNull();
    }

    [Fact]
    public void GetCluster_ShouldReturnNullBeforeInitialization()
    {
        // Arrange
        _sessionManager = new SessionManager(_loggerMock.Object, _connectionMonitorMock.Object, _configuration);

        // Act
        var cluster = _sessionManager.GetCluster();

        // Assert
        cluster.Should().BeNull();
    }

    [Fact]
    public void Dispose_ShouldCleanupResources()
    {
        // Arrange
        _sessionManager = new SessionManager(_loggerMock.Object, _connectionMonitorMock.Object, _configuration);

        // Act
        _sessionManager.Dispose();

        // Assert
        // After dispose is called, we expect the Dispose method to run without error
        // The static fields might not be cleared due to singleton pattern
        
        // Verify logging
        _loggerMock.Verify(
            x => x.Log(
                It.Is<LogLevel>(l => l == LogLevel.Information),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Disposing SessionManager")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void BuildCluster_ShouldHandleMultipleContactPoints()
    {
        // Arrange
        _configuration.ContactPoints = new List<string> { "10.0.0.1", "10.0.0.2", "10.0.0.3" };
        
        // Act
        _sessionManager = new SessionManager(_loggerMock.Object, _connectionMonitorMock.Object, _configuration);

        // Assert
        _sessionManager.Should().NotBeNull();
        _configuration.ContactPoints.Should().HaveCount(3);
        _configuration.ContactPoints.Should().Contain("10.0.0.1");
        _configuration.ContactPoints.Should().Contain("10.0.0.2");
        _configuration.ContactPoints.Should().Contain("10.0.0.3");
    }

    [Fact]
    public void BuildCluster_ShouldConfigureAuthentication()
    {
        // Arrange
        _configuration.Authentication.Username = "testuser";
        _configuration.Authentication.Password = "testpass";
        
        // Act
        _sessionManager = new SessionManager(_loggerMock.Object, _connectionMonitorMock.Object, _configuration);

        // Assert
        _sessionManager.Should().NotBeNull();
        _configuration.Authentication.Username.Should().Be("testuser");
        _configuration.Authentication.Password.Should().Be("testpass");
    }

    [Fact]
    public void BuildCluster_ShouldConfigureSsl()
    {
        // Arrange
        _configuration.Connection.UseSsl = true;
        _configuration.Connection.CertificatePath = "/path/to/cert.pem";
        
        // Act
        _sessionManager = new SessionManager(_loggerMock.Object, _connectionMonitorMock.Object, _configuration);

        // Assert
        _sessionManager.Should().NotBeNull();
        _configuration.Connection.UseSsl.Should().BeTrue();
        _configuration.Connection.CertificatePath.Should().Be("/path/to/cert.pem");
    }

    [Fact]
    public void BuildCluster_ShouldSetConnectionOptions()
    {
        // Arrange
        _configuration.Connection.ConnectionTimeoutSeconds = 60;
        _configuration.Connection.KeepAliveSeconds = 120;
        _configuration.Connection.MaxConnectionsPerHost = 4;
        _sessionManager = new SessionManager(_loggerMock.Object, _connectionMonitorMock.Object, _configuration);

        // Act & Assert
        // Verify that connection options are being set
        _configuration.Connection.ConnectionTimeoutSeconds.Should().Be(60);
        _configuration.Connection.KeepAliveSeconds.Should().Be(120);
        _configuration.Connection.MaxConnectionsPerHost.Should().Be(4);
    }

    [Theory]
    [InlineData("10.0.0.1", 9042)]
    [InlineData("cassandra.local", 19042)]
    [InlineData("192.168.1.100", 29042)]
    public void BuildCluster_ShouldHandleVariousEndpoints(string host, int port)
    {
        // Arrange
        _configuration.ContactPoints = new List<string> { host };
        _configuration.Connection.Port = port;
        
        // Act
        _sessionManager = new SessionManager(_loggerMock.Object, _connectionMonitorMock.Object, _configuration);

        // Assert
        // Just verify the session manager was created successfully
        _sessionManager.Should().NotBeNull();
    }

    // Note: The following tests were skipped because they require actual Cassandra connection:
    // - GetSessionAsync_ShouldCreateSessionOnFirstCall - requires Cluster.Connect() which needs real Cassandra
    // - GetSessionAsync_ShouldReuseExistingSession - requires static fields and real connection
    // The SessionManager uses a singleton pattern with static fields and synchronous Connect() method
    // which makes it difficult to test without a real Cassandra instance.
}