# Cassandra Probe C# Documentation

This directory contains comprehensive documentation for the Cassandra Probe C# project, a complete port of the Java-based [cassandra-probe](https://github.com/digitalis-io/cassandra-probe) diagnostic tool.

## Documentation Structure

### Core Documentation

- **[OVERVIEW.md](OVERVIEW.md)** - High-level project overview, purpose, and use cases
- **[FEATURES.md](FEATURES.md)** - Detailed documentation of all features and capabilities
- **[ARCHITECTURE.md](ARCHITECTURE.md)** - System architecture, design patterns, and technical decisions

### Implementation Guides

- **[IMPLEMENTATION-PLAN.md](IMPLEMENTATION-PLAN.md)** - Comprehensive implementation plan with project structure and phases
- **[IMPLEMENTATION-ROADMAP.md](IMPLEMENTATION-ROADMAP.md)** - Practical development roadmap with code examples and MVP approach

### Reference Documentation

- **[CLI-REFERENCE.md](CLI-REFERENCE.md)** - Complete command-line interface reference with examples
- **[CASSANDRA-COMPATIBILITY.md](CASSANDRA-COMPATIBILITY.md)** - Cassandra version compatibility guide and migration notes
- **[LOCAL-TESTING.md](LOCAL-TESTING.md)** - Local testing guide with Docker Compose examples for all platforms

## Quick Links

### For Developers
1. Start with [IMPLEMENTATION-ROADMAP.md](IMPLEMENTATION-ROADMAP.md) for practical setup steps
2. Review [ARCHITECTURE.md](ARCHITECTURE.md) for design principles
3. Check [IMPLEMENTATION-PLAN.md](IMPLEMENTATION-PLAN.md) for detailed phase breakdown

### For Users
1. Read [OVERVIEW.md](OVERVIEW.md) to understand the tool's purpose
2. Explore [FEATURES.md](FEATURES.md) for capability details
3. Reference [CLI-REFERENCE.md](CLI-REFERENCE.md) for command usage

## Key Features Summary

1. **Cluster Discovery** - Automatically discover all Cassandra nodes
2. **Connection Testing** - Socket, ping, and port-specific probes
3. **Query Execution** - Test CQL queries with tracing support
4. **Authentication** - Username/password and CQLSHRC file support
5. **Scheduling** - Continuous monitoring capabilities
6. **Comprehensive Logging** - Structured logs with rotation

## Technology Stack

- **.NET 6.0+** - Modern C# with async/await
- **DataStax C# Driver** - Official Cassandra driver
- **Quartz.NET** - Job scheduling
- **Serilog** - Structured logging
- **Polly** - Resilience and retry policies
- **CommandLineParser** - CLI argument parsing

## Getting Started

```bash
# Clone the repository
git clone [repository-url]
cd cassandra-probe-csharp

# Review documentation
cat docs/IMPLEMENTATION-ROADMAP.md

# Follow the setup instructions to create the solution structure
```

## Documentation Maintenance

This documentation is comprehensive and covers:
- All features from the original Java implementation
- C#-specific implementation details
- Modern .NET best practices
- Complete development workflow

Updates should maintain the same level of detail and clarity.