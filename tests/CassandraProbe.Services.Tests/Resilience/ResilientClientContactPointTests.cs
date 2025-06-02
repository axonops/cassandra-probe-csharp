using System.Net;
using CassandraProbe.Core.Configuration;
using CassandraProbe.Core.Exceptions;
using CassandraProbe.Services.Resilience;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CassandraProbe.Services.Tests.Resilience;

public class ResilientClientContactPointTests
{
    private readonly Mock<ILogger<ResilientCassandraClient>> _loggerMock;
    private readonly ProbeConfiguration _configuration;

    public ResilientClientContactPointTests()
    {
        _loggerMock = new Mock<ILogger<ResilientCassandraClient>>();
        _configuration = new ProbeConfiguration
        {
            ContactPoints = new List<string>(),
            Authentication = new AuthenticationSettings(),
            Connection = new ConnectionSettings(),
            ProbeSelection = new ProbeSelectionSettings(),
            Query = new QuerySettings(),
            Logging = new LoggingSettings(),
            Scheduling = new SchedulingSettings()
        };
    }

    [Theory]
    [InlineData("10.16.0.46:9042")]
    [InlineData("localhost:9042")]
    [InlineData("cassandra-node1:9042")]
    [InlineData("192.168.1.100:9042")]
    public void ResilientClient_ShouldHandleContactPointsWithPort(string contactPoint)
    {
        // Arrange
        _configuration.ContactPoints = new List<string> { contactPoint };

        // Act & Assert - Should not throw
        var exception = Record.Exception(() =>
        {
            using var client = new ResilientCassandraClient(_configuration, _loggerMock.Object);
        });

        // The client will fail to connect (no Cassandra running) but should parse the contact point correctly
        exception.Should().NotBeNull();
        exception.Should().BeOfType<ConnectionException>();
        exception!.Message.Should().NotContain("No host name could be resolved");
    }

    [Theory]
    [InlineData("cassandra-node1")]
    [InlineData("localhost")]
    [InlineData("10.16.0.46")]
    public void ResilientClient_ShouldHandleContactPointsWithoutPort(string contactPoint)
    {
        // Arrange
        _configuration.ContactPoints = new List<string> { contactPoint };

        // Act & Assert - Should not throw parsing error
        var exception = Record.Exception(() =>
        {
            using var client = new ResilientCassandraClient(_configuration, _loggerMock.Object);
        });

        // The client will fail to connect (no Cassandra running) but should parse the contact point correctly
        exception.Should().NotBeNull();
        exception.Should().BeOfType<ConnectionException>();
    }

    [Fact]
    public void ResilientClient_ShouldHandleMultipleContactPoints()
    {
        // Arrange
        _configuration.ContactPoints = new List<string> 
        { 
            "node1:9042",
            "node2:9043",
            "node3"
        };

        // Act & Assert - Should not throw parsing error
        var exception = Record.Exception(() =>
        {
            using var client = new ResilientCassandraClient(_configuration, _loggerMock.Object);
        });

        // The client will fail to connect (no Cassandra running) but should parse all contact points correctly
        exception.Should().NotBeNull();
        exception.Should().BeOfType<ConnectionException>();
    }
}