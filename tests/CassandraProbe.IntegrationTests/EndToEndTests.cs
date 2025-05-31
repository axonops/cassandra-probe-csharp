using CassandraProbe.Actions;
using CassandraProbe.Actions.PortSpecificProbes;
using CassandraProbe.Cli.DependencyInjection;
using CassandraProbe.Core.Configuration;
using CassandraProbe.Core.Interfaces;
using CassandraProbe.Core.Models;
using CassandraProbe.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CassandraProbe.IntegrationTests;

[Collection("Cassandra")]
public class EndToEndTests
{
    private readonly CassandraContainerFixture _fixture;
    private readonly IServiceProvider _serviceProvider;

    public EndToEndTests(CassandraContainerFixture fixture)
    {
        _fixture = fixture;
        _serviceProvider = BuildServiceProvider();
    }

    [Fact]
    public async Task ProbeOrchestrator_ShouldDiscoverAndProbeCluster()
    {
        // Arrange
        var orchestrator = _serviceProvider.GetRequiredService<IProbeOrchestrator>();
        var config = new ProbeConfiguration
        {
            ContactPoints = new List<string> { "localhost" },
            Connection = new ConnectionSettings { Port = _fixture.NativePort },
            ProbeSelection = new ProbeSelectionSettings { ExecuteAllProbes = true }
        };

        // Act
        var session = await orchestrator.ExecuteProbesAsync(config);

        // Assert
        session.Should().NotBeNull();
        session.Topology.Should().NotBeNull();
        session.Topology!.ClusterName.Should().Be("TestCluster");
        session.Topology.Hosts.Should().NotBeEmpty();
        session.Results.Should().NotBeEmpty();
        session.Results.Where(r => r.Success).Should().NotBeEmpty();
    }

    [Fact]
    public async Task SessionManager_ShouldMaintainSingletonConnection()
    {
        // Arrange
        var sessionManager = _serviceProvider.GetRequiredService<ISessionManager>();

        // Act
        var session1 = await sessionManager.GetSessionAsync();
        var session2 = await sessionManager.GetSessionAsync();
        var cluster1 = sessionManager.GetCluster();
        var cluster2 = sessionManager.GetCluster();

        // Assert
        session1.Should().BeSameAs(session2);
        cluster1.Should().BeSameAs(cluster2);
        cluster1.Should().NotBeNull();
    }

    [Fact]
    public async Task SocketProbe_ShouldSucceedAgainstRunningCassandra()
    {
        // Arrange
        var probe = _serviceProvider.GetRequiredService<IEnumerable<IProbeAction>>()
            .First(p => p.Type == ProbeType.Socket);
        
        var host = new HostProbe
        {
            Address = System.Net.IPAddress.Loopback,
            NativePort = _fixture.NativePort
        };
        var context = new ProbeContext();

        // Act
        var result = await probe.ExecuteAsync(host, context);

        // Assert
        result.Success.Should().BeTrue();
        result.Duration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task CqlQueryProbe_ShouldExecuteQuerySuccessfully()
    {
        // Arrange
        var probe = _serviceProvider.GetRequiredService<IEnumerable<IProbeAction>>()
            .First(p => p.Type == ProbeType.CqlQuery);
        
        var host = new HostProbe
        {
            Address = System.Net.IPAddress.Loopback,
            NativePort = _fixture.NativePort
        };
        var context = new ProbeContext
        {
            TestQuery = "SELECT key FROM system.local",
            ConsistencyLevel = "ONE"
        };

        // Act
        var result = await probe.ExecuteAsync(host, context);

        // Assert
        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task ClusterDiscovery_ShouldFindLocalNode()
    {
        // Arrange
        var discovery = _serviceProvider.GetRequiredService<IClusterDiscovery>();
        var config = new ProbeConfiguration
        {
            ContactPoints = new List<string> { "localhost" },
            Connection = new ConnectionSettings { Port = _fixture.NativePort }
        };

        // Act
        var topology = await discovery.DiscoverAsync(config);

        // Assert
        topology.Should().NotBeNull();
        topology.ClusterName.Should().Be("TestCluster");
        topology.Hosts.Should().HaveCountGreaterOrEqualTo(1);
        topology.Hosts.First().Status.Should().Be(HostStatus.Up);
        topology.Hosts.First().Datacenter.Should().Be("dc1");
    }

    [Fact]
    public async Task ConnectionMonitor_ShouldTrackConnectionEvents()
    {
        // Arrange
        var monitor = _serviceProvider.GetRequiredService<IConnectionMonitor>();
        var orchestrator = _serviceProvider.GetRequiredService<IProbeOrchestrator>();
        var config = new ProbeConfiguration
        {
            ContactPoints = new List<string> { "localhost" },
            Connection = new ConnectionSettings { Port = _fixture.NativePort }
        };

        // Act
        await orchestrator.ExecuteProbesAsync(config);
        var poolStatus = monitor.GetPoolStatus();

        // Assert
        poolStatus.TotalConnections.Should().BeGreaterThan(0);
        poolStatus.ActiveConnections.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task AllProbeTypes_ShouldExecuteAgainstCluster()
    {
        // Arrange
        var orchestrator = _serviceProvider.GetRequiredService<IProbeOrchestrator>();
        var config = new ProbeConfiguration
        {
            ContactPoints = new List<string> { "localhost" },
            Connection = new ConnectionSettings { Port = _fixture.NativePort },
            ProbeSelection = new ProbeSelectionSettings 
            { 
                ExecuteAllProbes = true,
                SocketTimeoutMs = 10000
            }
        };

        // Act
        var session = await orchestrator.ExecuteProbesAsync(config);

        // Assert
        var probeTypes = session.Results.Select(r => r.ProbeType).Distinct().ToList();
        probeTypes.Should().Contain(ProbeType.Socket);
        probeTypes.Should().Contain(ProbeType.CqlQuery);
        // Note: Ping might not work in container environment
        // Storage port might not be accessible from outside container
    }

    private IServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        
        // Configuration
        var config = new ProbeConfiguration
        {
            ContactPoints = new List<string> { "localhost" },
            Connection = new ConnectionSettings 
            { 
                Port = _fixture.NativePort,
                ConnectionTimeoutSeconds = 30
            }
        };
        services.AddSingleton(config);

        // Logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        // Add all probe services
        services.AddCassandraProbeServices();

        return services.BuildServiceProvider();
    }
}