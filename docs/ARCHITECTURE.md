# Cassandra Probe C# - Architecture

## Overview

Cassandra Probe C# follows a clean, modular architecture based on SOLID principles and modern .NET design patterns. The application is structured as a set of loosely-coupled components that can be easily extended and tested.

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                         CLI Layer                                │
│  ┌─────────────┐  ┌──────────────┐  ┌────────────────────┐    │
│  │   Program   │  │   Commands   │  │  Dependency Setup  │    │
│  └─────────────┘  └──────────────┘  └────────────────────┘    │
└─────────────────────────────────────────────────────────────────┘
                                │
┌─────────────────────────────────────────────────────────────────┐
│                      Service Layer                               │
│  ┌─────────────────┐  ┌──────────────────┐  ┌───────────────┐ │
│  │ ProbeOrchestrator│  │ClusterDiscovery  │  │SessionManager │ │
│  └─────────────────┘  └──────────────────┘  └───────────────┘ │
│  ┌─────────────────┐  ┌──────────────────┐  ┌───────────────┐ │
│  │ConnectionMonitor │  │ MetadataMonitor  │  │HostStateMonitor│ │
│  └─────────────────┘  └──────────────────┘  └───────────────┘ │
└─────────────────────────────────────────────────────────────────┘
                                │
┌─────────────────────────────────────────────────────────────────┐
│                        Core Layer                                │
│  ┌───────────┐  ┌────────────┐  ┌──────────┐  ┌─────────────┐ │
│  │   Models  │  │ Interfaces │  │  Config  │  │ Exceptions  │ │
│  └───────────┘  └────────────┘  └──────────┘  └─────────────┘ │
└─────────────────────────────────────────────────────────────────┘
                                │
┌─────────────────────────────────────────────────────────────────┐
│                     Actions Layer                                │
│  ┌────────────┐  ┌───────────┐  ┌─────────────┐  ┌──────────┐ │
│  │SocketProbe │  │ PingProbe │  │CqlQueryProbe│  │PortProbes│ │
│  └────────────┘  └───────────┘  └─────────────┘  └──────────┘ │
└─────────────────────────────────────────────────────────────────┘
                                │
┌─────────────────────────────────────────────────────────────────┐
│                   Infrastructure Layer                           │
│  ┌────────────┐  ┌────────────┐  ┌──────────┐  ┌────────────┐ │
│  │  Logging   │  │ Scheduling │  │ Metrics  │  │   Export   │ │
│  └────────────┘  └────────────┘  └──────────┘  └────────────┘ │
└─────────────────────────────────────────────────────────────────┘
```

## Layer Descriptions

### 1. CLI Layer
The entry point and command-line interface handling.

**Responsibilities:**
- Parse command-line arguments
- Configure dependency injection
- Execute commands
- Handle process lifecycle

**Key Components:**
- `Program.cs`: Application entry point
- `CommandLineOptions`: Argument definitions
- `Commands/`: Command implementations
- `ServiceConfiguration`: DI setup

### 2. Service Layer
Business logic orchestration and coordination.

**Responsibilities:**
- Orchestrate probe execution
- Discover cluster topology
- Handle authentication
- Aggregate results

**Key Components:**
- `ProbeOrchestrator`: Coordinates probe execution
- `ClusterDiscoveryService`: Discovers Cassandra nodes
- `SessionManager`: Manages Cassandra driver sessions and cluster events
- `ConnectionMonitor`: Tracks connection states and reconnection history
- `MetadataMonitor`: Monitors cluster metadata and schema changes
- `HostStateMonitor`: Detects host UP/DOWN state transitions

### 3. Core Layer
Domain models and contracts.

**Responsibilities:**
- Define domain entities
- Establish contracts (interfaces)
- Configuration models
- Domain exceptions

**Key Components:**
- `Models/`: Domain entities
- `Interfaces/`: Service contracts
- `Configuration/`: Config models
- `Exceptions/`: Custom exceptions

### 4. Actions Layer
Probe implementations.

**Responsibilities:**
- Implement specific probe types
- Execute connectivity tests
- Measure performance
- Return standardized results

**Key Components:**
- `SocketProbe`: TCP socket testing
- `PingProbe`: ICMP/TCP reachability
- `CqlQueryProbe`: Query execution
- `PortSpecificProbes/`: Specialized probes

### 5. Infrastructure Layer
Cross-cutting concerns and utilities.

**Responsibilities:**
- Logging infrastructure
- Job scheduling
- Metrics collection
- Result export

**Key Components:**
- `ProbeLogger`: Logging setup
- `JobScheduler`: Quartz.NET integration
- `MetricsExporter`: Metrics export
- `Formatters/`: Output formatting

## Key Design Patterns

### 1. Dependency Injection
All components are registered and resolved through DI container.

```csharp
services.AddScoped<IProbeOrchestrator, ProbeOrchestrator>();
services.AddScoped<IClusterDiscovery, ClusterDiscoveryService>();
services.AddScoped<IProbeAction, SocketProbe>();
```

### 2. Strategy Pattern
Probe actions implement a common interface.

```csharp
public interface IProbeAction
{
    Task<ProbeResult> ExecuteAsync(HostProbe host, ProbeContext context);
}
```

### 3. Builder Pattern
Configuration building for complex objects.

```csharp
var config = new ProbeConfigurationBuilder()
    .WithContactPoints("node1", "node2")
    .WithAuthentication(username, password)
    .Build();
