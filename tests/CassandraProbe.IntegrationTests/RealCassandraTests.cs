using CassandraProbe.Actions;
using CassandraProbe.Core.Configuration;
using CassandraProbe.Core.Interfaces;
using CassandraProbe.Core.Models;
using CassandraProbe.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CassandraProbe.IntegrationTests;

public class RealCassandraTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly bool _cassandraAvailable;

    public RealCassandraTests()
    {
        _serviceProvider = BuildServiceProvider();
        _cassandraAvailable = CheckCassandraAvailable();
    }

    private bool CheckCassandraAvailable()
    {
        try
        {
            using var socket = new System.Net.Sockets.TcpClient();
            socket.Connect("localhost", 9042);
            return true;
        }
        catch
        {
            return false;
        }
    }

    [Fact]
    public async Task SessionManager_ShouldConnectToRealCassandra()
    {
        if (!_cassandraAvailable)
        {
            // Skip test if Cassandra not available
            return;
        }

        // Arrange
        var sessionManager = _serviceProvider.GetRequiredService<ISessionManager>();

        // Act
        var session = await sessionManager.GetSessionAsync();

        // Assert
        session.Should().NotBeNull();
        session.Cluster.Should().NotBeNull();
        session.IsDisposed.Should().BeFalse();
    }

    [Fact]
    public async Task ClusterDiscovery_ShouldDiscoverRealCluster()
    {
        if (!_cassandraAvailable)
        {
            return;
        }

        // Arrange
        var discovery = _serviceProvider.GetRequiredService<IClusterDiscovery>();
        var config = new ProbeConfiguration
        {
            ContactPoints = new List<string> { "localhost" }
        };

        // Act
        var topology = await discovery.DiscoverAsync(config);

        // Assert
        topology.Should().NotBeNull();
        topology.ClusterName.Should().Be("TestCluster");
        topology.Hosts.Should().NotBeEmpty();
        topology.Hosts.First().Status.Should().Be(HostStatus.Up);
    }

    [Fact]
    public async Task ProbeOrchestrator_ShouldExecuteAllProbesAgainstRealCassandra()
    {
        if (!_cassandraAvailable)
        {
            return;
        }

        // Arrange
        var orchestrator = _serviceProvider.GetRequiredService<IProbeOrchestrator>();
        var config = new ProbeConfiguration
        {
            ContactPoints = new List<string> { "localhost" },
            ProbeSelection = new ProbeSelectionSettings
            {
                ExecuteAllProbes = true,
                SocketTimeoutMs = 5000
            }
        };

        // Act
        var session = await orchestrator.ExecuteProbesAsync(config);

        // Assert
        session.Should().NotBeNull();
        session.Results.Should().NotBeEmpty();
        
        // Socket probe should succeed
        var socketResults = session.Results.Where(r => r.ProbeType == ProbeType.Socket);
        socketResults.Should().NotBeEmpty();
        socketResults.Should().Contain(r => r.Success);

        // CQL query probe should succeed
        var cqlResults = session.Results.Where(r => r.ProbeType == ProbeType.CqlQuery);
        cqlResults.Should().NotBeEmpty();
        cqlResults.Should().Contain(r => r.Success);
    }

    [Fact]
    public async Task ConnectionMonitor_ShouldTrackRealConnectionEvents()
    {
        if (!_cassandraAvailable)
        {
            return;
        }

        // Arrange
        var monitor = _serviceProvider.GetRequiredService<IConnectionMonitor>();
        var sessionManager = _serviceProvider.GetRequiredService<ISessionManager>();

        // Act
        await sessionManager.GetSessionAsync();
        var poolStatus = monitor.GetPoolStatus();

        // Assert
        poolStatus.TotalConnections.Should().BeGreaterThan(0);
        poolStatus.ActiveConnections.Should().BeGreaterThan(0);
        poolStatus.FailedHosts.Should().Be(0);
    }

    private IServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        
        var config = new ProbeConfiguration
        {
            ContactPoints = new List<string> { "localhost" },
            Connection = new ConnectionSettings 
            { 
                Port = 9042,
                ConnectionTimeoutSeconds = 10
            }
        };
        services.AddSingleton(config);

        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // Core services
        services.AddSingleton<ISessionManager, SessionManager>();
        services.AddSingleton<IConnectionMonitor, ConnectionMonitor>();
        services.AddScoped<IClusterDiscovery, ClusterDiscoveryService>();
        services.AddScoped<IProbeOrchestrator, ProbeOrchestrator>();
        
        // Probe actions
        services.AddScoped<IProbeAction, SocketProbe>();
        services.AddScoped<IProbeAction, PingProbe>();
        services.AddScoped<IProbeAction, CqlQueryProbe>();
        services.AddScoped<IProbeAction, Actions.PortSpecificProbes.NativePortProbe>();
        services.AddScoped<IProbeAction, Actions.PortSpecificProbes.StoragePortProbe>();

        return services.BuildServiceProvider();
    }
}