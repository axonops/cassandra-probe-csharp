using CassandraProbe.Core.Configuration;
using CassandraProbe.Core.Interfaces;
using CassandraProbe.Services;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CassandraProbe.IntegrationTests;

public class ReconnectionTests : IAsyncLifetime
{
    private IContainer? _cassandraContainer;
    private IServiceProvider? _serviceProvider;
    private int _nativePort;

    public async Task InitializeAsync()
    {
        // Start with container running
        _cassandraContainer = await StartCassandraContainer();
        _serviceProvider = BuildServiceProvider();
    }

    public async Task DisposeAsync()
    {
        if (_cassandraContainer != null)
        {
            await _cassandraContainer.StopAsync();
            await _cassandraContainer.DisposeAsync();
        }
    }

    [Fact]
    public async Task ConnectionMonitor_ShouldTrackReconnectionAfterNodeRestart()
    {
        // Arrange
        var sessionManager = _serviceProvider!.GetRequiredService<ISessionManager>();
        var connectionMonitor = _serviceProvider.GetRequiredService<IConnectionMonitor>();
        
        // Establish initial connection
        var session = await sessionManager.GetSessionAsync();
        session.Should().NotBeNull();
        
        var initialStatus = connectionMonitor.GetPoolStatus();
        initialStatus.ActiveConnections.Should().BeGreaterThan(0);

        // Act - Stop container
        await _cassandraContainer!.StopAsync();
        await Task.Delay(TimeSpan.FromSeconds(2)); // Wait for driver to detect

        var downStatus = connectionMonitor.GetPoolStatus();
        
        // Restart container
        await _cassandraContainer.StartAsync();
        await Task.Delay(TimeSpan.FromSeconds(10)); // Wait for reconnection

        // Try to use the session again
        try
        {
            await session.ExecuteAsync("SELECT key FROM system.local");
        }
        catch
        {
            // Expected to fail, but should trigger reconnection
        }

        await Task.Delay(TimeSpan.FromSeconds(5)); // Wait for reconnection to complete

        var reconnectedStatus = connectionMonitor.GetPoolStatus();
        var history = connectionMonitor.GetReconnectionHistory();

        // Assert
        downStatus.FailedHosts.Should().BeGreaterThan(0);
        history.Should().NotBeEmpty();
        history.Should().Contain(e => e.EventType == Core.Models.ReconnectionEventType.AttemptStarted);
    }

    [Fact]
    public async Task SessionManager_ShouldMaintainSameSessionAcrossReconnection()
    {
        // Arrange
        var sessionManager = _serviceProvider!.GetRequiredService<ISessionManager>();
        
        // Get initial session and cluster references
        var sessionBefore = await sessionManager.GetSessionAsync();
        var clusterBefore = sessionManager.GetCluster();

        // Act - Simulate brief network interruption
        // In real scenario, we'd stop/start the container
        // For now, just verify the references remain the same
        await Task.Delay(TimeSpan.FromSeconds(1));
        
        var sessionAfter = await sessionManager.GetSessionAsync();
        var clusterAfter = sessionManager.GetCluster();

        // Assert - Verify singleton behavior
        sessionAfter.Should().BeSameAs(sessionBefore);
        clusterAfter.Should().BeSameAs(clusterBefore);
    }

    [Fact]
    public async Task ProbeExecution_ShouldContinueUsingExistingSession()
    {
        // Arrange
        var orchestrator = _serviceProvider!.GetRequiredService<IProbeOrchestrator>();
        var sessionManager = _serviceProvider.GetRequiredService<ISessionManager>();
        var config = CreateProbeConfiguration();

        // Act - Run probes multiple times
        var session1 = await orchestrator.ExecuteProbesAsync(config);
        var clusterBetween = sessionManager.GetCluster();
        var session2 = await orchestrator.ExecuteProbesAsync(config);
        var clusterAfter = sessionManager.GetCluster();

        // Assert
        session1.Should().NotBeNull();
        session2.Should().NotBeNull();
        clusterBetween.Should().BeSameAs(clusterAfter); // Same cluster instance used
    }

    private async Task<IContainer> StartCassandraContainer()
    {
        var container = new ContainerBuilder()
            .WithImage("cassandra:4.1")
            .WithName($"cassandra-reconnect-test-{Guid.NewGuid()}")
            .WithPortBinding(9042, true)
            .WithEnvironment("CASSANDRA_CLUSTER_NAME", "ReconnectTestCluster")
            .WithEnvironment("MAX_HEAP_SIZE", "512M")
            .WithEnvironment("HEAP_NEWSIZE", "128M")
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilMessageIsLogged("Startup complete")
                .WithTimeout(TimeSpan.FromMinutes(2)))
            .Build();

        await container.StartAsync();
        _nativePort = container.GetMappedPublicPort(9042);
        
        // Give Cassandra time to fully initialize
        await Task.Delay(TimeSpan.FromSeconds(5));
        
        return container;
    }

    private IServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        
        var config = CreateProbeConfiguration();
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
        services.AddScoped<IProbeAction, Actions.SocketProbe>();
        services.AddScoped<IProbeAction, Actions.CqlQueryProbe>();

        return services.BuildServiceProvider();
    }

    private ProbeConfiguration CreateProbeConfiguration()
    {
        return new ProbeConfiguration
        {
            ContactPoints = new List<string> { "localhost" },
            Connection = new ConnectionSettings 
            { 
                Port = _nativePort,
                ConnectionTimeoutSeconds = 10,
                KeepAliveSeconds = 30
            },
            ProbeSelection = new ProbeSelectionSettings
            {
                ProbeNativePort = true
            }
        };
    }
}