```

### 4. Factory Pattern
Creating appropriate probe instances.

```csharp
public interface IProbeFactory
{
    IProbeAction CreateProbe(ProbeType type);
}
```

### 5. Observer Pattern
Event-based notifications for probe results.

```csharp
public interface IProbeEventHandler
{
    Task OnProbeCompleted(ProbeResult result);
    Task OnProbeError(ProbeError error);
}
```

## Data Flow

### 1. Startup Flow
```
CLI Arguments → Configuration → DI Setup → Command Execution
```

### 2. Probe Execution Flow
```
Discovery → Host List → Probe Selection → Parallel Execution → Result Aggregation → Output
```

### 3. Scheduled Execution Flow
```
Scheduler → Job Trigger → Probe Orchestrator (reuses session) → Results → Logging/Metrics
```

### 4. Connection Recovery Flow
```
Initial Connection → Normal Operations → Connection Lost → Driver Auto-Reconnect → Log Events → Resume Operations
```

## Key Interfaces

### IProbeAction
```csharp
public interface IProbeAction
{
    string Name { get; }
    ProbeType Type { get; }
    Task<ProbeResult> ExecuteAsync(HostProbe host, ProbeContext context);
}
```

### IClusterDiscovery
```csharp
public interface IClusterDiscovery
{
    Task<ClusterTopology> DiscoverAsync(ProbeConfiguration config);
    Task<IEnumerable<HostProbe>> GetHostsAsync();
}
```

### IProbeOrchestrator
```csharp
public interface IProbeOrchestrator
{
    Task<ProbeSession> ExecuteProbesAsync(ProbeConfiguration config);
    event EventHandler<ProbeCompletedEventArgs> ProbeCompleted;
}
```

### IConnectionMonitor
```csharp
public interface IConnectionMonitor
{
    void RegisterCluster(ICluster cluster);
    ConnectionPoolStatus GetPoolStatus();
    IEnumerable<ReconnectionEvent> GetReconnectionHistory();
    event EventHandler<ConnectionStateChangedEventArgs> ConnectionStateChanged;
}

public class ConnectionPoolStatus
{
    public int TotalConnections { get; set; }
    public int ActiveConnections { get; set; }
    public int FailedHosts { get; set; }
    public Dictionary<IPEndPoint, ReconnectionInfo> ReconnectingHosts { get; set; }
}
```

## Configuration Architecture

### Configuration Sources
1. Command-line arguments (highest priority)
2. Configuration files (JSON/YAML)
3. Environment variables
4. Default values

### Configuration Model
```csharp
public class ProbeConfiguration
{
    public ConnectionSettings Connection { get; set; }
    public AuthenticationSettings Authentication { get; set; }
    public ProbeSettings Probes { get; set; }
    public LoggingSettings Logging { get; set; }
    public SchedulingSettings Scheduling { get; set; }
}
```

## Error Handling Strategy

### Exception Hierarchy
```
ProbeException (base)
├── ConnectionException
├── AuthenticationException
├── QueryExecutionException
├── ProbeTimeoutException
└── ConfigurationException
```

### Retry Policies
Using Polly for resilient operations:
- Exponential backoff for transient failures
- Circuit breaker for repeated failures
- Timeout policies for all operations

## Performance Considerations

### 1. Async/Await Throughout
- All I/O operations are async
- Parallel probe execution
- Non-blocking result aggregation

### 2. Connection Pooling and Session Management
Critical for testing driver reconnection capabilities:

**Session Persistence Architecture:**
- Single `ICluster` instance per application lifecycle
- Shared `ISession` across all probe iterations
- No manual connection management - driver handles all reconnections
- Connection pool events monitored and logged

**Connection Pool Strategy:**
```csharp
public class SessionManager
{
    private static ICluster _cluster;
    private static ISession _session;
    
