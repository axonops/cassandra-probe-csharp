# Cassandra Probe C# - Implementation Plan

## Project Structure

```
CassandraProbe/
├── src/
│   ├── CassandraProbe.Core/           # Core domain logic
│   │   ├── Models/
│   │   │   ├── HostProbe.cs
│   │   │   ├── ProbeResult.cs
│   │   │   └── ClusterTopology.cs
│   │   ├── Interfaces/
│   │   │   ├── IProbeAction.cs
│   │   │   ├── IClusterDiscovery.cs
│   │   │   └── IProbeOrchestrator.cs
│   │   ├── Configuration/
│   │   │   ├── ProbeConfiguration.cs
│   │   │   └── CqlshrcParser.cs
│   │   └── Exceptions/
│   │       └── ProbeException.cs
│   │
│   ├── CassandraProbe.Actions/        # Probe implementations
│   │   ├── SocketProbe.cs
│   │   ├── PingProbe.cs
│   │   ├── CqlQueryProbe.cs
│   │   └── PortSpecificProbes/
│   │       ├── NativePortProbe.cs
│   │       └── StoragePortProbe.cs
│   │
│   ├── CassandraProbe.Services/       # Service layer
│   │   ├── ClusterDiscoveryService.cs
│   │   ├── ProbeOrchestrator.cs
│   │   ├── AuthenticationService.cs
│   │   └── MetricsCollector.cs
│   │
│   ├── CassandraProbe.Scheduling/     # Scheduling logic
│   │   ├── ProbeJob.cs
│   │   ├── JobScheduler.cs
│   │   └── QuartzConfiguration.cs
│   │
│   ├── CassandraProbe.Logging/        # Logging infrastructure
│   │   ├── ProbeLogger.cs
│   │   ├── LoggerConfiguration.cs
│   │   └── Formatters/
│   │       ├── JsonFormatter.cs
│   │       └── CsvFormatter.cs
│   │
│   └── CassandraProbe.Cli/            # Console application
│       ├── Program.cs
│       ├── CommandLineOptions.cs
│       ├── Commands/
│       │   ├── ProbeCommand.cs
│       │   └── ScheduleCommand.cs
│       └── DependencyInjection/
│           └── ServiceConfiguration.cs
│
├── tests/
│   ├── CassandraProbe.Core.Tests/
│   ├── CassandraProbe.Actions.Tests/
│   ├── CassandraProbe.Services.Tests/
│   └── CassandraProbe.Integration.Tests/
│
├── docs/
├── samples/
│   ├── cqlshrc.sample
│   ├── appsettings.json
│   └── docker-compose.yml
└── build/
    └── Docker/
        └── Dockerfile
```

## Implementation Phases

### Phase 1: Core Foundation (Week 1-2)

#### 1.1 Project Setup
- [x] Create solution structure
- [ ] Set up projects with dependencies
- [ ] Configure build pipeline
- [ ] Add NuGet packages:
  - CassandraCSharpDriver (latest)
  - Quartz.NET
  - Serilog
  - CommandLineParser
  - YamlDotNet
  - Polly (for retry logic)

#### 1.2 Core Models and Interfaces
- [ ] Implement HostProbe model
- [ ] Create ProbeResult hierarchy
- [ ] Define IProbeAction interface
- [ ] Create configuration models
- [ ] Implement exception types

#### 1.3 Configuration Management
- [ ] Command-line argument parser
- [ ] Configuration file support (JSON/YAML)
- [ ] CQLSHRC parser implementation
- [ ] Environment variable support

### Phase 2: Cluster Discovery and Connection Management (Week 2-3)

#### 2.1 Connection Management (Critical for Reconnection Testing)
- [ ] Implement singleton SessionManager for persistent connections
- [ ] Create cluster only once, reuse throughout application lifecycle
- [ ] Register connection event handlers (HostUp, HostDown, etc.)
- [ ] Implement comprehensive connection logging
- [ ] Add SSL/TLS support
- [ ] Authentication handling
- [ ] NO manual reconnection logic - let driver handle it

#### 2.2 Connection Monitoring
- [ ] Implement IConnectionMonitor interface
- [ ] Track all reconnection attempts with timestamps
- [ ] Log connection pool state changes
- [ ] Monitor and report reconnection success/failure rates
- [ ] Capture reconnection duration metrics
- [ ] Create event stream for connection state changes

#### 2.3 Discovery Service
- [ ] Implement ClusterDiscoveryService
- [ ] Query system tables for topology
- [ ] Parse node metadata
- [ ] Handle multi-datacenter scenarios
- [ ] Add retry logic with Polly for discovery only

### Phase 3: Probe Implementations (Week 3-4)

#### 3.1 Socket Probe
- [ ] Implement async socket connections
- [ ] Add timeout handling
- [ ] Measure connection timing
- [ ] Implement retry logic

#### 3.2 Ping Probe
- [ ] Implement ICMP ping (where supported)
- [ ] Add TCP ping fallback
- [ ] Async implementation
- [ ] Platform-specific handling

#### 3.3 CQL Query Probe
- [ ] Query validation
- [ ] Prepared statement support
- [ ] Consistency level configuration
- [ ] Query tracing implementation
- [ ] Result set handling

#### 3.4 Port-Specific Probes
- [ ] Native port probe
- [ ] Storage port probe

