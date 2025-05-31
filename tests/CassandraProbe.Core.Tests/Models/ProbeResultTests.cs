using System.Net;
using CassandraProbe.Core.Models;
using FluentAssertions;
using Xunit;

namespace CassandraProbe.Core.Tests.Models;

public class ProbeResultTests
{
    private readonly HostProbe _testHost;

    public ProbeResultTests()
    {
        _testHost = new HostProbe
        {
            Address = IPAddress.Loopback,
            NativePort = 9042,
            Datacenter = "dc1",
            Rack = "rack1",
            Status = HostStatus.Up
        };
    }

    [Fact]
    public void CreateSuccess_ShouldCreateSuccessfulResult()
    {
        // Arrange
        var duration = TimeSpan.FromMilliseconds(100);

        // Act
        var result = ProbeResult.CreateSuccess(_testHost, ProbeType.Socket, duration);

        // Assert
        result.Should().NotBeNull();
        result.Host.Should().Be(_testHost);
        result.ProbeType.Should().Be(ProbeType.Socket);
        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        result.Duration.Should().Be(duration);
        result.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        result.Metadata.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void CreateFailure_ShouldCreateFailedResult()
    {
        // Arrange
        var errorMessage = "Connection refused";
        var duration = TimeSpan.FromMilliseconds(50);

        // Act
        var result = ProbeResult.CreateFailure(_testHost, ProbeType.Socket, errorMessage, duration);

        // Assert
        result.Should().NotBeNull();
        result.Host.Should().Be(_testHost);
        result.ProbeType.Should().Be(ProbeType.Socket);
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be(errorMessage);
        result.Duration.Should().Be(duration);
        result.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Timeout_ShouldCreateTimeoutResult()
    {
        // Act
        var result = ProbeResult.Timeout(_testHost, ProbeType.Ping);

        // Assert
        result.Should().NotBeNull();
        result.Host.Should().Be(_testHost);
        result.ProbeType.Should().Be(ProbeType.Ping);
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Operation timed out");
        result.Duration.Should().Be(TimeSpan.Zero);
        result.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void ProbeResult_ShouldAllowMetadataAddition()
    {
        // Arrange
        var result = ProbeResult.CreateSuccess(_testHost, ProbeType.CqlQuery, TimeSpan.FromMilliseconds(200));

        // Act
        result.Metadata["RowCount"] = 42;
        result.Metadata["QueryType"] = "SELECT";
        result.Metadata["TracingEnabled"] = true;

        // Assert
        result.Metadata.Should().HaveCount(3);
        result.Metadata["RowCount"].Should().Be(42);
        result.Metadata["QueryType"].Should().Be("SELECT");
        result.Metadata["TracingEnabled"].Should().Be(true);
    }

    [Theory]
    [InlineData(ProbeType.Socket)]
    [InlineData(ProbeType.Ping)]
    [InlineData(ProbeType.CqlQuery)]
    [InlineData(ProbeType.NativePort)]
    [InlineData(ProbeType.StoragePort)]
    public void ProbeResult_ShouldHandleAllProbeTypes(ProbeType probeType)
    {
        // Act
        var result = ProbeResult.CreateSuccess(_testHost, probeType, TimeSpan.FromMilliseconds(100));

        // Assert
        result.ProbeType.Should().Be(probeType);
    }
}