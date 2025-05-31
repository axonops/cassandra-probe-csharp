# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Cassandra Probe C# is a comprehensive port of the Java-based [cassandra-probe](https://github.com/digitalis-io/cassandra-probe) diagnostic tool. It provides connectivity testing, performance monitoring, and cluster discovery for Apache Cassandra databases.

## Architecture

The project follows a clean, modular architecture:

```
CassandraProbe/
├── src/
│   ├── CassandraProbe.Core/        # Domain models, interfaces, configuration
│   ├── CassandraProbe.Actions/     # Probe implementations (Socket, Ping, CQL)
│   ├── CassandraProbe.Services/    # Business logic (Discovery, Orchestration)
│   ├── CassandraProbe.Scheduling/  # Quartz.NET job scheduling
│   ├── CassandraProbe.Logging/     # Serilog logging infrastructure
│   └── CassandraProbe.Cli/         # Console application entry point
└── tests/
    ├── CassandraProbe.Core.Tests/
    ├── CassandraProbe.Actions.Tests/
    └── CassandraProbe.Integration.Tests/
```

## Key Features to Implement

1. **Cluster Discovery**: Enumerate all nodes using system tables
2. **Connection Probes**: Socket, Ping, Port-specific tests
3. **CQL Query Testing**: Execute queries with tracing support
4. **Authentication**: Username/password and CQLSHRC file support
5. **Scheduling**: Continuous monitoring with Quartz.NET
6. **Logging**: Structured logging with rotation

## Development Commands

```bash
# Initial setup
dotnet new sln -n CassandraProbe
dotnet restore

# Build
dotnet build                    # Debug build
dotnet build -c Release        # Release build

# Test
dotnet test                    # Run all tests
dotnet test --filter "FullyQualifiedName~Unit"      # Unit tests only
dotnet test --filter "FullyQualifiedName~Integration" # Integration tests

# Run
dotnet run --project src/CassandraProbe.Cli -- -cp localhost -u cassandra -p cassandra

# Package
dotnet pack -c Release
dotnet publish -c Release -r linux-x64 --self-contained

# Code Quality
dotnet format              # Format code
dotnet build -warnaserror  # Treat warnings as errors
```

## Key Implementation Details

### Async Pattern
All I/O operations must be async:
```csharp
public async Task<ProbeResult> ExecuteAsync(HostProbe host, CancellationToken ct)
```

### Dependency Injection
Use Microsoft.Extensions.DependencyInjection throughout:
```csharp
services.AddScoped<IProbeOrchestrator, ProbeOrchestrator>();
```

### Error Handling
Use Polly for resilience:
```csharp
var retryPolicy = Policy
    .Handle<SocketException>()
    .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
```

### Configuration
Support multiple sources (CLI args, files, environment):
```csharp
services.Configure<ProbeConfiguration>(configuration.GetSection("Probe"));
```

## Testing Strategy

1. **Unit Tests**: Mock external dependencies, test business logic
2. **Integration Tests**: Use Docker for real Cassandra instances
3. **Performance Tests**: Benchmark probe execution times

### Docker Commands for Testing
```bash
# Start test Cassandra cluster
docker-compose -f tests/docker-compose.yml up -d

# Run integration tests
dotnet test tests/CassandraProbe.Integration.Tests

# Cleanup
docker-compose -f tests/docker-compose.yml down
```

## Important Conventions

1. **Naming**: Use clear, descriptive names (e.g., `ProbeOrchestrator`, not `Orchestrator`)
2. **Async**: All async methods end with `Async`
3. **Cancellation**: Always accept `CancellationToken` in async methods
4. **Logging**: Use structured logging with appropriate levels
5. **Exceptions**: Create specific exception types in `Core/Exceptions`

## Dependencies and Versions

- **Target Framework**: .NET 6.0 or later
- **Cassandra Driver**: CassandraCSharpDriver 3.18.0+
- **Scheduling**: Quartz.NET 3.8.0+
- **Logging**: Serilog 3.1.1+
- **CLI**: CommandLineParser 2.9.1+
- **Testing**: xUnit 2.4.2+, Moq 4.20.0+

## Common Tasks

### Adding a New Probe Type
1. Create class implementing `IProbeAction` in `CassandraProbe.Actions`
2. Register in DI container in `ServiceConfiguration.cs`
3. Add command-line option in `CommandLineOptions.cs`
4. Add unit tests in `CassandraProbe.Actions.Tests`

### Modifying Configuration
1. Update `ProbeConfiguration.cs` model
2. Update `CommandLineOptions.cs` for CLI support
3. Update `appsettings.json` sample
4. Document in `CLI-REFERENCE.md`

## Performance Considerations

- Use `ValueTask` for hot paths
- Minimize allocations with object pooling
- Use `ConfigureAwait(false)` in library code
- Profile with BenchmarkDotNet

## Security Notes

- Never log sensitive information (passwords, connection strings)
- Use SecureString for password handling where possible
- Validate all input, especially CQL queries
- Support SSL/TLS for all connections