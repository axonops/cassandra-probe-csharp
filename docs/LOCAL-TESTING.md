# Local Testing Guide

This guide provides instructions for testing Cassandra Probe C# locally using Docker/Podman Compose with various Cassandra configurations.

## Prerequisites

- Docker or Podman with docker-compose/podman-compose installed
- .NET 6.0+ SDK (for building from source)
- Cassandra Probe executable for your platform

## Supported Cassandra Versions

The probe supports only modern Cassandra versions:
- **Cassandra 4.0** - Minimum supported version
- **Cassandra 4.1** - Recommended version
- **Cassandra 5.0** - Latest supported version

**Note**: Cassandra 3.x versions are NOT supported.

## Quick Start

### 1. Start Test Cassandra Clusters

```bash
# Clone the repository
git clone [repository-url]
cd cassandra-probe-csharp

# Start all test clusters (Docker)
docker-compose -f samples/docker-compose.yml up -d
# OR using Podman
podman-compose -f samples/docker-compose.yml up -d

# Or start specific configurations:
# Single node without auth (Docker)
docker-compose -f samples/docker-compose.yml up -d cassandra-no-auth
# OR using Podman
podman-compose -f samples/docker-compose.yml up -d cassandra-no-auth

# Single node with auth (Docker)
docker-compose -f samples/docker-compose.yml up -d cassandra-with-auth
# OR using Podman
podman-compose -f samples/docker-compose.yml up -d cassandra-with-auth

# Multi-node cluster (Docker)
docker-compose -f samples/docker-compose.yml up -d cassandra-node1 cassandra-node2 cassandra-node3
# OR using Podman
podman-compose -f samples/docker-compose.yml up -d cassandra-node1 cassandra-node2 cassandra-node3
```

### 2. Wait for Clusters to Initialize

```bash
# Check cluster status (Docker)
docker-compose -f samples/docker-compose.yml ps
# OR using Podman
podman-compose -f samples/docker-compose.yml ps

# View logs (Docker)
docker-compose -f samples/docker-compose.yml logs -f
# OR using Podman
podman-compose -f samples/docker-compose.yml logs -f

# Wait for "Created default superuser role 'cassandra'" in logs
```

## Test Scenarios

### Scenario 1: Cluster Without Authentication

This is the simplest case - no authentication or SSL required.

**macOS:**
```bash
./cassandra-probe -cp localhost:9042
```

**Windows:**
```cmd
cassandra-probe.exe -cp localhost:9042
```

**Linux:**
```bash
./cassandra-probe -cp localhost:9042
```

### Scenario 2: Cluster With Authentication

Default Cassandra credentials (username: cassandra, password: cassandra).

**macOS:**
```bash
./cassandra-probe -cp localhost:9043 -u cassandra -p cassandra
```

**Windows:**
```cmd
cassandra-probe.exe -cp localhost:9043 -u cassandra -p cassandra
```

**Linux:**
```bash
./cassandra-probe -cp localhost:9043 -u cassandra -p cassandra
```

### Scenario 3: Multi-Node Cluster Discovery

Test cluster discovery across multiple nodes.

**macOS:**
```bash
# Connect to any node - will discover all
./cassandra-probe -cp localhost:9044 --all-probes
```

**Windows:**
```cmd
REM Connect to any node - will discover all
cassandra-probe.exe -cp localhost:9044 --all-probes
```

**Linux:**
```bash
# Connect to any node - will discover all
./cassandra-probe -cp localhost:9044 --all-probes
```

### Scenario 4: Test CQL Queries

Execute test queries against the cluster.

**All Platforms:**
```bash
# Simple query without auth
./cassandra-probe -cp localhost:9042 -cql "SELECT * FROM system.local"

# Query with auth
./cassandra-probe -cp localhost:9043 -u cassandra -p cassandra \
  -cql "SELECT * FROM system.peers" -tr

# Query with consistency level
./cassandra-probe -cp localhost:9044 \
  -cql "SELECT * FROM system_schema.keyspaces" -con LOCAL_ONE
```

### Scenario 5: Continuous Monitoring

Run probes continuously with scheduling.

**macOS/Linux:**
```bash
# Probe every 30 seconds
./cassandra-probe -cp localhost:9042 -i 30

# Probe every minute for 10 minutes
./cassandra-probe -cp localhost:9042 -i 60 -d 10
```

**Windows:**
```cmd
REM Probe every 30 seconds
cassandra-probe.exe -cp localhost:9042 -i 30

REM Probe every minute for 10 minutes
cassandra-probe.exe -cp localhost:9042 -i 60 -d 10
```

### Scenario 6: Comprehensive Probe

Test all probe types with detailed logging.

**All Platforms:**
```bash
# Full probe suite
./cassandra-probe \
  -cp localhost:9042,localhost:9044,localhost:9045 \
  --all-probes \
  -cql "SELECT count(*) FROM system.local" \
  -ld ./logs \
  --verbose

# With authentication
./cassandra-probe \
  -cp localhost:9043 \
  -u cassandra -p cassandra \
  --all-probes \
  -cql "SELECT * FROM system_auth.roles" \
  -tr \
  -o json \
  -of probe-results.json
```

## Platform-Specific Execution

### macOS

```bash
# Make executable
chmod +x cassandra-probe

# Run directly
./cassandra-probe [options]

# Or via dotnet (if built from source)
dotnet run --project src/CassandraProbe.Cli -- [options]
```

### Windows

```cmd
REM Run executable
cassandra-probe.exe [options]

REM Or via dotnet (if built from source)
dotnet run --project src\CassandraProbe.Cli -- [options]

REM PowerShell
.\cassandra-probe.exe [options]
```

