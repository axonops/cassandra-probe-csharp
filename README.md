# Cassandra Probe C#

A comprehensive diagnostic and monitoring tool for Apache Cassandra clusters, written in C#. This is a complete port of the original Java-based [cassandra-probe](https://github.com/digitalis-io/cassandra-probe) with modern .NET features and enhanced capabilities.

## 🚀 Quick Start

```bash
# Test with Docker or Podman (no authentication)
# Using Docker:
docker run -d --name cassandra -p 9042:9042 cassandra:4.1
# OR using Podman:
podman run -d --name cassandra -p 9042:9042 cassandra:4.1

./cassandra-probe --contact-points localhost:9042

# Test with authentication
./cassandra-probe --contact-points localhost:9042 -u cassandra -p cassandra

# Run all probes
./cassandra-probe --contact-points localhost:9042 --all-probes
```

## 📋 Requirements

- **Cassandra**: 4.0 or later (4.1 recommended)
- **.NET Runtime**: Not required (self-contained executable)
- **Platform**: Windows, macOS, or Linux

**Important**: Cassandra 3.x versions are NOT supported. The probe requires features available only in Cassandra 4.0+.

## ✨ Features

- **Driver Reconnection Testing**: Test Cassandra driver's automatic recovery from node failures
- **Cluster Discovery**: Automatically discover all nodes in the cluster
- **Connection Persistence**: Maintains session across probes to validate reconnection behavior
- **Connectivity Testing**: Socket, ping, and port-specific probes
- **CQL Query Testing**: Execute queries with tracing and consistency control
- **Flexible Authentication**: Supports clusters with or without authentication
- **SSL/TLS Support**: Optional encrypted connections
- **Continuous Monitoring**: Schedule probes at regular intervals with session reuse
- **Reconnection Logging**: Detailed logs of all connection events and recovery attempts
- **Cross-Platform**: Native executables for Windows, macOS, and Linux

## 🔄 Primary Use Case: Testing Driver Resilience

One of the main purposes of this tool is to validate the Cassandra driver's reconnection capabilities:
- Maintains persistent connections across all probe iterations
- Monitors and logs all reconnection attempts and successes
- Helps validate production failover scenarios
- Essential for testing rolling restarts and network disruptions

## 📦 Installation

### Download Pre-built Executables

