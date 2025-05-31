using System.Net;
using CassandraProbe.Core.Models;
using FluentAssertions;
using Xunit;

namespace CassandraProbe.Core.Tests.Models;

public class HostProbeTests
{
    [Fact]
    public void HostProbe_ShouldInitializeWithDefaults()
    {
        // Arrange & Act
        var host = new HostProbe();

        // Assert
        host.Address.Should().BeNull();
        host.HostId.Should().BeNull();
        host.Datacenter.Should().BeEmpty();
        host.Rack.Should().BeEmpty();
        host.CassandraVersion.Should().BeEmpty();
        host.NativePort.Should().Be(9042);
        host.StoragePort.Should().Be(7000);
        host.Status.Should().Be(HostStatus.Unknown);
    }

    [Fact]
    public void HostProbe_ToString_ShouldFormatCorrectly()
    {
        // Arrange
        var host = new HostProbe
        {
            Address = IPAddress.Parse("192.168.1.100"),
            NativePort = 9042,
            Datacenter = "dc1",
            Rack = "rack1",
            Status = HostStatus.Up
        };

        // Act
        var result = host.ToString();

        // Assert
        result.Should().Be("192.168.1.100:9042 (DC: dc1, Rack: rack1, Status: Up)");
    }

    [Theory]
    [InlineData("10.0.0.1", 9042, "dc1", "rack1", HostStatus.Up)]
    [InlineData("10.0.0.2", 9043, "dc2", "rack2", HostStatus.Down)]
    [InlineData("10.0.0.3", 9044, "dc3", "rack3", HostStatus.Unknown)]
    public void HostProbe_ShouldStorePropertiesCorrectly(string ip, int port, string dc, string rack, HostStatus status)
    {
        // Arrange & Act
        var host = new HostProbe
        {
            Address = IPAddress.Parse(ip),
            NativePort = port,
            Datacenter = dc,
            Rack = rack,
            Status = status,
            HostId = Guid.NewGuid().ToString(),
            CassandraVersion = "4.1.0",
            LastSeen = DateTime.UtcNow
        };

        // Assert
        host.Address.ToString().Should().Be(ip);
        host.NativePort.Should().Be(port);
        host.Datacenter.Should().Be(dc);
        host.Rack.Should().Be(rack);
        host.Status.Should().Be(status);
        host.HostId.Should().NotBeNullOrEmpty();
        host.CassandraVersion.Should().Be("4.1.0");
        host.LastSeen.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }
}