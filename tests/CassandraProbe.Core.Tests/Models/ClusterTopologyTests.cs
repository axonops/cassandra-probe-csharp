using System.Net;
using CassandraProbe.Core.Models;
using FluentAssertions;
using Xunit;

namespace CassandraProbe.Core.Tests.Models;

public class ClusterTopologyTests
{
    [Fact]
    public void ClusterTopology_ShouldInitializeWithDefaults()
    {
        // Arrange & Act
        var topology = new ClusterTopology();

        // Assert
        topology.ClusterName.Should().BeEmpty();
        topology.Hosts.Should().NotBeNull().And.BeEmpty();
        topology.Datacenters.Should().NotBeNull().And.BeEmpty();
        topology.DiscoveredAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void ClusterTopology_ShouldCalculateHostCounts()
    {
        // Arrange
        var topology = new ClusterTopology
        {
            ClusterName = "TestCluster",
            Hosts = new List<HostProbe>
            {
                new HostProbe { Address = IPAddress.Parse("10.0.0.1"), Status = HostStatus.Up },
                new HostProbe { Address = IPAddress.Parse("10.0.0.2"), Status = HostStatus.Up },
                new HostProbe { Address = IPAddress.Parse("10.0.0.3"), Status = HostStatus.Down },
                new HostProbe { Address = IPAddress.Parse("10.0.0.4"), Status = HostStatus.Unknown }
            }
        };

        // Act & Assert
        topology.TotalHosts.Should().Be(4);
        topology.UpHosts.Should().Be(2);
        topology.DownHosts.Should().Be(1);
    }

    [Fact]
    public void ClusterTopology_ShouldHandleEmptyHostList()
    {
        // Arrange
        var topology = new ClusterTopology
        {
            ClusterName = "EmptyCluster",
            Hosts = new List<HostProbe>()
        };

        // Act & Assert
        topology.TotalHosts.Should().Be(0);
        topology.UpHosts.Should().Be(0);
        topology.DownHosts.Should().Be(0);
    }

    [Fact]
    public void ClusterTopology_ShouldPopulateDatacentersCorrectly()
    {
        // Arrange
        var topology = new ClusterTopology
        {
            ClusterName = "MultiDCCluster",
            Hosts = new List<HostProbe>
            {
                new HostProbe { Address = IPAddress.Parse("10.0.0.1"), Datacenter = "dc1", Rack = "rack1" },
                new HostProbe { Address = IPAddress.Parse("10.0.0.2"), Datacenter = "dc1", Rack = "rack2" },
                new HostProbe { Address = IPAddress.Parse("10.0.0.3"), Datacenter = "dc2", Rack = "rack1" }
            },
            Datacenters = new List<string> { "dc1", "dc2" }
        };

        // Act & Assert
        topology.Datacenters.Should().HaveCount(2);
        topology.Datacenters.Should().Contain(new[] { "dc1", "dc2" });
    }

    [Theory]
    [InlineData("Production Cluster", 10, 8, 2)]
    [InlineData("Test Cluster", 3, 3, 0)]
    [InlineData("Maintenance Cluster", 5, 0, 5)]
    public void ClusterTopology_ShouldHandleVariousClusterStates(string clusterName, int total, int up, int down)
    {
        // Arrange
        var hosts = new List<HostProbe>();
        
        for (int i = 0; i < up; i++)
        {
            hosts.Add(new HostProbe 
            { 
                Address = IPAddress.Parse($"10.0.0.{i + 1}"), 
                Status = HostStatus.Up 
            });
        }
        
        for (int i = 0; i < down; i++)
        {
            hosts.Add(new HostProbe 
            { 
                Address = IPAddress.Parse($"10.0.1.{i + 1}"), 
                Status = HostStatus.Down 
            });
        }
        
        for (int i = 0; i < (total - up - down); i++)
        {
            hosts.Add(new HostProbe 
            { 
                Address = IPAddress.Parse($"10.0.2.{i + 1}"), 
                Status = HostStatus.Unknown 
            });
        }

        var topology = new ClusterTopology
        {
            ClusterName = clusterName,
            Hosts = hosts
        };

        // Act & Assert
        topology.ClusterName.Should().Be(clusterName);
        topology.TotalHosts.Should().Be(total);
        topology.UpHosts.Should().Be(up);
        topology.DownHosts.Should().Be(down);
    }
}