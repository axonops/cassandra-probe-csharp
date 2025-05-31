# Cassandra Probe C# - Implementation Roadmap

## Quick Start Guide for Development

### 1. Initial Project Setup

```bash
# Create solution and projects
dotnet new sln -n CassandraProbe
dotnet new classlib -n CassandraProbe.Core -o src/CassandraProbe.Core
dotnet new classlib -n CassandraProbe.Actions -o src/CassandraProbe.Actions
dotnet new classlib -n CassandraProbe.Services -o src/CassandraProbe.Services
dotnet new classlib -n CassandraProbe.Scheduling -o src/CassandraProbe.Scheduling
dotnet new classlib -n CassandraProbe.Logging -o src/CassandraProbe.Logging
dotnet new console -n CassandraProbe.Cli -o src/CassandraProbe.Cli

# Add projects to solution
dotnet sln add src/**/*.csproj

# Add project references
cd src/CassandraProbe.Actions
dotnet add reference ../CassandraProbe.Core/CassandraProbe.Core.csproj

cd ../CassandraProbe.Services
dotnet add reference ../CassandraProbe.Core/CassandraProbe.Core.csproj
dotnet add reference ../CassandraProbe.Actions/CassandraProbe.Actions.csproj

cd ../CassandraProbe.Scheduling
dotnet add reference ../CassandraProbe.Core/CassandraProbe.Core.csproj
dotnet add reference ../CassandraProbe.Services/CassandraProbe.Services.csproj

cd ../CassandraProbe.Cli
dotnet add reference ../CassandraProbe.Core/CassandraProbe.Core.csproj
dotnet add reference ../CassandraProbe.Services/CassandraProbe.Services.csproj
dotnet add reference ../CassandraProbe.Scheduling/CassandraProbe.Scheduling.csproj
dotnet add reference ../CassandraProbe.Logging/CassandraProbe.Logging.csproj
```

### 2. Essential NuGet Packages

```xml
<!-- CassandraProbe.Core -->
<PackageReference Include="CassandraCSharpDriver" Version="3.18.0" />
<PackageReference Include="Microsoft.Extensions.Configuration.Abstractions" Version="7.0.0" />
<PackageReference Include="Microsoft.Extensions.Options" Version="7.0.0" />

<!-- CassandraProbe.Actions -->
<PackageReference Include="Polly" Version="8.2.0" />
<PackageReference Include="System.Net.NetworkInformation" Version="4.3.0" />

<!-- CassandraProbe.Services -->
<PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="7.0.0" />
<PackageReference Include="YamlDotNet" Version="13.7.1" />

<!-- CassandraProbe.Scheduling -->
<PackageReference Include="Quartz" Version="3.8.0" />
<PackageReference Include="Quartz.Extensions.DependencyInjection" Version="3.8.0" />

<!-- CassandraProbe.Logging -->
<PackageReference Include="Serilog" Version="3.1.1" />
<PackageReference Include="Serilog.Sinks.Console" Version="5.0.0" />
<PackageReference Include="Serilog.Sinks.File" Version="5.0.0" />
<PackageReference Include="Serilog.Formatting.Compact" Version="2.0.0" />

<!-- CassandraProbe.Cli -->
<PackageReference Include="CommandLineParser" Version="2.9.1" />
<PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="7.0.0" />
<PackageReference Include="Microsoft.Extensions.Configuration.Json" Version="7.0.0" />
<PackageReference Include="Microsoft.Extensions.Configuration.EnvironmentVariables" Version="7.0.0" />
```

## Implementation Order

### Step 1: Core Models and Interfaces (Day 1-2)

Create these files first as they define the contracts:

**CassandraProbe.Core/Models/HostProbe.cs**
```csharp
public class HostProbe
{
    public IPAddress Address { get; set; }
    public string Datacenter { get; set; }
    public string Rack { get; set; }
    public string CassandraVersion { get; set; }
    public int NativePort { get; set; } = 9042;
    public int StoragePort { get; set; } = 7000;
    public HostStatus Status { get; set; }
}
```

**CassandraProbe.Core/Interfaces/IProbeAction.cs**
```csharp
public interface IProbeAction
{
    string Name { get; }
    ProbeType Type { get; }
    Task<ProbeResult> ExecuteAsync(HostProbe host, ProbeContext context, CancellationToken cancellationToken = default);
}
```

### Step 2: Configuration System (Day 3-4)

**CassandraProbe.Core/Configuration/ProbeConfiguration.cs**
```csharp
public class ProbeConfiguration
{
    public List<string> ContactPoints { get; set; } = new();
    public AuthenticationSettings Authentication { get; set; } = new();
    public ProbeSelectionSettings ProbeSelection { get; set; } = new();
    public QuerySettings Query { get; set; } = new();
    public LoggingSettings Logging { get; set; } = new();
    public SchedulingSettings Scheduling { get; set; } = new();
}
```

**CassandraProbe.Core/Configuration/CqlshrcParser.cs**
- Parse CQLSHRC files for authentication
- Extract SSL settings
- Handle various formats

