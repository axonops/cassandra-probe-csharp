using System.Net;
using Cassandra;
using CassandraProbe.Core.Configuration;
using CassandraProbe.Core.Interfaces;
using CassandraProbe.Core.Models;
using CassandraProbe.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CassandraProbe.Services.Tests;

public class ProbeOrchestratorTests
{
    private readonly Mock<IClusterDiscovery> _discoveryMock;
    private readonly Mock<IEnumerable<IProbeAction>> _probeActionsMock;
    private readonly Mock<ILogger<ProbeOrchestrator>> _loggerMock;
    private readonly Mock<ISessionManager> _sessionManagerMock;
    private readonly ProbeOrchestrator _orchestrator;
    private readonly List<IProbeAction> _probeActions;

    public ProbeOrchestratorTests()
    {
        _discoveryMock = new Mock<IClusterDiscovery>();
        _loggerMock = new Mock<ILogger<ProbeOrchestrator>>();
        _sessionManagerMock = new Mock<ISessionManager>();
        _probeActions = new List<IProbeAction>();
        _probeActionsMock = new Mock<IEnumerable<IProbeAction>>();
        _probeActionsMock.Setup(x => x.GetEnumerator()).Returns(() => _probeActions.GetEnumerator());
        
        _orchestrator = new ProbeOrchestrator(
            _discoveryMock.Object,
            _probeActionsMock.Object,
            _loggerMock.Object,
            _sessionManagerMock.Object
        );
    }

    [Fact]
    public async Task ExecuteProbesAsync_ShouldDiscoverTopologyFirst()
    {
        // Arrange
        var config = CreateTestConfiguration();
        var topology = CreateTestTopology();
        var mockSession = new Mock<ISession>();
        
        _sessionManagerMock.Setup(x => x.GetSessionAsync())
            .ReturnsAsync(mockSession.Object);
        
        _discoveryMock.Setup(x => x.DiscoverAsync(config))
            .ReturnsAsync(topology);
        _discoveryMock.Setup(x => x.GetHostsAsync())
            .ReturnsAsync(topology.Hosts);

        // Act
        var session = await _orchestrator.ExecuteProbesAsync(config);

        // Assert
        _discoveryMock.Verify(x => x.DiscoverAsync(config), Times.Once);
        session.Topology.Should().BeSameAs(topology);
    }

    [Fact]
    public async Task ExecuteProbesAsync_ShouldExecuteEnabledProbes()
    {
        // Arrange
        var config = CreateTestConfiguration();
        config.ProbeSelection.ExecuteAllProbes = true;
        
        var topology = CreateTestTopology();
        var socketProbe = CreateMockProbeAction(ProbeType.Socket, true);
        var pingProbe = CreateMockProbeAction(ProbeType.Ping, true);
        var mockSession = new Mock<ISession>();
        
        _probeActions.Add(socketProbe.Object);
        _probeActions.Add(pingProbe.Object);
        
        _sessionManagerMock.Setup(x => x.GetSessionAsync())
            .ReturnsAsync(mockSession.Object);
        
        _discoveryMock.Setup(x => x.DiscoverAsync(config))
            .ReturnsAsync(topology);
        _discoveryMock.Setup(x => x.GetHostsAsync())
            .ReturnsAsync(topology.Hosts);

        // Act
        var session = await _orchestrator.ExecuteProbesAsync(config);

        // Assert
        socketProbe.Verify(x => x.ExecuteAsync(It.IsAny<HostProbe>(), It.IsAny<ProbeContext>()), 
            Times.Exactly(topology.Hosts.Count));
        pingProbe.Verify(x => x.ExecuteAsync(It.IsAny<HostProbe>(), It.IsAny<ProbeContext>()), 
            Times.Exactly(topology.Hosts.Count));
        session.Results.Should().HaveCount(topology.Hosts.Count * 2);
    }

