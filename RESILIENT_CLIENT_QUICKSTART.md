# Resilient Cassandra Client - Quick Start Guide

## Overview

The Cassandra Probe includes a production-grade **Resilient Client** implementation that demonstrates how to handle various Cassandra failure scenarios without requiring application restarts. This client provides automatic recovery from node failures, network issues, and cluster-wide outages.

## Quick Demo (No Cassandra Required)

The easiest way to see the resilient client in action:

```bash
# Clone the repository
git clone https://github.com/axonops/cassandra-probe-csharp.git
cd cassandra-probe-csharp

# Run the interactive demo (includes Cassandra setup)
./scripts/stable-demo.sh
```

This will:
1. Start a local Cassandra instance using Docker
2. Run both standard and resilient clients side-by-side
3. Simulate node failures and show recovery behavior
4. Display detailed metrics and recovery times

## Using with Existing Cassandra

If you already have Cassandra running:

```bash
# Download the latest release
curl -LO https://github.com/axonops/cassandra-probe-csharp/releases/latest/download/cassandra-probe-linux-x64.tar.gz
tar -xzf cassandra-probe-linux-x64.tar.gz

# Run the resilient client demo
./cassandra-probe --contact-points your-cassandra:9042 --resilient-client
```

## Key Features Demonstrated

✅ **Automatic Session Recreation** - Recovers from connection failures without restart  
✅ **Circuit Breakers** - Prevents cascading failures to unhealthy nodes  
✅ **Host Monitoring** - Detects node failures within 5 seconds  
✅ **Connection Pool Management** - Refreshes stale connections automatically  
✅ **Multi-DC Support** - Handles datacenter failures gracefully  
✅ **Graceful Degradation** - Continues operating with reduced capacity  

## Advanced Testing Scenarios

Test specific failure scenarios:

```bash
# Test all recovery scenarios
./scripts/test-resilient-recovery-scenarios.sh --all

# Test specific scenarios:
./scripts/test-resilient-recovery-scenarios.sh 1  # Single node failure
./scripts/test-resilient-recovery-scenarios.sh 2  # Complete cluster outage
./scripts/test-resilient-recovery-scenarios.sh 3  # Network partition
./scripts/test-resilient-recovery-scenarios.sh 4  # Rolling restart
```

## Monitoring During Tests

Run continuous monitoring while testing failures:

```bash
# Monitor with 5-second intervals for 10 minutes
./cassandra-probe \
  --contact-points node1:9042,node2:9042,node3:9042 \
  --resilient-client \
  --interval 5 \
  --duration 10 \
  --connection-events \
  --log-level Information
```

## Implementation for Your Application

The resilient client implementation can be found at:
- **Source Code**: [`src/CassandraProbe.Services/Resilience/ResilientCassandraClient.cs`](src/CassandraProbe.Services/Resilience/ResilientCassandraClient.cs)
- **Configuration Options**: [`ResilientClientOptions` class](src/CassandraProbe.Services/Resilience/ResilientCassandraClient.cs#L1117)

Copy and adapt the implementation for your needs. Key configuration points:
- Host monitoring interval (default: 5 seconds)
- Connection refresh interval (default: 60 seconds)
- Health check interval (default: 30 seconds)
- Circuit breaker thresholds
- Retry policies

## Documentation

- **[Resilient Client Implementation Guide](docs/RESILIENT_CLIENT_IMPLEMENTATION.md)** - Detailed implementation documentation
- **[C# Driver Observations](docs/CSHARP_DRIVER_OBSERVATIONS.md)** - Why we built this and what problems it solves
- **[Architecture Overview](docs/ARCHITECTURE.md)** - System design and components
- **[Demo Scripts Documentation](scripts/README.md)** - All available test scripts

## Requirements

- **For demos**: Docker or Podman
- **For production use**: .NET 9.0 runtime (or use our self-contained binaries)
- **Cassandra**: Version 4.0 or later

## Questions?

- Report issues: https://github.com/axonops/cassandra-probe-csharp/issues
- Implementation questions: See the [Implementation Guide](docs/RESILIENT_CLIENT_IMPLEMENTATION.md)
- Driver behavior: See our [C# Driver Observations](docs/CSHARP_DRIVER_OBSERVATIONS.md)