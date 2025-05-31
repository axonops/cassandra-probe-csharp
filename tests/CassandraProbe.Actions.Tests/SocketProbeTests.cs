using System.Net;
using System.Net.Sockets;
using CassandraProbe.Actions;
using CassandraProbe.Core.Configuration;
using CassandraProbe.Core.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CassandraProbe.Actions.Tests;

public class SocketProbeTests : IDisposable
{
    private readonly Mock<ILogger<SocketProbe>> _loggerMock;
    private readonly SocketProbe _probe;
    private TcpListener? _listener;

    public SocketProbeTests()
    {
        _loggerMock = new Mock<ILogger<SocketProbe>>();
        _probe = new SocketProbe(_loggerMock.Object);
    }

    public void Dispose()
    {
        _listener?.Stop();
    }

    [Fact]
    public void ProbeType_ShouldReturnSocket()
    {
        // Assert
        _probe.Type.Should().Be(ProbeType.Socket);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldSucceedWhenPortIsOpen()
    {
        // Arrange
        var port = GetAvailablePort();
        _listener = new TcpListener(IPAddress.Loopback, port);
        _listener.Start();

        var host = new HostProbe
        {
            Address = IPAddress.Loopback,
            NativePort = port
        };

        var context = new ProbeContext
        {
            SocketTimeout = TimeSpan.FromSeconds(5)
        };

        // Act
        var result = await _probe.ExecuteAsync(host, context);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Host.Should().BeSameAs(host);
        result.ProbeType.Should().Be(ProbeType.Socket);
        result.Duration.Should().BeGreaterThan(TimeSpan.Zero);
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldFailWhenPortIsClosed()
    {
        // Arrange
        var host = new HostProbe
        {
            Address = IPAddress.Loopback,
            NativePort = 65432 // Unlikely to be in use
        };

        var context = new ProbeContext
        {
            SocketTimeout = TimeSpan.FromSeconds(1)
        };

        // Act
        var result = await _probe.ExecuteAsync(host, context);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Host.Should().BeSameAs(host);
        result.ProbeType.Should().Be(ProbeType.Socket);
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

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await _probe.ExecuteAsync(host, context);
        stopwatch.Stop();

        // Assert
        result.Success.Should().BeFalse();
        // Allow more time in CI environments
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(2000);
        result.ErrorMessage.Should().ContainAny("timed out", "timeout");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldLogConnectionAttempt()
    {
        // Arrange
        var host = new HostProbe
        {
            Address = IPAddress.Loopback,
            NativePort = 65432
        };

        var context = new ProbeContext();

        // Act
        await _probe.ExecuteAsync(host, context);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                It.Is<LogLevel>(l => l == LogLevel.Debug),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Socket probe")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Theory]
    [InlineData("10.0.0.1", 9042)]
    [InlineData("::1", 9042)] // IPv6 loopback
    [InlineData("127.0.0.1", 19042)]
    public async Task ExecuteAsync_ShouldHandleVariousAddresses(string ipAddress, int port)
    {
        // Arrange
        var host = new HostProbe
        {
            Address = IPAddress.Parse(ipAddress),
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
        result.Host.Should().BeSameAs(host);
        result.ProbeType.Should().Be(ProbeType.Socket);
        // Result may succeed or fail depending on local environment
    }

    [Fact]
    public async Task ExecuteAsync_ShouldHandleMultipleConcurrentProbes()
    {
        // Arrange
        var port = GetAvailablePort();
        _listener = new TcpListener(IPAddress.Loopback, port);
        _listener.Start();

        var hosts = Enumerable.Range(1, 5).Select(_ => new HostProbe
        {
            Address = IPAddress.Loopback,
            NativePort = port
        }).ToList();

        var context = new ProbeContext();

        // Act
        var tasks = hosts.Select(h => _probe.ExecuteAsync(h, context));
        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().HaveCount(5);
        results.Should().OnlyContain(r => r.Success);
    }

    private static int GetAvailablePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}