    // Created once, reused forever
    public async Task<ISession> GetSessionAsync()
    {
        if (_session == null)
        {
            _cluster = CreateCluster();
            _session = await _cluster.ConnectAsync();
            RegisterEventHandlers(_cluster);
        }
        return _session;
    }
    
    private void RegisterEventHandlers(ICluster cluster)
    {
        cluster.Metadata.HostAdded += OnHostAdded;
        cluster.Metadata.HostRemoved += OnHostRemoved;
        cluster.Metadata.HostUp += OnHostUp;
        cluster.Metadata.HostDown += OnHostDown;
    }
}
```

**Reconnection Testing Focus:**
- Validates driver's automatic recovery
- No try-catch around queries for connection errors
- Let driver handle all reconnection logic
- Monitor and log all connection events
- Essential for production resilience validation

### 3. Memory Efficiency
- Streaming for large results
- Object pooling for frequent allocations
- Careful string handling

## Extensibility Points

### 1. Custom Probes
Implement `IProbeAction` to add new probe types.

### 2. Custom Loggers
Add new Serilog sinks for different outputs.

### 3. Export Formats
Implement `IResultExporter` for new formats.

### 4. Authentication Providers
Extend `IAuthenticationProvider` for custom auth.

## Testing Architecture

### Unit Testing
- Mock all external dependencies
- Test individual components in isolation
- High code coverage target (>80%)

### Integration Testing
- Docker-based Cassandra clusters
- End-to-end probe scenarios
- Performance benchmarks

### Test Doubles
- Repository pattern for data access
- Interface-based design for mocking
- Test builders for complex objects

## Security Architecture

### 1. Credential Management
- No hardcoded credentials
- Secure string handling
- Memory cleanup for sensitive data

### 2. SSL/TLS Support
- Certificate validation
- Custom certificate stores
- TLS version configuration

### 3. Audit Logging
- Security event logging
- Failed authentication tracking
- Access patterns monitoring

## Deployment Architecture

### 1. Packaging Options
- Single executable (self-contained)
- .NET tool package
- Docker container
- NuGet library

### 2. Cross-Platform Support
- Runtime checks for platform features
- Conditional compilation
- Platform-specific implementations

### 3. Configuration Management
- External configuration files
- Environment-specific settings
- Secure configuration storage

## Cassandra Version Compatibility

### Cassandra 4.1+ Optimizations

The architecture is designed to support modern Cassandra deployments while maintaining backward compatibility:

**Protocol Handling:**
- Native protocol v4 support with automatic version negotiation
- No Thrift protocol support (removed in Cassandra 4.0)
- Efficient connection pooling for CQL native protocol

**Discovery Enhancements:**
- Compatible with both legacy and modern system table structures
- Support for virtual tables in `system_views` keyspace
- Handles mixed-version clusters during upgrades

**Security Updates:**
- Client-side password hashing support
- Plugin-based authentication compatibility
- Modern SSL/TLS configurations

**Query Optimization:**
- Uses only stable system tables (`system.local`, `system.peers`)
- Avoids deprecated tables and columns
- Supports new consistency levels and query features

### Version Detection

The probe automatically detects Cassandra version and adjusts behavior:

```csharp
public interface IVersionAdapter
{
    Task<CassandraVersion> DetectVersionAsync();
    ISystemTableQuery GetSystemTableQuery(CassandraVersion version);
    IAuthenticationStrategy GetAuthStrategy(CassandraVersion version);
}
```

This ensures optimal performance and compatibility across all supported Cassandra versions (4.0+).