using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Xunit;

namespace CassandraProbe.IntegrationTests;

public class CassandraContainerFixture : IAsyncLifetime
{
    private readonly IContainer _container;
    public string ConnectionString { get; private set; } = string.Empty;
    public int NativePort { get; private set; }
    public int StoragePort { get; private set; } = 7000;

    public CassandraContainerFixture()
    {
        _container = new ContainerBuilder()
            .WithImage("cassandra:4.1")
            .WithName($"cassandra-probe-test-{Guid.NewGuid()}")
            .WithPortBinding(9042, true) // Random host port
            .WithPortBinding(7000, true)
            .WithEnvironment("CASSANDRA_CLUSTER_NAME", "TestCluster")
            .WithEnvironment("CASSANDRA_DC", "dc1")
            .WithEnvironment("CASSANDRA_ENDPOINT_SNITCH", "GossipingPropertyFileSnitch")
            .WithEnvironment("MAX_HEAP_SIZE", "512M")
            .WithEnvironment("HEAP_NEWSIZE", "128M")
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilMessageIsLogged("Startup complete")
                .WithTimeout(TimeSpan.FromMinutes(2)))
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        
        NativePort = _container.GetMappedPublicPort(9042);
        StoragePort = _container.GetMappedPublicPort(7000);
        ConnectionString = $"localhost:{NativePort}";
        
        // Give Cassandra a moment to fully initialize
        await Task.Delay(TimeSpan.FromSeconds(5));
    }

    public async Task DisposeAsync()
    {
        await _container.StopAsync();
        await _container.DisposeAsync();
    }
}

[CollectionDefinition("Cassandra")]
public class CassandraCollection : ICollectionFixture<CassandraContainerFixture>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}