### Step 3: Basic Probe Implementations (Day 5-7)

Start with the simplest probe and build up:

**CassandraProbe.Actions/SocketProbe.cs**
```csharp
public class SocketProbe : IProbeAction
{
    public async Task<ProbeResult> ExecuteAsync(HostProbe host, ProbeContext context, CancellationToken cancellationToken)
    {
        // Implement socket connection test
    }
}
```

**CassandraProbe.Actions/PingProbe.cs**
- Use System.Net.NetworkInformation.Ping
- Handle platform differences

### Step 4: Connection Management and Discovery (Day 8-10)

**CassandraProbe.Services/SessionManager.cs**
```csharp
public class SessionManager : ISessionManager
{
    private static readonly object _lock = new object();
    private static ICluster _cluster;
    private static ISession _session;
    private readonly ILogger<SessionManager> _logger;
    private readonly IConnectionMonitor _connectionMonitor;
    
    public async Task<ISession> GetSessionAsync()
    {
        if (_session == null)
        {
            lock (_lock)
            {
                if (_session == null)
                {
                    _logger.LogInformation("Creating new Cluster instance (one-time operation)");
                    _cluster = CreateCluster();
                    RegisterEventHandlers();
                    _session = _cluster.Connect();
                    _logger.LogInformation("Session established and will be reused for all operations");
                }
            }
        }
        return _session;
    }
    
    private void RegisterEventHandlers()
    {
        _cluster.Metadata.HostAdded += (sender, host) => 
            _logger.LogInformation($"Host added: {host.Address}");
        _cluster.Metadata.HostRemoved += (sender, host) => 
            _logger.LogInformation($"Host removed: {host.Address}");
        _cluster.Metadata.HostUp += (sender, host) => 
            _logger.LogInformation($"Host up: {host.Address} - Driver reconnected successfully");
        _cluster.Metadata.HostDown += (sender, host) => 
            _logger.LogWarning($"Host down: {host.Address} - Driver will attempt reconnection");
    }
}
```

**CassandraProbe.Services/ClusterDiscoveryService.cs**
```csharp
public class ClusterDiscoveryService : IClusterDiscovery
{
    private readonly ISessionManager _sessionManager;
    
    public async Task<IEnumerable<HostProbe>> DiscoverHostsAsync()
    {
        // Query system.peers and system.local
        // Build HostProbe objects
    }
}
```

### Step 5: CQL Query Probe (Day 11-12)

**CassandraProbe.Actions/CqlQueryProbe.cs**
- Validate query types
- Execute with consistency levels
- Handle tracing

### Step 6: Probe Orchestration (Day 13-15)

**CassandraProbe.Services/ProbeOrchestrator.cs**
```csharp
public class ProbeOrchestrator : IProbeOrchestrator
{
    public async Task<ProbeSession> ExecuteProbesAsync(ProbeConfiguration config)
    {
        // Discover hosts
        // Select probes based on config
        // Execute in parallel
        // Aggregate results
    }
}
```

### Step 7: Logging Infrastructure (Day 16-17)

**CassandraProbe.Logging/ProbeLogger.cs**
```csharp
public static class ProbeLogger
{
    public static ILogger CreateLogger(LoggingSettings settings)
    {
        var config = new LoggerConfiguration()
            .WriteTo.Console()
            .WriteTo.File(
                path: Path.Combine(settings.Directory, "probe-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: settings.MaxDays
            );
            
        return config.CreateLogger();
    }
}
```

### Step 8: CLI Application (Day 18-20)

**CassandraProbe.Cli/Program.cs**
```csharp
class Program
{
    static async Task<int> Main(string[] args)
    {
        return await Parser.Default.ParseArguments<ProbeOptions, ScheduleOptions>(args)
            .MapResult(
                (ProbeOptions opts) => RunProbe(opts),
                (ScheduleOptions opts) => RunScheduled(opts),
                errs => Task.FromResult(1)
            );
    }
}
```

### Step 9: Scheduling Support (Day 21-22)

**CassandraProbe.Scheduling/ProbeJob.cs**
```csharp
public class ProbeJob : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        // Retrieve configuration
        // Execute probe orchestrator
        // Log results
    }
}
```

### Step 10: Testing Infrastructure (Day 23-25)

Create test projects:
```bash
dotnet new xunit -n CassandraProbe.Core.Tests -o tests/CassandraProbe.Core.Tests
dotnet new xunit -n CassandraProbe.Actions.Tests -o tests/CassandraProbe.Actions.Tests
dotnet new xunit -n CassandraProbe.Integration.Tests -o tests/CassandraProbe.Integration.Tests
```

## Key Implementation Patterns

### 1. Async/Await Pattern
```csharp
public async Task<ProbeResult> ExecuteAsync(HostProbe host, CancellationToken ct)
{
    try
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(10));
        
        return await DoProbeAsync(host, cts.Token);
    }
    catch (OperationCanceledException)
    {
        return ProbeResult.Timeout(host);
    }
}
```

