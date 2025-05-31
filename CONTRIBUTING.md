# Contributing to Cassandra Probe C#

Thank you for your interest in contributing to Cassandra Probe C#! This document provides guidelines for contributing to the project.

## Getting Started

1. Fork the repository on GitHub
2. Clone your fork locally
3. Create a new branch for your feature or fix
4. Make your changes
5. Submit a pull request

## Development Setup

### Prerequisites

- .NET 9.0 SDK or later
- Git
- Your favorite C# IDE (Visual Studio, VS Code, Rider)
- Docker or Podman (for integration testing)

### Building the Project

```bash
# Clone the repository
git clone https://github.com/axonops/cassandra-probe-csharp.git
cd cassandra-probe-csharp

# Build all projects
dotnet build

# Run tests
dotnet test

# Run with coverage
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

## Code Style

### General Guidelines

- Follow C# coding conventions
- Use meaningful variable and method names
- Keep methods small and focused
- Write self-documenting code
- Add XML documentation for public APIs

### Formatting

Run the formatter before committing:
```bash
dotnet format
```

### Naming Conventions

- **Classes**: PascalCase (e.g., `SessionManager`)
- **Interfaces**: IPascalCase (e.g., `IProbeAction`)
- **Methods**: PascalCase (e.g., `ExecuteAsync`)
- **Variables**: camelCase (e.g., `connectionString`)
- **Constants**: UPPER_CASE (e.g., `DEFAULT_TIMEOUT`)
- **Private fields**: _camelCase (e.g., `_logger`)

## Testing

### Unit Tests

- Write tests for all new functionality
- Maintain or improve test coverage (target: 80%+)
- Use descriptive test names that explain what is being tested
- Follow the Arrange-Act-Assert pattern

Example:
```csharp
[Fact]
public async Task ExecuteAsync_ShouldReturnSuccess_WhenPortIsOpen()
{
    // Arrange
    var probe = new SocketProbe(logger);
    var host = new HostProbe { Address = IPAddress.Loopback };
    
    // Act
    var result = await probe.ExecuteAsync(host, context);
    
    // Assert
    result.Success.Should().BeTrue();
}
```

### Integration Tests

For tests requiring Cassandra:
```bash
# Start test container
docker run -d --name cassandra-test -p 9042:9042 cassandra:4.1

# Run integration tests
dotnet test tests/CassandraProbe.IntegrationTests
```

## Pull Request Process

1. **Update Documentation**: Update README.md and relevant docs for any API changes
2. **Add Tests**: Include unit tests for new functionality
3. **Check Tests**: Ensure all tests pass (`dotnet test`)
4. **Update CHANGELOG**: Add a note about your changes
5. **Commit Messages**: Use clear, descriptive commit messages

### Commit Message Format

```
<type>: <subject>

<body>

<footer>
```

Types:
- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation changes
- `style`: Code style changes
- `refactor`: Code refactoring
- `test`: Test additions or fixes
- `chore`: Build process or auxiliary tool changes

Example:
```
feat: Add support for custom CQL queries

- Implement CqlQueryProbe action
- Add query timeout configuration
- Support for prepared statements

Closes #123
```

## Project Structure

```
cassandra-probe-csharp/
├── src/
│   ├── CassandraProbe.Core/        # Domain models and interfaces
│   ├── CassandraProbe.Actions/     # Probe implementations
│   ├── CassandraProbe.Services/    # Business logic
│   ├── CassandraProbe.Scheduling/  # Job scheduling
│   ├── CassandraProbe.Logging/     # Output formatting
│   └── CassandraProbe.Cli/         # CLI application
├── tests/
│   ├── *.Tests/                    # Unit test projects
│   └── *.IntegrationTests/         # Integration tests
└── docs/                           # Documentation
```

## Adding New Features

### Adding a New Probe Type

1. Create interface in `Core/Interfaces/IProbeAction.cs`
2. Implement in `Actions/YourProbe.cs`
3. Register in DI container in `Cli/Program.cs`
4. Add tests in `Actions.Tests/YourProbeTests.cs`
5. Update documentation

### Adding Configuration Options

1. Update `Core/Configuration/ProbeConfiguration.cs`
2. Add command-line option in `Cli/CommandLineOptions.cs`
3. Map in `Cli/Program.cs`
4. Add tests
5. Update CLI documentation

## Debugging

### Local Debugging

1. Set breakpoints in your IDE
2. Run with debugger attached
3. Use test containers for Cassandra

### Logging

Use structured logging:
```csharp
_logger.LogInformation("Executing probe {ProbeType} for host {Host}", 
    probeType, host.Address);
```

## Performance Considerations

- Use async/await for I/O operations
- Dispose resources properly
- Consider connection pooling
- Profile performance-critical code

## Security

- Never commit credentials
- Use secure connection options
- Validate all inputs
- Follow OWASP guidelines

## Getting Help

- Check existing issues on GitHub
- Read the documentation
- Ask questions in pull requests
- Be respectful and constructive

## Code of Conduct

- Be respectful and inclusive
- Welcome newcomers
- Focus on what is best for the community
- Show empathy towards others

## License

By contributing, you agree that your contributions will be licensed under the Apache License 2.0.

## Recognition

Contributors will be recognized in the project documentation. Thank you for helping improve Cassandra Probe C#!