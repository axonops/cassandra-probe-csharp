# Testing Guide

## Overview

The Cassandra Probe C# project follows a comprehensive testing strategy to ensure code quality and reliability. This guide covers the testing approach, test organization, and how to run tests.

## Test Organization

Tests are organized into separate projects by component:

```
tests/
├── CassandraProbe.Core.Tests/       # Core models and utilities
├── CassandraProbe.Services.Tests/   # Service layer tests
├── CassandraProbe.Actions.Tests/    # Probe action tests
├── CassandraProbe.Scheduling.Tests/ # Scheduling component tests
├── CassandraProbe.Cli.Tests/        # CLI and parsing tests
└── CassandraProbe.TestHelpers/      # Shared test utilities
```

## Test Coverage Goals

- **Target**: 80% code coverage across all projects
- **Critical Components**: 90%+ coverage for:
  - SessionManager (connection persistence)
  - ConnectionMonitor (reconnection tracking)
  - ProbeOrchestrator (execution logic)
  - All probe actions

## Running Tests

### Quick Start

```bash
# Run all tests
./run-tests.sh

# Run specific project tests
dotnet test tests/CassandraProbe.Core.Tests

# Run with coverage
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

### Visual Studio / VS Code

- Open Test Explorer
- Run all tests or specific test projects
- Coverage visualization available with appropriate extensions

### Command Line Options

```bash
# Verbose output
dotnet test --logger "console;verbosity=detailed"

# Filter tests by name
dotnet test --filter "FullyQualifiedName~SocketProbe"

# Run tests in parallel
dotnet test --parallel
```

## Test Categories

### Unit Tests

Pure unit tests with mocked dependencies:

```csharp
[Fact]
public async Task ExecuteAsync_ShouldSucceedWhenPortIsOpen()
{
    // Arrange
    var host = new HostProbe { Address = IPAddress.Loopback };
    var context = new ProbeContext();
    
    // Act
    var result = await _probe.ExecuteAsync(host, context);
    
    // Assert
    result.Success.Should().BeTrue();
}
```

### Integration Tests

Tests that verify component interaction:

```csharp
[Fact]
public async Task ProbeOrchestrator_ShouldExecuteAllEnabledProbes()
{
    // Tests actual probe execution flow
}
```

### Mock Strategies

#### Session Mocking

```csharp
var mockSession = new Mock<ISession>();
mockSession.Setup(x => x.ExecuteAsync(It.IsAny<IStatement>()))
    .ReturnsAsync(mockRowSet.Object);
```

#### Connection Event Simulation

```csharp
_connectionMonitor.RecordHostDown(address, "Connection lost");
_connectionMonitor.RecordReconnectionAttempt(address);
_connectionMonitor.RecordReconnectionSuccess(address);
```

## Test Data Builders

Use test builders for consistent test data:

```csharp
// Create test hosts
var host = TestHostBuilder.CreateHost("10.0.0.1", status: HostStatus.Up);
var cluster = TestHostBuilder.CreateHostCluster(5);

// Create test configuration
var config = TestConfigurationBuilder.CreateWithAllProbes();
```

## Testing Reconnection Logic

Critical tests for driver reconnection monitoring:

```csharp
[Fact]
public void ConnectionMonitor_ShouldTrackReconnectionEvents()
{
    // Simulate connection failure and recovery
    _monitor.RecordHostDown(address, "Network error");
    _monitor.RecordReconnectionAttempt(address);
    _monitor.RecordReconnectionSuccess(address);
    
    // Verify state transitions
    var history = _monitor.GetReconnectionHistory();
    history.Should().HaveCount(3);
}
```

## Performance Testing

For probe timeout validation:

```csharp
[Fact]
public async Task SocketProbe_ShouldRespectTimeout()
{
    var stopwatch = Stopwatch.StartNew();
    var result = await _probe.ExecuteAsync(host, context);
    stopwatch.Stop();
    
    stopwatch.ElapsedMilliseconds.Should().BeLessThan(500);
}
```

## Test Fixtures and Cleanup

Proper resource management:

```csharp
public class SocketProbeTests : IDisposable
{
    private TcpListener? _listener;
    
