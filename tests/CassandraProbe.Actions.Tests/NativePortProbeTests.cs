using System.Net;
using CassandraProbe.Actions.PortSpecificProbes;
using CassandraProbe.Core.Configuration;
using CassandraProbe.Core.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CassandraProbe.Actions.Tests;

public class NativePortProbeTests
{
    private readonly Mock<ILogger<NativePortProbe>> _loggerMock;
    private readonly NativePortProbe _probe;

    public NativePortProbeTests()
    {
        _loggerMock = new Mock<ILogger<NativePortProbe>>();
        _probe = new NativePortProbe(_loggerMock.Object);
    }

    [Fact]
    public void ProbeType_ShouldReturnNativePort()
    {
        // Assert
        _probe.Type.Should().Be(ProbeType.NativePort);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldProbeNativePort()
    {
        // Arrange
        var host = new HostProbe
        {
            Address = IPAddress.Parse("10.0.0.1"),
            NativePort = 9042
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
        result.ProbeType.Should().Be(ProbeType.NativePort);
        result.Duration.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldLogPortDetails()
    {
        // Arrange
        var host = new HostProbe
        {
            Address = IPAddress.Parse("10.0.0.1"),
            NativePort = 19042
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
                    v.ToString()!.Contains("Native port probe") &&
                    v.ToString()!.Contains("19042")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Theory]
    [InlineData(9042)]
    [InlineData(19042)]
    [InlineData(29042)]
    public async Task ExecuteAsync_ShouldHandleVariousPorts(int port)
    {
        // Arrange
        var host = new HostProbe
        {
            Address = IPAddress.Loopback,
            NativePort = port
        };

        var context = new ProbeContext
        {
            SocketTimeout = TimeSpan.FromMilliseconds(100)
        };

        // Act
        var result = await _probe.ExecuteAsync(host, context);

        // Assert
        result.Should().NotBeNull();
        result.ProbeType.Should().Be(ProbeType.NativePort);
        // Port should be included in metadata or result
    }

    [Fact]
    public async Task ExecuteAsync_ShouldHandleConnectionTimeout()
    {
        // Arrange
        var host = new HostProbe
        {
            Address = IPAddress.Parse("192.168.255.255"), // Non-routable
            NativePort = 9042
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
            NativePort = 9042
        };

        var context = new ProbeContext();

        // Act
        var result = await _probe.ExecuteAsync(host, context);

        // Assert
        result.Metadata.Should().ContainKey("Port");
        result.Metadata["Port"].Should().Be(9042);
    }
}