### 2. Polly Retry Pattern
```csharp
var retryPolicy = Policy
    .Handle<SocketException>()
    .WaitAndRetryAsync(
        3,
        retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
        onRetry: (exception, timeSpan, retryCount, context) =>
        {
            _logger.LogWarning($"Retry {retryCount} after {timeSpan} seconds");
        });

return await retryPolicy.ExecuteAsync(async () => await ConnectAsync());
```

### 3. Parallel Execution Pattern
```csharp
var tasks = hosts.Select(host => ProbeHostAsync(host)).ToList();
var results = await Task.WhenAll(tasks);
```

### 4. Configuration Pattern
```csharp
services.Configure<ProbeConfiguration>(configuration.GetSection("Probe"));
services.AddSingleton(resolver => resolver.GetRequiredService<IOptions<ProbeConfiguration>>().Value);
```

## Development Workflow

### 1. Feature Branch Strategy
```bash
git checkout -b feature/socket-probe
# Implement feature
git commit -m "feat: implement socket probe"
git push origin feature/socket-probe
```

### 2. Local Testing
```bash
# Run with local Cassandra
docker run -d --name cassandra -p 9042:9042 cassandra:4.0

# Test the probe
dotnet run -- -cp localhost -u cassandra -p cassandra
```

### 3. Integration Testing
```bash
# Run integration tests
docker-compose -f tests/docker-compose.yml up -d
dotnet test tests/CassandraProbe.Integration.Tests
```

### 4. Testing Reconnection Scenarios
```bash
# Start probe in continuous mode
dotnet run -- -cp localhost:9042 -i 10

# In another terminal, simulate failures:
# Stop a node
docker stop cassandra-node1

# Watch probe logs for reconnection attempts
# Start the node again
docker start cassandra-node1

# Verify driver reconnects automatically
```

## Minimum Viable Product (MVP)

For the initial working version, implement:

1. **Core Models**: HostProbe, ProbeResult
2. **Basic Discovery**: Connect and list nodes
3. **Socket Probe**: Test connectivity
4. **Simple CLI**: Basic command parsing
5. **Console Logging**: Simple output

This MVP can be completed in 5-7 days and provides immediate value.

## Performance Optimization Checklist

- [ ] Use `ConfigureAwait(false)` in library code
- [ ] Implement connection pooling
- [ ] Use `ValueTask` where appropriate
- [ ] Minimize allocations in hot paths
- [ ] Profile memory usage
- [ ] Benchmark probe execution
- [ ] Optimize parallel execution

## Quality Checklist

- [ ] Unit test coverage > 80%
- [ ] Integration tests for all probes
- [ ] XML documentation on public APIs
- [ ] Code analysis rules enabled
- [ ] Nullable reference types enabled
- [ ] Security scanning configured
- [ ] Performance benchmarks

## Release Checklist

- [ ] All tests passing
- [ ] Documentation complete
- [ ] NuGet package metadata
- [ ] Docker image built
- [ ] Cross-platform tested
- [ ] Performance validated
- [ ] Security scan clean
- [ ] Release notes prepared

## Platform-Specific Build Instructions

### Building Self-Contained Executables

Create platform-specific executables that don't require .NET runtime:

**macOS (Intel)**
```bash
dotnet publish -c Release -r osx-x64 --self-contained true -p:PublishSingleFile=true -o ./publish/macos-intel
```

**macOS (Apple Silicon)**
```bash
dotnet publish -c Release -r osx-arm64 --self-contained true -p:PublishSingleFile=true -o ./publish/macos-arm
```

**Windows (64-bit)**
```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ./publish/windows
```

**Linux (64-bit)**
```bash
dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o ./publish/linux
```

**Linux (ARM64)**
```bash
dotnet publish -c Release -r linux-arm64 --self-contained true -p:PublishSingleFile=true -o ./publish/linux-arm
```

### Testing on Each Platform

**macOS:**
```bash
# Make executable
chmod +x ./publish/macos-intel/CassandraProbe.Cli

# Test with local Docker Cassandra
docker run -d --name test-cassandra -p 9042:9042 cassandra:4.1
./publish/macos-intel/CassandraProbe.Cli -cp localhost:9042
```

**Windows:**
```powershell
# Test with local Docker Cassandra
docker run -d --name test-cassandra -p 9042:9042 cassandra:4.1
.\publish\windows\CassandraProbe.Cli.exe -cp localhost:9042
```

**Linux:**
```bash
# Make executable
chmod +x ./publish/linux/CassandraProbe.Cli

# Test with local Docker Cassandra
docker run -d --name test-cassandra -p 9042:9042 cassandra:4.1
./publish/linux/CassandraProbe.Cli -cp localhost:9042
```

### Docker Testing

```bash
# Build Docker image
docker build -t cassandra-probe .

# Run from Docker
docker run --rm cassandra-probe -cp host.docker.internal:9042

# With authentication
docker run --rm cassandra-probe -cp host.docker.internal:9042 -u cassandra -p cassandra
```