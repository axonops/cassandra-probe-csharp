using System.Net;
using CassandraProbe.Actions.PortSpecificProbes;
using CassandraProbe.Core.Configuration;
using CassandraProbe.Core.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CassandraProbe.Actions.Tests;

public class StoragePortProbeTests
{
    private readonly Mock<ILogger<StoragePortProbe>> _loggerMock;
    private readonly StoragePortProbe _probe;

    public StoragePortProbeTests()
    {
        _loggerMock = new Mock<ILogger<StoragePortProbe>>();
        _probe = new StoragePortProbe(_loggerMock.Object);
    }

    [Fact]
    public void ProbeType_ShouldReturnStoragePort()
    {
        // Assert
        _probe.Type.Should().Be(ProbeType.StoragePort);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldProbeStoragePort()
    {
        // Arrange
        var host = new HostProbe
        {
            Address = IPAddress.Parse("10.0.0.1"),
            StoragePort = 7000
        };

        var context = new ProbeContext
        {
            SocketTimeout = TimeSpan.FromSeconds(5)
        };

        // Act
        var result = await _probe.ExecuteAsync(host, context);

        // Assert
        result.Should().NotBeNull();
        result.Host.Should().BeSameAs(host);
        result.ProbeType.Should().Be(ProbeType.StoragePort);
        result.Duration.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldLogStoragePortDetails()
    {
        // Arrange
        var host = new HostProbe
        {
            Address = IPAddress.Parse("10.0.0.1"),
            StoragePort = 7001
        };

        var context = new ProbeContext();

        // Act
        await _probe.ExecuteAsync(host, context);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                It.Is<LogLevel>(l => l == LogLevel.Debug),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => 
                    v.ToString()!.Contains("Storage port probe") &&
                    v.ToString()!.Contains("7001")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Theory]
    [InlineData(7000)]
    [InlineData(7001)]
    [InlineData(17000)]
    public async Task ExecuteAsync_ShouldHandleVariousStoragePorts(int port)
    {
        // Arrange
        var host = new HostProbe
        {
            Address = IPAddress.Loopback,
            StoragePort = port
        };

        var context = new ProbeContext
        {
            SocketTimeout = TimeSpan.FromMilliseconds(100)
        };

        // Act
        var result = await _probe.ExecuteAsync(host, context);

        // Assert
        result.Should().NotBeNull();
        result.ProbeType.Should().Be(ProbeType.StoragePort);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldHandleConnectionTimeout()
    {
        // Arrange
        var host = new HostProbe
        {
            Address = IPAddress.Parse("192.168.255.255"), // Non-routable
            StoragePort = 7000
        };

        var context = new ProbeContext
        {
            SocketTimeout = TimeSpan.FromMilliseconds(50)
        };

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await _probe.ExecuteAsync(host, context);
        stopwatch.Stop();

        // Assert
        result.Success.Should().BeFalse();
        // Allow more time in CI environments
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldIncludePortInMetadata()
    {
        // Arrange
        var host = new HostProbe
        {
            Address = IPAddress.Loopback,
            StoragePort = 7000
        };

        var context = new ProbeContext();

        // Act
        var result = await _probe.ExecuteAsync(host, context);

        // Assert
        result.Metadata.Should().ContainKey("Port");
        result.Metadata["Port"].Should().Be(7000);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldHandleSecureStoragePort()
    {
        // Arrange
        var host = new HostProbe
        {
            Address = IPAddress.Parse("10.0.0.1"),
            StoragePort = 7001 // Secure inter-node port
        };

        var context = new ProbeContext();

        // Act
        var result = await _probe.ExecuteAsync(host, context);

        // Assert
        result.Should().NotBeNull();
        result.Metadata.Should().ContainKey("PortType");
        result.Metadata["PortType"].Should().Be("SecureStorage");
    }
}