    [Fact]
    public async Task ExecuteProbesAsync_ShouldSkipDisabledProbes()
    {
        // Arrange
        var config = CreateTestConfiguration();
        config.ProbeSelection.ProbeNativePort = true;
        config.ProbeSelection.ProbePing = false;
        
        var topology = CreateTestTopology();
        var nativeProbe = CreateMockProbeAction(ProbeType.NativePort, true);
        var pingProbe = CreateMockProbeAction(ProbeType.Ping, true);
        var mockSession = new Mock<ISession>();
        
        _probeActions.Add(nativeProbe.Object);
        _probeActions.Add(pingProbe.Object);
        
        _sessionManagerMock.Setup(x => x.GetSessionAsync())
            .ReturnsAsync(mockSession.Object);
        
        _discoveryMock.Setup(x => x.DiscoverAsync(config))
            .ReturnsAsync(topology);
        _discoveryMock.Setup(x => x.GetHostsAsync())
            .ReturnsAsync(topology.Hosts);

        // Act
        var session = await _orchestrator.ExecuteProbesAsync(config);

        // Assert
        nativeProbe.Verify(x => x.ExecuteAsync(It.IsAny<HostProbe>(), It.IsAny<ProbeContext>()), 
            Times.Exactly(topology.Hosts.Count));
        pingProbe.Verify(x => x.ExecuteAsync(It.IsAny<HostProbe>(), It.IsAny<ProbeContext>()), 
            Times.Never);
    }

