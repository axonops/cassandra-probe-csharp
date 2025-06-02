# Cassandra Probe C# - Overview

## Purpose

Cassandra Probe C# is a diagnostic tool for testing and monitoring Apache Cassandra cluster connectivity and performance. It's a complete port of the original Java-based [cassandra-probe](https://github.com/digitalis-io/cassandra-probe) to C# using the latest DataStax C# Driver for Cassandra.

## Key Features

### 1. Cluster Discovery
- Automatically discovers all nodes in a Cassandra cluster
- Retrieves node metadata including:
  - IP addresses (from and to addresses)
  - Datacenter and rack information
  - Cassandra version
  - Node status

### 2. Connectivity Testing
- **Socket Probe**: Tests TCP connectivity to various Cassandra ports
- **Ping Probe**: Network-level reachability test
- **Port-specific probes**:
  - Native protocol port (9042)
  - Storage/Gossip port (7000)

### 3. CQL Query Testing
- Execute test queries against the cluster
- Support for SELECT, INSERT, and UPDATE queries
- Configurable consistency levels
- Optional query tracing for performance analysis
- Comprehensive error handling and reporting

### 4. Authentication and Security Support
- **No Authentication**: Fully supports clusters without authentication
- **Username/Password**: Optional authentication when required
- **CQLSHRC File**: Parse credentials from cqlsh configuration
- **SSL/TLS**: Optional encrypted connections (not required)
- **Flexible Security**: Adapts to cluster security configuration

### 5. Scheduling and Monitoring
- Single-run mode for one-time diagnostics
- Continuous monitoring with configurable intervals
- Comprehensive logging with rotation policies
- **Session persistence**: Reuses connections to test driver recovery

## Use Cases

1. **Driver Reconnection Testing**: Test Cassandra driver's ability to recover from node failures and network disruptions
2. **Cluster Health Monitoring**: Continuously probe cluster nodes to detect connectivity issues
3. **Failover Validation**: Verify automatic failover and reconnection during rolling restarts or outages
4. **Troubleshooting**: Diagnose connection problems in restricted environments
5. **Performance Testing**: Execute test queries to measure cluster response times
6. **Network Validation**: Verify network connectivity across all cluster nodes
7. **Pre-deployment Validation**: Test cluster accessibility before application deployment

## Critical Feature: Connection Recovery Testing

One of the primary use cases for this tool is testing the Cassandra driver's reconnection capabilities. During continuous monitoring:
- The probe maintains persistent Cluster and Session objects
- Connections are reused across probe iterations
- Detailed logging captures all reconnection attempts and successes
- You can simulate node failures and observe driver recovery behavior
- Essential for validating production resilience patterns

## Architecture Overview

The application follows a modular architecture with clear separation of concerns:

- **Core Components**: Handle cluster discovery and orchestration
- **Probe Actions**: Implement different types of connectivity tests
- **Models**: Define data structures for hosts and configuration
- **Logging**: Provide structured logging with rotation
- **Scheduling**: Enable continuous monitoring capabilities

## Requirements

- For running pre-built binaries: No runtime requirements (self-contained)
- For building from source: .NET 9.0 SDK or later
- Apache Cassandra 4.0 or later (optimized for 4.1+)
- Access to at least one Cassandra contact point
- Appropriate network permissions for probing
- Optional: Cassandra credentials for authenticated clusters

## Supported Cassandra Versions

This tool is designed exclusively for modern Cassandra deployments:
- **Cassandra 4.0.x** - Fully supported
- **Cassandra 4.1.x** - Fully supported (recommended)
- **Cassandra 5.0.x** - Fully supported

**Note**: Cassandra 3.x versions are NOT supported. The probe requires features and system tables available only in Cassandra 4.0+.

## Quick Start

### Local Testing with Docker

```bash
# Start a local Cassandra 4.1 cluster without authentication
docker run -d --name cassandra-test -p 9042:9042 cassandra:4.1

# Wait for initialization (30-60 seconds)
docker logs cassandra-test | grep "Created default superuser"

# Run probe
./cassandra-probe --contact-points localhost:9042
```

### Download and Run

The probe ships as a self-contained executable for all major platforms:

**macOS:**
```bash
curl -LO [release-url]/cassandra-probe-macos
chmod +x cassandra-probe-macos
./cassandra-probe-macos --contact-points your-cassandra-host:9042
```

**Windows:**
```powershell
Invoke-WebRequest -Uri [release-url]/cassandra-probe-windows.exe -OutFile cassandra-probe.exe
.\cassandra-probe.exe --contact-points your-cassandra-host:9042
```

**Linux:**
```bash
wget [release-url]/cassandra-probe-linux
chmod +x cassandra-probe-linux
./cassandra-probe-linux --contact-points your-cassandra-host:9042
```

For detailed testing scenarios and Docker Compose configurations, see [LOCAL-TESTING.md](LOCAL-TESTING.md).