    public void Dispose()
    {
        _listener?.Stop();
    }
}
```

## Continuous Integration

Tests are designed to run in CI environments:

- No hardcoded paths
- No external dependencies
- Configurable timeouts
- Clean test isolation

## Debugging Tests

### Enable Detailed Logging

```csharp
var loggerMock = new Mock<ILogger<ProbeJob>>();
loggerMock.Setup(x => x.Log(/*...*/))
    .Callback<LogLevel, EventId, object, Exception, Func<object, Exception, string>>(
        (level, eventId, state, exception, formatter) => 
        {
            Console.WriteLine($"[{level}] {formatter(state, exception)}");
        });
```

### Test Output

```csharp
public class MyTests
{
    private readonly ITestOutputHelper _output;
    
    public MyTests(ITestOutputHelper output)
    {
        _output = output;
    }
    
    [Fact]
    public void Test()
    {
        _output.WriteLine("Debug information");
    }
}
```

## Common Test Patterns

### Async Testing

```csharp
[Fact]
public async Task AsyncMethod_ShouldCompleteSuccessfully()
{
    // Always use async/await for async methods
    var result = await _service.ExecuteAsync();
    result.Should().NotBeNull();
}
```

### Exception Testing

```csharp
[Fact]
public async Task Method_ShouldThrowOnInvalidInput()
{
    var act = async () => await _service.ExecuteAsync(null);
    
    await act.Should().ThrowAsync<ArgumentNullException>()
        .WithMessage("*cannot be null*");
}
```

### Parameterized Tests

```csharp
[Theory]
[InlineData("ALL", 45)]
[InlineData("QUORUM", 30)]
[InlineData("ONE", 10)]
public void ProbeContext_ShouldHandleConsistencyLevels(
    string level, int timeout)
{
    var context = new ProbeContext
    {
        ConsistencyLevel = level,
        QueryTimeout = TimeSpan.FromSeconds(timeout)
    };
    
    context.ConsistencyLevel.Should().Be(level);
}
```

## Test Maintenance

### Adding New Tests

1. Create test file in appropriate project
2. Follow naming convention: `{ClassName}Tests.cs`
3. Use AAA pattern: Arrange, Act, Assert
4. Include both positive and negative test cases

### Test Review Checklist

- [ ] Tests are independent and can run in any order
- [ ] No hardcoded values that might break in different environments
- [ ] Proper cleanup of resources
- [ ] Clear test names that describe what is being tested
- [ ] Both success and failure paths covered
- [ ] Edge cases handled

## Troubleshooting

### Common Issues

1. **Port Already in Use**
   ```csharp
   private static int GetAvailablePort()
   {
       using var listener = new TcpListener(IPAddress.Loopback, 0);
       listener.Start();
       var port = ((IPEndPoint)listener.LocalEndpoint).Port;
       listener.Stop();
       return port;
   }
   ```

2. **Flaky Async Tests**
   - Use proper timeouts
   - Avoid `Task.Delay` in tests
   - Use `TaskCompletionSource` for synchronization

3. **Mock Setup Issues**
   - Verify mock setup matches actual usage
   - Use `It.IsAny<T>()` for flexible matching
   - Check callback parameters

## Coverage Reports

Generate detailed coverage reports:

```bash
# Install report generator
dotnet tool install -g dotnet-reportgenerator-globaltool

# Generate HTML report
reportgenerator \
    -reports:"**/coverage.cobertura.xml" \
    -targetdir:"coveragereport" \
    -reporttypes:Html
```

## Best Practices

1. **Test Isolation**: Each test should be independent
2. **Clear Names**: Test names should describe the scenario
3. **Single Assertion**: Prefer one logical assertion per test
4. **Mock Boundaries**: Mock external dependencies, not internal components
5. **Test Data**: Use builders for complex test data
6. **Async Patterns**: Always await async calls
7. **Resource Cleanup**: Implement IDisposable when needed
8. **Readable Assertions**: Use FluentAssertions for clarity

## Next Steps

- Set up CI/CD pipeline with test execution
- Add performance benchmarks for critical paths
- Implement integration tests with Docker Cassandra
- Add mutation testing for quality validation