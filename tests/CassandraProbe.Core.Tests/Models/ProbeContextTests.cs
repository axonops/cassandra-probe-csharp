using System.Net;
using CassandraProbe.Core.Models;
using FluentAssertions;
using Xunit;

namespace CassandraProbe.Core.Tests.Models;

public class ProbeContextTests
{
    [Fact]
    public void ProbeContext_ShouldInitializeWithDefaults()
    {
        // Arrange & Act
        var context = new ProbeContext();

        // Assert
        context.SessionId.Should().NotBeEmpty();
        context.Hosts.Should().NotBeNull().And.BeEmpty();
        context.ProbeTypes.Should().NotBeNull().And.BeEmpty();
        context.StartTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        context.ConsistencyLevel.Should().BeNull();
        context.QueryTimeout.Should().Be(TimeSpan.FromSeconds(30));
        context.SocketTimeout.Should().Be(TimeSpan.FromSeconds(5));
        context.EnableTracing.Should().BeFalse();
        context.TestQuery.Should().Be("SELECT key FROM system.local");
        context.MaxRetries.Should().Be(3);
    }

    [Fact]
    public void ProbeContext_SessionId_ShouldBeUnique()
    {
        // Arrange & Act
        var context1 = new ProbeContext();
        var context2 = new ProbeContext();

        // Assert
        context1.SessionId.Should().NotBe(context2.SessionId);
    }

    [Fact]
    public void ProbeContext_ShouldAllowCustomization()
    {
        // Arrange
        var hosts = new List<HostProbe>
        {
            new HostProbe { Address = IPAddress.Parse("10.0.0.1") },
            new HostProbe { Address = IPAddress.Parse("10.0.0.2") }
        };

        var probeTypes = new List<ProbeType> { ProbeType.Socket, ProbeType.CqlQuery };

        // Act
        var context = new ProbeContext
        {
            Hosts = hosts,
            ProbeTypes = probeTypes,
            ConsistencyLevel = "LOCAL_ONE",
            QueryTimeout = TimeSpan.FromSeconds(60),
            SocketTimeout = TimeSpan.FromSeconds(10),
            EnableTracing = true,
            TestQuery = "SELECT * FROM custom_table",
            MaxRetries = 5
        };

        // Assert
        context.Hosts.Should().BeEquivalentTo(hosts);
        context.ProbeTypes.Should().BeEquivalentTo(probeTypes);
        context.ConsistencyLevel.Should().Be("LOCAL_ONE");
        context.QueryTimeout.Should().Be(TimeSpan.FromSeconds(60));
        context.SocketTimeout.Should().Be(TimeSpan.FromSeconds(10));
        context.EnableTracing.Should().BeTrue();
        context.TestQuery.Should().Be("SELECT * FROM custom_table");
        context.MaxRetries.Should().Be(5);
    }

    [Theory]
    [InlineData("ALL", 45)]
    [InlineData("QUORUM", 30)]
    [InlineData("LOCAL_ONE", 15)]
    [InlineData("ONE", 10)]
    public void ProbeContext_ShouldHandleVariousConfigurations(string consistencyLevel, int timeoutSeconds)
    {
        // Arrange & Act
        var context = new ProbeContext
        {
            ConsistencyLevel = consistencyLevel,
            QueryTimeout = TimeSpan.FromSeconds(timeoutSeconds)
        };

        // Assert
        context.ConsistencyLevel.Should().Be(consistencyLevel);
        context.QueryTimeout.TotalSeconds.Should().Be(timeoutSeconds);
    }

    [Fact]
    public void ProbeContext_ShouldHandleAllProbeTypes()
    {
        // Arrange
        var allProbeTypes = Enum.GetValues<ProbeType>().ToList();

        // Act
        var context = new ProbeContext
        {
            ProbeTypes = allProbeTypes
        };

        // Assert
        context.ProbeTypes.Should().HaveCount(5);
        context.ProbeTypes.Should().Contain(ProbeType.Socket);
        context.ProbeTypes.Should().Contain(ProbeType.Ping);
        context.ProbeTypes.Should().Contain(ProbeType.CqlQuery);
        context.ProbeTypes.Should().Contain(ProbeType.NativePort);
        context.ProbeTypes.Should().Contain(ProbeType.StoragePort);
    }

    [Fact]
    public void ProbeContext_ShouldMaintainHostListIntegrity()
    {
        // Arrange
        var context = new ProbeContext();
        var host1 = new HostProbe { Address = IPAddress.Parse("10.0.0.1") };
        var host2 = new HostProbe { Address = IPAddress.Parse("10.0.0.2") };

        // Act
        context.Hosts.Add(host1);
        context.Hosts.Add(host2);

        // Assert
        context.Hosts.Should().HaveCount(2);
        context.Hosts.Should().Contain(host1);
        context.Hosts.Should().Contain(host2);
        context.Hosts.First().Should().BeSameAs(host1);
        context.Hosts.Last().Should().BeSameAs(host2);
    }
}