### Phase 4: Orchestration and Logging (Week 4-5)

#### 4.1 Probe Orchestrator
- [ ] Implement ProbeOrchestrator
- [ ] Parallel probe execution
- [ ] Result aggregation
- [ ] Error handling and recovery

#### 4.2 Logging System (Focus on Connection Events)
- [ ] Serilog configuration with correlation IDs
- [ ] Dedicated logger for connection events
- [ ] Structured logging with event types:
  - ConnectionEstablished
  - ConnectionLost
  - ReconnectionAttempt
  - ReconnectionSuccess
  - ReconnectionFailure
  - HostStatusChange
- [ ] File sink with rotation
- [ ] Custom formatters (JSON, CSV)
- [ ] Real-time console output for connection events
- [ ] Connection event aggregation and reporting

#### 4.3 Metrics Collection
- [ ] Probe execution metrics
- [ ] Success/failure tracking
- [ ] Performance statistics
- [ ] Export capabilities

### Phase 5: Scheduling and CLI (Week 5-6)

#### 5.1 Quartz.NET Integration (With Session Persistence)
- [ ] Job implementation with shared SessionManager
- [ ] Scheduler configuration
- [ ] Ensure single Cluster/Session across all job executions
- [ ] Pass session via JobDataMap or DI
- [ ] Cron expression support
- [ ] Job persistence
- [ ] Prevent session recreation between jobs

#### 5.2 CLI Application
- [ ] Command-line parser setup
- [ ] Command implementations
- [ ] Dependency injection setup
- [ ] Graceful shutdown handling

#### 5.3 Output and Reporting
- [ ] Console output formatting
- [ ] Report generation
- [ ] Export commands
- [ ] Real-time monitoring display

### Phase 6: Testing and Documentation (Week 6-7)

#### 6.1 Unit Tests
- [ ] Core logic tests
- [ ] Probe action tests
- [ ] Service layer tests
- [ ] Configuration parsing tests

#### 6.2 Integration Tests
- [ ] Docker-based Cassandra setup
- [ ] Full probe workflow tests
- [ ] Authentication scenarios
- [ ] Failure scenario tests

#### 6.3 Documentation
- [ ] API documentation
- [ ] User guide
- [ ] Configuration reference
- [ ] Troubleshooting guide

### Phase 7: Advanced Features (Week 7-8)

#### 7.1 Performance Optimization
- [ ] Connection pooling optimization
- [ ] Async/await optimization
- [ ] Memory usage profiling
- [ ] Batch operation support

#### 7.2 Extended Features
- [ ] Custom probe plugins
- [ ] Web API endpoint (optional)
- [ ] Prometheus metrics export
- [ ] Health check endpoints

#### 7.3 Deployment
- [ ] Docker container
- [ ] Release packages (.NET tool)
- [ ] CI/CD pipeline
- [ ] Cross-platform testing

## Technology Stack

### Core Dependencies
- **Cassandra Driver**: DataStax C# Driver for Apache Cassandra (latest)
- **Scheduling**: Quartz.NET 3.x
- **Logging**: Serilog 3.x with sinks
- **CLI**: CommandLineParser 2.x
- **Configuration**: Microsoft.Extensions.Configuration
- **DI Container**: Microsoft.Extensions.DependencyInjection
- **HTTP Client**: System.Net.Http with Polly
- **Testing**: xUnit, Moq, FluentAssertions

### Development Tools
- **.NET SDK**: 6.0 or later
- **IDE**: Visual Studio 2022 / VS Code / Rider
- **Docker**: For integration testing
- **Git**: Version control

## Key Implementation Considerations

### 1. Async-First Design
- All I/O operations should be async
- Use `ConfigureAwait(false)` for library code
- Implement cancellation token support
- Avoid blocking calls

### 2. Dependency Injection
- Use Microsoft.Extensions.DependencyInjection
- Register all services and probes
- Support for custom probe registration
- Configuration through IOptions<T>

### 3. Error Handling
- Specific exception types for different failures
- Retry policies with Polly
- Circuit breaker for failing nodes
- Comprehensive error logging

### 4. Performance
- Connection pooling optimization
- Parallel probe execution
- Efficient memory usage
- Minimal allocations in hot paths

### 5. Cross-Platform Support
- Test on Windows, Linux, macOS
- Handle platform-specific features gracefully
- Docker support for all platforms
- Platform-specific installation guides

### 6. Security
- No hardcoded credentials
- Secure credential storage
- SSL/TLS best practices
- Audit logging for sensitive operations

## Success Criteria

1. **Feature Parity**: All features from Java version implemented
2. **Performance**: Equal or better performance than Java version
3. **Reliability**: Comprehensive error handling and recovery
4. **Usability**: Clear documentation and intuitive CLI
5. **Maintainability**: Clean architecture, high test coverage
6. **Cross-Platform**: Runs on Windows, Linux, macOS
7. **Modern C#**: Uses latest C# features and best practices

## Deliverables

1. **Source Code**: Complete C# implementation
2. **Documentation**: User guide, API docs, configuration reference
3. **Tests**: Unit and integration test suite
4. **Packages**: NuGet package, .NET tool, Docker image
5. **Examples**: Sample configurations and usage scenarios
6. **CI/CD**: Automated build and release pipeline