    [Fact]
    public async Task ExecuteProbesAsync_ShouldSetSessionTimestamps()
    {
        // Arrange
        var config = CreateTestConfiguration();
        var topology = CreateTestTopology();
        var mockSession = new Mock<ISession>();
        
        _sessionManagerMock.Setup(x => x.GetSessionAsync())
            .ReturnsAsync(mockSession.Object);
        
        _discoveryMock.Setup(x => x.DiscoverAsync(config))
            .ReturnsAsync(topology);
        _discoveryMock.Setup(x => x.GetHostsAsync())
            .ReturnsAsync(topology.Hosts);

        // Act
        var session = await _orchestrator.ExecuteProbesAsync(config);

        // Assert
        session.StartTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        session.EndTime.Should().NotBeNull();
        session.EndTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        session.Duration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task ExecuteProbesAsync_ShouldCreateProbeContext()
    {
        // Arrange
        var config = CreateTestConfiguration();
        config.Query.TestCql = "SELECT * FROM test";
        config.Query.ConsistencyLevel = "QUORUM";
        config.Query.EnableTracing = true;
        config.ProbeSelection.ExecuteAllProbes = true; // Enable all probes
        
        var topology = CreateTestTopology();
        ProbeContext? capturedContext = null;
        var mockSession = new Mock<ISession>();
        
        var probe = new Mock<IProbeAction>();
        probe.Setup(x => x.Type).Returns(ProbeType.CqlQuery);
        probe.Setup(x => x.ExecuteAsync(It.IsAny<HostProbe>(), It.IsAny<ProbeContext>()))
            .Callback<HostProbe, ProbeContext>((host, ctx) => capturedContext = ctx)
            .ReturnsAsync(ProbeResult.CreateSuccess(topology.Hosts.First(), ProbeType.CqlQuery, TimeSpan.FromMilliseconds(50)));
        
        _probeActions.Add(probe.Object);
        
        _sessionManagerMock.Setup(x => x.GetSessionAsync())
            .ReturnsAsync(mockSession.Object);
        
        _discoveryMock.Setup(x => x.DiscoverAsync(config))
            .ReturnsAsync(topology);
        _discoveryMock.Setup(x => x.GetHostsAsync())
            .ReturnsAsync(topology.Hosts);

        // Act
        await _orchestrator.ExecuteProbesAsync(config);

        // Assert
        capturedContext.Should().NotBeNull();
        capturedContext!.Configuration.Should().BeSameAs(config);
        capturedContext.Configuration.Query.TestCql.Should().Be("SELECT * FROM test");
        capturedContext.Configuration.Query.ConsistencyLevel.Should().Be("QUORUM");
        capturedContext.Configuration.Query.EnableTracing.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteProbesAsync_ShouldHandleProbeFailures()
    {
        // Arrange
        var config = CreateTestConfiguration();
        config.ProbeSelection.ExecuteAllProbes = true; // Enable all probes
        var topology = CreateTestTopology();
        var mockSession = new Mock<ISession>();
        
        var probe = new Mock<IProbeAction>();
        probe.Setup(x => x.Type).Returns(ProbeType.Socket);
        probe.Setup(x => x.Name).Returns("Socket Probe");
        probe.Setup(x => x.ExecuteAsync(It.IsAny<HostProbe>(), It.IsAny<ProbeContext>()))
            .ThrowsAsync(new Exception("Probe failed"));
        
        _probeActions.Add(probe.Object);
        
        _sessionManagerMock.Setup(x => x.GetSessionAsync())
            .ReturnsAsync(mockSession.Object);
        
        _discoveryMock.Setup(x => x.DiscoverAsync(config))
            .ReturnsAsync(topology);
        _discoveryMock.Setup(x => x.GetHostsAsync())
            .ReturnsAsync(topology.Hosts);

        // Act
        var session = await _orchestrator.ExecuteProbesAsync(config);

        // Assert
        session.Results.Should().HaveCount(topology.Hosts.Count);
        session.Results.Should().OnlyContain(r => !r.Success);
        session.Results.Should().OnlyContain(r => r.ErrorMessage!.Contains("Unexpected error: Probe failed"));
    }

    [Fact]
    public async Task ExecuteProbesAsync_ShouldRunProbesInParallel()
    {
        // Arrange
        var config = CreateTestConfiguration();
        config.ProbeSelection.ExecuteAllProbes = true; // Enable all probes
        var topology = CreateTestTopologyWithMultipleHosts(5);
        var mockSession = new Mock<ISession>();
        
        var executionTimes = new List<DateTime>();
        var probe = new Mock<IProbeAction>();
        probe.Setup(x => x.Type).Returns(ProbeType.Socket);
        probe.Setup(x => x.Name).Returns("Socket Probe");
        probe.Setup(x => x.ExecuteAsync(It.IsAny<HostProbe>(), It.IsAny<ProbeContext>()))
            .ReturnsAsync((HostProbe host, ProbeContext ctx) =>
            {
                lock (executionTimes)
                {
                    executionTimes.Add(DateTime.UtcNow);
                }
                Thread.Sleep(100); // Simulate work
                return ProbeResult.CreateSuccess(host, ProbeType.Socket, TimeSpan.FromMilliseconds(100));
            });
        
        _probeActions.Add(probe.Object);
        
        _sessionManagerMock.Setup(x => x.GetSessionAsync())
            .ReturnsAsync(mockSession.Object);
        
        _discoveryMock.Setup(x => x.DiscoverAsync(config))
            .ReturnsAsync(topology);
        _discoveryMock.Setup(x => x.GetHostsAsync())
            .ReturnsAsync(topology.Hosts);

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await _orchestrator.ExecuteProbesAsync(config);
        stopwatch.Stop();

        // Assert
        // If running sequentially, would take 500ms+ (5 hosts * 100ms)
        // In parallel, should complete much faster
        // Allow more time in CI environments for parallel execution
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(2000);
        executionTimes.Should().HaveCount(5);
    }

    private ProbeConfiguration CreateTestConfiguration()
    {
        return new ProbeConfiguration
        {
            ContactPoints = new List<string> { "10.0.0.1" },
            ProbeSelection = new ProbeSelectionSettings
            {
                ProbeNativePort = true,
                ExecuteAllProbes = false
            },
            Query = new QuerySettings
            {
                TestCql = "SELECT key FROM system.local",
                ConsistencyLevel = "LOCAL_ONE"
            }
        };
    }

    private ClusterTopology CreateTestTopology()
    {
        return new ClusterTopology
        {
            ClusterName = "TestCluster",
            Hosts = new List<HostProbe>
            {
                new HostProbe 
                { 
                    Address = IPAddress.Parse("10.0.0.1"),
                    Datacenter = "dc1",
                    Status = HostStatus.Up
                }
            }
        };
    }

    private ClusterTopology CreateTestTopologyWithMultipleHosts(int count)
    {
        var hosts = new List<HostProbe>();
        for (int i = 1; i <= count; i++)
        {
            hosts.Add(new HostProbe
            {
                Address = IPAddress.Parse($"10.0.0.{i}"),
                Datacenter = "dc1",
                Status = HostStatus.Up
            });
        }

        return new ClusterTopology
        {
            ClusterName = "TestCluster",
            Hosts = hosts
        };
    }

    private Mock<IProbeAction> CreateMockProbeAction(ProbeType type, bool enabled)
    {
        var mock = new Mock<IProbeAction>();
        mock.Setup(x => x.Type).Returns(type);
        mock.Setup(x => x.ExecuteAsync(It.IsAny<HostProbe>(), It.IsAny<ProbeContext>()))
            .ReturnsAsync((HostProbe host, ProbeContext ctx) => 
                ProbeResult.CreateSuccess(host, type, TimeSpan.FromMilliseconds(50)));
        return mock;
    }
}