### Linux

```bash
# Make executable
chmod +x cassandra-probe

# Run directly
./cassandra-probe [options]

# Or via dotnet (if built from source)
dotnet run --project src/CassandraProbe.Cli -- [options]
```

## Building from Source

### All Platforms

```bash
# Clone repository
git clone [repository-url]
cd cassandra-probe-csharp

# Build
dotnet build -c Release

# Run
dotnet run --project src/CassandraProbe.Cli -- [options]

# Create platform-specific executable
# macOS
dotnet publish -c Release -r osx-x64 --self-contained

# Windows
dotnet publish -c Release -r win-x64 --self-contained

# Linux
dotnet publish -c Release -r linux-x64 --self-contained
```

## Multi-Version Testing

Test against different Cassandra versions:

```bash
# Start all versions (Docker)
docker-compose -f samples/docker-compose-multiversion.yml up -d
# OR using Podman
podman-compose -f samples/docker-compose-multiversion.yml up -d

# Test Cassandra 4.0
./cassandra-probe -cp localhost:9040

# Test Cassandra 4.1 (Recommended)
./cassandra-probe -cp localhost:9041

# Test Cassandra 5.0
./cassandra-probe -cp localhost:9050

# Test with authentication (4.1)
./cassandra-probe -cp localhost:9142 -u cassandra -p cassandra

# Compare versions
./cassandra-probe -cp localhost:9040 -cql "SELECT release_version FROM system.local"
./cassandra-probe -cp localhost:9041 -cql "SELECT release_version FROM system.local"
./cassandra-probe -cp localhost:9050 -cql "SELECT release_version FROM system.local"
```

## Container Usage

Run the probe itself in a container:

```bash
# Build container image (Docker)
docker build -t cassandra-probe .
# OR using Podman
podman build -t cassandra-probe .

# Run probe from container (Docker)
docker run --network cassandra-probe-network \
  cassandra-probe -cp cassandra-no-auth:9042
# OR using Podman
podman run --network cassandra-probe-network \
  cassandra-probe -cp cassandra-no-auth:9042

# With volume for logs (Docker)
docker run --network cassandra-probe-network \
  -v $(pwd)/logs:/app/logs \
  cassandra-probe -cp cassandra-no-auth:9042 -ld /app/logs
# OR using Podman
podman run --network cassandra-probe-network \
  -v $(pwd)/logs:/app/logs \
  cassandra-probe -cp cassandra-no-auth:9042 -ld /app/logs
```

## Troubleshooting

### Connection Refused

```bash
# Check if Cassandra is running (Docker)
docker ps
# OR using Podman
podman ps

# Check logs (Docker)
docker logs cassandra-no-auth
# OR using Podman
podman logs cassandra-no-auth

# Wait for initialization (can take 30-60 seconds)
# Docker:
docker logs cassandra-no-auth | grep "Created default superuser"
# OR Podman:
podman logs cassandra-no-auth | grep "Created default superuser"
```

### Authentication Failed

```bash
# Default credentials may not be ready immediately
# Wait and retry, or check logs:
docker logs cassandra-with-auth | grep "Created default superuser"

# Verify authentication is enabled
docker exec cassandra-with-auth cat /etc/cassandra/cassandra.yaml | grep authenticator
```

### Cannot Discover All Nodes

```bash
# Ensure all nodes are up
docker-compose -f samples/docker-compose.yml ps

# Check node status
docker exec cassandra-node1 nodetool status

# Verify inter-node communication
docker exec cassandra-node1 nodetool describecluster
```

## Test Data Setup

Create test keyspace and data:

```bash
# Connect to cluster
docker exec -it cassandra-no-auth cqlsh

# Create test keyspace
CREATE KEYSPACE IF NOT EXISTS test_probe
  WITH replication = {
    'class': 'SimpleStrategy',
    'replication_factor': 1
  };

# Create test table
USE test_probe;
CREATE TABLE IF NOT EXISTS probe_test (
    id UUID PRIMARY KEY,
    timestamp timestamp,
    value text
);

# Insert test data
INSERT INTO probe_test (id, timestamp, value) 
VALUES (uuid(), toTimestamp(now()), 'test1');
```

Then test with probe:

```bash
./cassandra-probe -cp localhost:9042 \
  -cql "SELECT * FROM test_probe.probe_test"
```

## Clean Up

```bash
# Stop all containers
docker-compose -f samples/docker-compose.yml down

# Remove volumes (deletes all data)
docker-compose -f samples/docker-compose.yml down -v

# Remove individual containers
docker stop cassandra-no-auth && docker rm cassandra-no-auth
```

## Configuration Examples

### Using Configuration File

Create `probe-config.json`:
```json
{
  "contactPoints": ["localhost:9042"],
  "probes": {
    "native": true,
    "storage": true,
    "ping": true
  },
  "logging": {
    "directory": "./logs",
    "level": "Debug"
  }
}
```

Run with config:
```bash
./cassandra-probe --config probe-config.json
```

### Environment Variables

```bash
# macOS/Linux
export CASSANDRA_CONTACT_POINTS=localhost:9042
export CASSANDRA_USERNAME=cassandra
export CASSANDRA_PASSWORD=cassandra
./cassandra-probe

# Windows
set CASSANDRA_CONTACT_POINTS=localhost:9042
set CASSANDRA_USERNAME=cassandra
set CASSANDRA_PASSWORD=cassandra
cassandra-probe.exe
```

## Performance Testing

```bash
# Benchmark connection times
./cassandra-probe -cp localhost:9042 --metrics -o csv -of metrics.csv

# Stress test with rapid probing
./cassandra-probe -cp localhost:9042 -i 1 --max-runs 100
```