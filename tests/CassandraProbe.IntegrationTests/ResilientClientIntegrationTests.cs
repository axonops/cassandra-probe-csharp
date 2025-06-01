using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cassandra;
using CassandraProbe.Core.Configuration;
using CassandraProbe.Core.Interfaces;
using CassandraProbe.Services;
using CassandraProbe.Services.Resilience;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace CassandraProbe.IntegrationTests;

public class ResilientClientIntegrationTests : IAsyncLifetime, IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly ServiceProvider _serviceProvider;
    private readonly List<IContainer> _cassandraContainers = new();
    private readonly List<string> _nodeAddresses = new();
    private const int NumberOfNodes = 3;
    private const string CassandraImage = "cassandra:4.1";
    private const string ClusterName = "TestCluster";

    public ResilientClientIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Debug);
            builder.AddConsole();
        });
        
        _serviceProvider = services.BuildServiceProvider();
    }

    public async Task InitializeAsync()
    {
        _output.WriteLine("Starting Cassandra cluster with {0} nodes...", NumberOfNodes);
        
        // Start first node
        var firstNode = await StartCassandraNode(1, null);
        _cassandraContainers.Add(firstNode);
        _nodeAddresses.Add(firstNode.Hostname);
        
        // Wait for first node to be ready
        await WaitForNodeReady(firstNode);
        
        // Start additional nodes
        for (int i = 2; i <= NumberOfNodes; i++)
        {
            var node = await StartCassandraNode(i, firstNode.Hostname);
            _cassandraContainers.Add(node);
            _nodeAddresses.Add(node.Hostname);
            await WaitForNodeReady(node);
        }
        
        _output.WriteLine("Cassandra cluster started with nodes: {0}", string.Join(", ", _nodeAddresses));
    }

    private async Task<IContainer> StartCassandraNode(int nodeNumber, string? seedNode)
    {
        var builder = new ContainerBuilder()
            .WithImage(CassandraImage)
            .WithName($"cassandra-test-node-{nodeNumber}")
            .WithPortBinding(9042, true)
            .WithEnvironment("CASSANDRA_CLUSTER_NAME", ClusterName)
            .WithEnvironment("CASSANDRA_DC", "dc1")
            .WithEnvironment("CASSANDRA_ENDPOINT_SNITCH", "GossipingPropertyFileSnitch")
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilMessageIsLogged("Startup complete"));

        if (seedNode != null)
        {
            builder = builder.WithEnvironment("CASSANDRA_SEEDS", seedNode);
        }

        var container = builder.Build();
        await container.StartAsync();
        
        return container;
    }

    private async Task WaitForNodeReady(IContainer container)
    {
        var maxWaitTime = TimeSpan.FromMinutes(2);
        var stopwatch = Stopwatch.StartNew();
        
        while (stopwatch.Elapsed < maxWaitTime)
        {
            try
            {
                var cluster = Cluster.Builder()
                    .AddContactPoint(container.Hostname)
                    .WithPort(container.GetMappedPublicPort(9042))
                    .Build();
                    
                using (cluster)
                {
                    var session = cluster.Connect();
                    session.Dispose();
                    _output.WriteLine("Node {0} is ready", container.Hostname);
                    return;
                }
            }
            catch
            {
                await Task.Delay(TimeSpan.FromSeconds(2));
            }
        }
        
        throw new TimeoutException($"Node {container.Hostname} did not become ready within {maxWaitTime}");
    }

    [Fact]
    public async Task ResilientClient_HandlesRollingRestart()
    {
        // Arrange
        var logger = _serviceProvider.GetRequiredService<ILogger<ResilientCassandraClient>>();
        var config = new ProbeConfiguration
        {
            ContactPoints = _nodeAddresses,
            Connection = new ConnectionSettings 
            { 
                Port = _cassandraContainers.First().GetMappedPublicPort(9042) 
            }
        };
        
        var options = new ResilientClientOptions
        {
            HostMonitoringInterval = TimeSpan.FromSeconds(2),
            ConnectionRefreshInterval = TimeSpan.FromSeconds(10),
            MaxRetryAttempts = 3
        };
        
        using var resilientClient = new ResilientCassandraClient(config, logger, options);
        
        // Create test keyspace and table
        await resilientClient.ExecuteAsync(
            "CREATE KEYSPACE IF NOT EXISTS test WITH replication = " +
            "{'class': 'SimpleStrategy', 'replication_factor': 3}");
        await resilientClient.ExecuteAsync(
            "CREATE TABLE IF NOT EXISTS test.data (id int PRIMARY KEY, value text)");
        
        // Act - Start query loop
        var cts = new CancellationTokenSource();
        var queryTask = Task.Run(async () => await ContinuouslyQuery(resilientClient, cts.Token));
        
        // Perform rolling restart
        _output.WriteLine("Starting rolling restart...");
        foreach (var container in _cassandraContainers)
        {
            _output.WriteLine("Stopping node {0}...", container.Hostname);
            await container.StopAsync();
            
            await Task.Delay(TimeSpan.FromSeconds(5));
            
            _output.WriteLine("Starting node {0}...", container.Hostname);
            await container.StartAsync();
            await WaitForNodeReady(container);
            
            await Task.Delay(TimeSpan.FromSeconds(5));
        }
        
        // Let queries run for a bit after restart
        await Task.Delay(TimeSpan.FromSeconds(10));
        cts.Cancel();
        
        await queryTask;
        
        // Assert - Check metrics
        var metrics = resilientClient.GetMetrics();
        _output.WriteLine("Final metrics:");
        _output.WriteLine("  Total queries: {0}", metrics.TotalQueries);
        _output.WriteLine("  Failed queries: {0}", metrics.FailedQueries);
        _output.WriteLine("  Success rate: {0:P2}", metrics.SuccessRate);
        _output.WriteLine("  State transitions: {0}", metrics.StateTransitions);
        
        Assert.True(metrics.TotalQueries > 0, "Should have executed queries");
        Assert.True(metrics.SuccessRate > 0.7, "Success rate should be above 70% during rolling restart");
        Assert.True(metrics.StateTransitions > 0, "Should have detected state transitions");
    }

    private async Task ContinuouslyQuery(IResilientCassandraClient client, CancellationToken cancellationToken)
    {
        var random = new Random();
        var successCount = 0;
        var failureCount = 0;
        
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var id = random.Next(1000);
                await client.ExecuteIdempotentAsync(
                    "INSERT INTO test.data (id, value) VALUES (?, ?)",
                    id, $"test-{id}");
                
                var result = await client.ExecuteIdempotentAsync(
                    "SELECT * FROM test.data WHERE id = ?", id);
                
                if (result.Any())
                {
                    successCount++;
                    _output.WriteLine("[SUCCESS] Query completed successfully");
                }
            }
            catch (Exception ex)
            {
                failureCount++;
                _output.WriteLine("[FAILURE] Query failed: {0}", ex.Message);
            }
            
            await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
        }
        
        _output.WriteLine("Query loop completed. Success: {0}, Failures: {1}", 
            successCount, failureCount);
    }

    [Fact]
    public async Task ResilientClient_ComparedToStandardClient()
    {
        // This test compares behavior of standard vs resilient client
        var logger = _serviceProvider.GetRequiredService<ILogger<ResilientCassandraClient>>();
        var sessionLogger = _serviceProvider.GetRequiredService<ILogger<SessionManager>>();
        
        var config = new ProbeConfiguration
        {
            ContactPoints = _nodeAddresses,
            Connection = new ConnectionSettings 
            { 
                Port = _cassandraContainers.First().GetMappedPublicPort(9042) 
            }
        };
        
        // Create both clients
        var connectionMonitor = new Mock<IConnectionMonitor>();
        var standardSessionManager = new SessionManager(sessionLogger, connectionMonitor.Object, config);
        var standardSession = await standardSessionManager.GetSessionAsync();
        
        using var resilientClient = new ResilientCassandraClient(config, logger);
        
        // Prepare test data
        await standardSession.ExecuteAsync(new SimpleStatement(
            "CREATE KEYSPACE IF NOT EXISTS test WITH replication = " +
            "{'class': 'SimpleStrategy', 'replication_factor': 3}"));
        await standardSession.ExecuteAsync(new SimpleStatement(
            "CREATE TABLE IF NOT EXISTS test.comparison (id int PRIMARY KEY, client text)"));
        
        // Stop one node
        var nodeToStop = _cassandraContainers[1];
        _output.WriteLine("Stopping node {0} to simulate failure...", nodeToStop.Hostname);
        await nodeToStop.StopAsync();
        
        // Wait for detection
        await Task.Delay(TimeSpan.FromSeconds(10));
        
        // Try queries with both clients
        var standardErrors = 0;
        var resilientErrors = 0;
        var iterations = 10;
        
        for (int i = 0; i < iterations; i++)
        {
            // Standard client
            try
            {
                await standardSession.ExecuteAsync(new SimpleStatement(
                    "INSERT INTO test.comparison (id, client) VALUES (?, ?)", i, "standard"));
            }
            catch
            {
                standardErrors++;
            }
            
            // Resilient client
            try
            {
                await resilientClient.ExecuteAsync(
                    "INSERT INTO test.comparison (id, client) VALUES (?, ?)",
                    i + 1000, "resilient");
            }
            catch
            {
                resilientErrors++;
            }
            
            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }
        
        _output.WriteLine("Standard client errors: {0}/{1}", standardErrors, iterations);
        _output.WriteLine("Resilient client errors: {0}/{1}", resilientErrors, iterations);
        
        // Restart the node
        await nodeToStop.StartAsync();
        await WaitForNodeReady(nodeToStop);
        
        standardSessionManager.Dispose();
        
        // Assert - Resilient client should have fewer errors
        Assert.True(resilientErrors <= standardErrors, 
            "Resilient client should have same or fewer errors than standard client");
    }

    public async Task DisposeAsync()
    {
        foreach (var container in _cassandraContainers)
        {
            await container.StopAsync();
            await container.DisposeAsync();
        }
        
        _serviceProvider?.Dispose();
    }

    public void Dispose()
    {
        DisposeAsync().GetAwaiter().GetResult();
    }
}