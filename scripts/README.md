# CassandraProbe Scripts

This directory contains demonstration and testing scripts for the CassandraProbe resilient client functionality.

## Script Organization

### 🚀 Quick Start Demos

#### **stable-demo.sh** (Recommended)
The most reliable demonstration script using docker-compose for proper container orchestration.
```bash
./stable-demo.sh
```
- Uses docker-compose for container management
- Runs single Cassandra node on port 19042
- Shows side-by-side comparison of standard vs resilient client
- Includes health checks and proper startup sequencing

#### **simple-stable-demo.sh**
Alternative demo without docker-compose dependency.
```bash
./simple-stable-demo.sh
```
- Works with Docker or Podman directly
- No docker-compose required
- Good for environments without compose

#### **test-resilient.sh**
Minimal test script for existing Cassandra installations.
```bash
./test-resilient.sh
```
- Assumes Cassandra is already running on localhost:9042
- Quick way to test the resilient client functionality

### 🧪 Advanced Testing Scripts

#### **test-resilient-recovery-scenarios.sh**
Comprehensive test suite demonstrating automatic recovery in various failure scenarios.
```bash
# Run all scenarios
./test-resilient-recovery-scenarios.sh --all

# Run specific scenario
./test-resilient-recovery-scenarios.sh 1  # Single node failure
./test-resilient-recovery-scenarios.sh 2  # Complete cluster outage
./test-resilient-recovery-scenarios.sh 3  # Network issues
./test-resilient-recovery-scenarios.sh 4  # Rolling restart
```

#### **test-resilient-client-rolling-restart.sh**
Demonstrates resilient client behavior during a rolling restart maintenance window.
```bash
./test-resilient-client-rolling-restart.sh
```
- Creates 3-node cluster
- Performs rolling restart with 10-second maintenance window per node
- Shows continuous query execution throughout

#### **test-recovery-scenarios.sh**
Multi-datacenter recovery testing with various failure scenarios.
```bash
./test-recovery-scenarios.sh
```
- Tests multi-DC setups
- Network partition scenarios
- DC-level failures

### 🛠️ Utility Scripts

#### **container-runtime.sh**
Shared library for container runtime detection (Docker/Podman).
- Automatically sourced by other scripts
- Detects and configures appropriate container runtime

#### **container-helper.sh**
Helper functions for container management.
- Container cleanup prompts
- Existing container detection
- Shared utility functions

#### **cleanup-all.sh**
Cleanup utility to remove all Cassandra-related containers.
```bash
./cleanup-all.sh
```
- Finds and removes all Cassandra containers
- Useful for cleaning up after failed tests

## Prerequisites

- Docker or Podman installed and running
- .NET 9.0 SDK (for building the application)
- docker-compose (for stable-demo.sh only)
- Sufficient resources for running Cassandra containers

## Common Usage Patterns

### 1. First Time Demo
```bash
# Recommended approach
./stable-demo.sh
```

### 2. Testing Recovery Scenarios
```bash
# Test all recovery scenarios
./test-resilient-recovery-scenarios.sh --all
```

### 3. Quick Test with Existing Cassandra
```bash
# If you already have Cassandra running
./test-resilient.sh
```

### 4. Cleanup After Testing
```bash
./cleanup-all.sh
```

## Troubleshooting

### Container Conflicts
If you see errors about existing containers:
1. The scripts will prompt you to clean up existing containers
2. Choose option 1 to stop and remove them
3. Or run `./cleanup-all.sh` manually

### Port Conflicts
Default ports used:
- `9042` - Standard Cassandra native port
- `19042` - Used by stable-demo.sh to avoid conflicts
- `9043`, `9044` - Additional nodes in multi-node setups

### Docker vs Podman
The scripts automatically detect and use the available runtime. No configuration needed.

## Script Features

All demo scripts demonstrate:
- ✅ Automatic session/cluster recreation on failure
- ✅ Circuit breaker protection against cascading failures
- ✅ Host state monitoring and automatic recovery
- ✅ Multi-DC failover capabilities
- ✅ Graceful degradation under various failure conditions
- ✅ Recovery without application restart

## Architecture

The resilient client provides:
1. **Proactive Monitoring**: Checks host states every 5 seconds
2. **Connection Refresh**: Refreshes connections every 60 seconds
3. **Health Checks**: Performs session health checks every 30 seconds
4. **Circuit Breakers**: Prevents connection storms to failed hosts
5. **Operation Modes**: Normal, Degraded, ReadOnly, Emergency
6. **Automatic Recovery**: Session and cluster recreation without restart