Download the latest release for your platform from the [Releases](https://github.com/axonops/cassandra-probe-csharp/releases) page:

- **Windows**: `cassandra-probe-win-x64.exe`
- **macOS (Intel)**: `cassandra-probe-osx-x64`
- **macOS (Apple Silicon)**: `cassandra-probe-osx-arm64`
- **Linux (x64)**: `cassandra-probe-linux-x64`
- **Linux (ARM64)**: `cassandra-probe-linux-arm64`

### Build from Source

#### Prerequisites

- .NET 9.0 SDK or later ([Download](https://dotnet.microsoft.com/download))
- Git

#### Platform-Specific Build Instructions

##### **Windows**

```powershell
# Clone the repository
git clone https://github.com/axonops/cassandra-probe-csharp.git
cd cassandra-probe-csharp

# Build for Windows
dotnet publish src/CassandraProbe.Cli -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish/windows

# The executable will be at: publish/windows/cassandra-probe.exe
```

##### **macOS**

```bash
# Clone the repository
git clone https://github.com/axonops/cassandra-probe-csharp.git
cd cassandra-probe-csharp

# Build for macOS (Intel)
dotnet publish src/CassandraProbe.Cli -c Release -r osx-x64 --self-contained -p:PublishSingleFile=true -o publish/macos-intel

# Build for macOS (Apple Silicon)
dotnet publish src/CassandraProbe.Cli -c Release -r osx-arm64 --self-contained -p:PublishSingleFile=true -o publish/macos-arm64

# The executable will be at: publish/macos-*/cassandra-probe
# Make it executable
chmod +x publish/macos-*/cassandra-probe
```

##### **Debian/Ubuntu Linux**

```bash
# Install .NET SDK
wget https://packages.microsoft.com/config/ubuntu/$(lsb_release -rs)/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb
sudo apt-get update && sudo apt-get install -y dotnet-sdk-9.0

# Clone and build
git clone https://github.com/axonops/cassandra-probe-csharp.git
cd cassandra-probe-csharp

# Build for Linux x64
dotnet publish src/CassandraProbe.Cli -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true -o publish/linux

# Build for Linux ARM64 (for ARM-based systems)
dotnet publish src/CassandraProbe.Cli -c Release -r linux-arm64 --self-contained -p:PublishSingleFile=true -o publish/linux-arm64

# Make executable
chmod +x publish/linux*/cassandra-probe
```

##### **RHEL/CentOS/Fedora Linux**

```bash
# Install .NET SDK
sudo dnf install dotnet-sdk-9.0

# Clone and build
git clone https://github.com/axonops/cassandra-probe-csharp.git
cd cassandra-probe-csharp

# Build for Linux x64
dotnet publish src/CassandraProbe.Cli -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true -o publish/linux

# Make executable
chmod +x publish/linux/cassandra-probe
```

#### Build Options

- **Self-contained**: Includes .NET runtime (no installation required on target machine)
- **Framework-dependent**: Smaller file size but requires .NET runtime installed

```bash
# Framework-dependent build (smaller size)
dotnet publish src/CassandraProbe.Cli -c Release -r [RID] --self-contained false -p:PublishSingleFile=true

# Available Runtime Identifiers (RID):
# - win-x64, win-x86, win-arm64
# - osx-x64, osx-arm64
# - linux-x64, linux-arm64, linux-musl-x64
```

## 🔧 Usage Examples

### Basic Connectivity Test
```bash
# No authentication required
./cassandra-probe --contact-points cassandra-host:9042

# With authentication
./cassandra-probe --contact-points cassandra-host:9042 -u username -p password
```

### Run Test Queries
```bash
# Simple query
./cassandra-probe --contact-points localhost:9042 --test-cql "SELECT * FROM system.local"

# Query with tracing
./cassandra-probe --contact-points localhost:9042 --test-cql "SELECT * FROM system.peers" --tracing
```

### Continuous Monitoring
```bash
# Probe every 30 seconds
./cassandra-probe --contact-points localhost:9042 -i 30

# Run for 10 minutes
./cassandra-probe --contact-points localhost:9042 -i 60 -d 10
```

### Multi-Node Discovery
```bash
# Connect to any node - discovers all
./cassandra-probe --contact-points node1:9042,node2:9042,node3:9042 --all-probes
```

### Test Driver Reconnection
```bash
# Start continuous monitoring to test reconnection
./cassandra-probe --contact-points cluster:9042 -i 5 --connection-events

# In another terminal, stop a Cassandra node
# Watch the probe logs show reconnection attempts and success
# The driver should automatically reconnect when the node returns
```

## 🧪 Local Testing

Use the included quickstart script:

```bash
# Unix/macOS
./quickstart.sh

# Windows
powershell -ExecutionPolicy Bypass -File quickstart.ps1
```

Or use Docker Compose:

```bash
# Start test clusters
docker-compose -f samples/docker-compose.yml up -d

# Test different versions
docker-compose -f samples/docker-compose-multiversion.yml up -d
```

## 📚 Documentation

- [Overview](docs/OVERVIEW.md) - Project overview and capabilities
- [Features](docs/FEATURES.md) - Detailed feature documentation
- [CLI Reference](docs/CLI-REFERENCE.md) - Complete command-line options
- [Local Testing](docs/LOCAL-TESTING.md) - Testing guide with examples
- [Architecture](docs/ARCHITECTURE.md) - System design and patterns
- [Cassandra Compatibility](docs/CASSANDRA-COMPATIBILITY.md) - Version compatibility guide

## 🏗️ Architecture

The probe follows a clean, modular architecture:

```
CassandraProbe/
├── Core/        # Domain models and interfaces
├── Actions/     # Probe implementations
├── Services/    # Business logic
├── Scheduling/  # Job scheduling
├── Logging/     # Structured logging
└── CLI/         # Command-line interface
```

## 📦 Software Bill of Materials (SBOM)

Every release includes Software Bill of Materials (SBOM) files in CycloneDX format:
- JSON format: `cassandra-probe-<version>-sbom.json`
- XML format: `cassandra-probe-<version>-sbom.xml`

These files provide a complete inventory of all dependencies used in the project, including:
- Direct and transitive dependencies
- Version information
- License details
- Security vulnerability tracking

### Generating SBOM Locally

To generate an SBOM for the current source:

```bash
# Install CycloneDX tool
dotnet tool install --global CycloneDX

# Generate SBOM in JSON format
dotnet CycloneDX src/CassandraProbe.Cli/CassandraProbe.Cli.csproj -o . -f json

# Generate SBOM in XML format
dotnet CycloneDX src/CassandraProbe.Cli/CassandraProbe.Cli.csproj -o . -f xml
```

### Key Dependencies

The project uses the following major dependencies:
- **DataStax C# Driver for Apache Cassandra** - Core database connectivity
- **Polly** - Resilience and retry policies
- **Serilog** - Structured logging
- **Quartz.NET** - Job scheduling
- **CommandLineParser** - CLI argument parsing
- **YamlDotNet** - YAML configuration support

For a complete list, refer to the SBOM files in the release artifacts.

## 🤝 Contributing

See [IMPLEMENTATION-PLAN.md](docs/IMPLEMENTATION-PLAN.md) and [IMPLEMENTATION-ROADMAP.md](docs/IMPLEMENTATION-ROADMAP.md) for development guidelines.

## 📄 License

This project is licensed under the Apache License 2.0 - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

This project is a C# port of the original [cassandra-probe](https://github.com/digitalis-io/cassandra-probe) by Digitalis.IO.