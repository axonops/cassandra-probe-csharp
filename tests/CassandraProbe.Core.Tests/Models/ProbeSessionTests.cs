using System.Net;
using CassandraProbe.Core.Models;
using FluentAssertions;
using Xunit;

namespace CassandraProbe.Core.Tests.Models;

public class ProbeSessionTests
{
    [Fact]
    public void ProbeSession_ShouldInitializeWithDefaults()
    {
        // Arrange & Act
        var session = new ProbeSession();

        // Assert
        session.Id.Should().NotBeEmpty();
        session.StartTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        session.EndTime.Should().BeNull();
        session.Results.Should().NotBeNull().And.BeEmpty();
        session.Topology.Should().BeNull();
        session.Duration.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void ProbeSession_ShouldCalculateDurationWhenEndTimeSet()
    {
        // Arrange
        var session = new ProbeSession();
        var startTime = DateTime.UtcNow.AddMinutes(-5);
        var endTime = DateTime.UtcNow;
        
        // Use reflection to set StartTime as it's init-only
        typeof(ProbeSession).GetProperty(nameof(ProbeSession.StartTime))!
            .SetValue(session, startTime);

        // Act
        session.EndTime = endTime;

        // Assert
        session.Duration.Should().BeCloseTo(TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void ProbeSession_ShouldReturnZeroDurationWhenEndTimeNotSet()
    {
        // Arrange & Act
        var session = new ProbeSession();

        // Assert
        session.EndTime.Should().BeNull();
        session.Duration.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void ProbeSession_ShouldAccumulateResults()
    {
        // Arrange
        var session = new ProbeSession();
        var host = new HostProbe { Address = IPAddress.Parse("10.0.0.1") };
        
        var result1 = ProbeResult.CreateSuccess(host, ProbeType.Socket, TimeSpan.FromMilliseconds(50));
        var result2 = ProbeResult.CreateFailure(host, ProbeType.Ping, "Timeout", TimeSpan.FromMilliseconds(100));
        var result3 = ProbeResult.CreateSuccess(host, ProbeType.CqlQuery, TimeSpan.FromMilliseconds(75));

        // Act
        session.Results.Add(result1);
        session.Results.Add(result2);
        session.Results.Add(result3);

        // Assert
        session.Results.Should().HaveCount(3);
        session.Results.Count(r => r.Success).Should().Be(2);
        session.Results.Count(r => !r.Success).Should().Be(1);
    }

    [Fact]
    public void ProbeSession_ShouldAssociateWithTopology()
    {
        // Arrange
        var session = new ProbeSession();
        var topology = new ClusterTopology
        {
            ClusterName = "TestCluster",
            Hosts = new List<HostProbe>
            {
                new HostProbe { Address = IPAddress.Parse("10.0.0.1"), Status = HostStatus.Up },
                new HostProbe { Address = IPAddress.Parse("10.0.0.2"), Status = HostStatus.Down }
            },
            Datacenters = new List<string> { "dc1" }
        };

        // Act
        session.Topology = topology;

        // Assert
        session.Topology.Should().NotBeNull();
        session.Topology.ClusterName.Should().Be("TestCluster");
        session.Topology.TotalHosts.Should().Be(2);
        session.Topology.UpHosts.Should().Be(1);
        session.Topology.DownHosts.Should().Be(1);
    }

    [Fact]
    public void ProbeSession_ShouldHandleCompleteSession()
    {
        // Arrange
        var session = new ProbeSession();
        var host1 = new HostProbe { Address = IPAddress.Parse("10.0.0.1"), Datacenter = "dc1" };
        var host2 = new HostProbe { Address = IPAddress.Parse("10.0.0.2"), Datacenter = "dc1" };

        session.Topology = new ClusterTopology
        {
            ClusterName = "ProductionCluster",
            Hosts = new List<HostProbe> { host1, host2 }
        };

        // Add results for multiple probe types
        foreach (var probeType in new[] { ProbeType.Socket, ProbeType.CqlQuery })
        {
            session.Results.Add(ProbeResult.CreateSuccess(host1, probeType, TimeSpan.FromMilliseconds(50)));
            session.Results.Add(ProbeResult.CreateSuccess(host2, probeType, TimeSpan.FromMilliseconds(60)));
        }

        // Act
        session.EndTime = session.StartTime.AddSeconds(10);

        // Assert
        session.Results.Should().HaveCount(4);
        session.Results.Should().OnlyContain(r => r.Success);
        session.Duration.Should().BeCloseTo(TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(100));
        session.Topology.Should().NotBeNull();
        session.Topology!.TotalHosts.Should().Be(2);
    }

    [Fact]
    public void ProbeSession_Id_ShouldBeUnique()
    {
        // Arrange & Act
        var session1 = new ProbeSession();
        var session2 = new ProbeSession();

        // Assert
        session1.Id.Should().NotBe(session2.Id);
        Guid.TryParse(session1.Id, out _).Should().BeTrue();
        Guid.TryParse(session2.Id, out _).Should().BeTrue();
    }
}