using System.Net;
using System.Net.NetworkInformation;
using CassandraProbe.Actions;
using CassandraProbe.Actions.Interfaces;
using CassandraProbe.Core.Configuration;
using CassandraProbe.Core.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CassandraProbe.Actions.Tests;

public class PingProbeTests
{
    private readonly Mock<ILogger<PingProbe>> _loggerMock;
    private readonly Mock<IPingProvider> _pingProviderMock;
    private readonly PingProbe _probe;

    public PingProbeTests()
    {
        _loggerMock = new Mock<ILogger<PingProbe>>();
        _pingProviderMock = new Mock<IPingProvider>();
        _probe = new PingProbe(_loggerMock.Object, _pingProviderMock.Object);
    }

    private static PingReply CreateSuccessfulPingReply(long roundTripTime = 1)
    {
        // Use reflection to create PingReply since it has no public constructor
        // Constructor: (IPAddress address, PingOptions options, IPStatus ipStatus, Int64 rtt, Byte[] buffer)
        var constructor = typeof(PingReply).GetConstructors(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)[0];
        return (PingReply)constructor.Invoke(new object[] { 
            IPAddress.Loopback, // address
            new PingOptions(), // options
            IPStatus.Success, // status
            roundTripTime, // roundtrip time
            new byte[0] // buffer
        });
    }

    private static PingReply CreateFailedPingReply(IPStatus status = IPStatus.TimedOut)
    {
        var constructor = typeof(PingReply).GetConstructors(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)[0];
        return (PingReply)constructor.Invoke(new object[] { 
            IPAddress.Loopback, // address
            new PingOptions(), // options
            status, // status
            0L, // roundtrip time
            new byte[0] // buffer
        });
    }

    [Fact]
    public void ProbeType_ShouldReturnPing()
    {
        // Assert
        _probe.Type.Should().Be(ProbeType.Ping);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldSucceedForLoopback()
    {
        // Arrange
        var host = new HostProbe
        {
            Address = IPAddress.Loopback,
            NativePort = 9042
        };

        var context = new ProbeContext
        {
            SocketTimeout = TimeSpan.FromSeconds(5)
        };

        var successfulReply = CreateSuccessfulPingReply(25);
        _pingProviderMock.Setup(x => x.SendPingAsync(host.Address, 5000))
                        .ReturnsAsync(successfulReply);

        // Act
        var result = await _probe.ExecuteAsync(host, context);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Host.Should().BeSameAs(host);
        result.ProbeType.Should().Be(ProbeType.Ping);
        result.Duration.Should().BeGreaterThan(TimeSpan.Zero);
        result.ErrorMessage.Should().BeNull();
        result.Metadata.Should().ContainKey("RoundTripTime");
        result.Metadata["RoundTripTime"].Should().Be(25L);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldFailForUnreachableHost()
    {
        // Arrange
        var host = new HostProbe
        {
            Address = IPAddress.Parse("192.168.255.255"), // Non-routable address
            NativePort = 9042
        };

        var context = new ProbeContext
        {
            SocketTimeout = TimeSpan.FromSeconds(1)
        };

        var failedReply = CreateFailedPingReply(IPStatus.TimedOut);
        _pingProviderMock.Setup(x => x.SendPingAsync(host.Address, 1000))
                        .ReturnsAsync(failedReply);

        // Act
        var result = await _probe.ExecuteAsync(host, context);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Host.Should().BeSameAs(host);
        result.ProbeType.Should().Be(ProbeType.Ping);
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRespectTimeout()
    {
        // Arrange
        var host = new HostProbe
        {
            Address = IPAddress.Parse("192.168.255.255"), // Non-routable address
            NativePort = 9042
        };

        var context = new ProbeContext
        {
            SocketTimeout = TimeSpan.FromMilliseconds(100)
        };

        var failedReply = CreateFailedPingReply(IPStatus.TimedOut);
        _pingProviderMock.Setup(x => x.SendPingAsync(host.Address, 100))
                        .ReturnsAsync(failedReply);

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await _probe.ExecuteAsync(host, context);
        stopwatch.Stop();

        // Assert
        result.Success.Should().BeFalse();
        // The actual ping call is mocked, so the timing should be much faster than the configured timeout
        // Allow more time in CI environments
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(500);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldLogPingAttempt()
    {
        // Arrange
        var host = new HostProbe
        {
            Address = IPAddress.Loopback
        };

        var context = new ProbeContext();

        var successfulReply = CreateSuccessfulPingReply(25);
        _pingProviderMock.Setup(x => x.SendPingAsync(host.Address, 5000))
                        .ReturnsAsync(successfulReply);

        // Act
        await _probe.ExecuteAsync(host, context);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                It.Is<LogLevel>(l => l == LogLevel.Debug),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Ping probe")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("::1")] // IPv6 loopback
    public async Task ExecuteAsync_ShouldHandleVariousAddresses(string ipAddress)
    {
        // Arrange
        var host = new HostProbe
        {
            Address = IPAddress.Parse(ipAddress)
        };

        var context = new ProbeContext();

        var successfulReply = CreateSuccessfulPingReply(25);
        _pingProviderMock.Setup(x => x.SendPingAsync(host.Address, 5000))
                        .ReturnsAsync(successfulReply);

        // Act
        var result = await _probe.ExecuteAsync(host, context);

        // Assert
        result.Should().NotBeNull();
        result.Host.Should().BeSameAs(host);
        result.ProbeType.Should().Be(ProbeType.Ping);
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldHandleExceptionGracefully()
    {
        // This test simulates an exception scenario
        // In real scenarios, Ping might throw PingException or other network-related exceptions
        
        // Arrange
        var host = new HostProbe
        {
            Address = null! // This will cause an exception
        };

        var context = new ProbeContext();

        // Act
        var result = await _probe.ExecuteAsync(host, context);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReportPingStatistics()
    {
        // Arrange
        var host = new HostProbe
        {
            Address = IPAddress.Loopback
        };

        var context = new ProbeContext();

        var successfulReply = CreateSuccessfulPingReply(25);
        _pingProviderMock.Setup(x => x.SendPingAsync(host.Address, 5000))
                        .ReturnsAsync(successfulReply);

        // Act
        var result = await _probe.ExecuteAsync(host, context);

        // Assert
        result.Success.Should().BeTrue();
        result.Duration.Should().BeGreaterThan(TimeSpan.Zero);
        result.Duration.Should().BeLessThan(TimeSpan.FromSeconds(1)); // Should be fast since mocked
        result.Metadata.Should().ContainKey("RoundTripTime");
        result.Metadata["RoundTripTime"].Should().Be(25L);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldHandleMultipleConcurrentPings()
    {
        // Arrange
        var hosts = Enumerable.Range(1, 5).Select(_ => new HostProbe
        {
            Address = IPAddress.Loopback
        }).ToList();

        var context = new ProbeContext();

        var successfulReply = CreateSuccessfulPingReply(25);
        _pingProviderMock.Setup(x => x.SendPingAsync(It.IsAny<IPAddress>(), 5000))
                        .ReturnsAsync(successfulReply);

        // Act
        var tasks = hosts.Select(h => _probe.ExecuteAsync(h, context));
        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().HaveCount(5);
        results.Should().OnlyContain(r => r.Success);
        results.Select(r => r.Duration).Should().OnlyContain(d => d > TimeSpan.Zero);
    }
}