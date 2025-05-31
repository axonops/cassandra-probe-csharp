# Cassandra Probe User Guide

This guide will help you get started with Cassandra Probe and use it effectively for testing and monitoring your Apache Cassandra clusters.

## Table of Contents

- [Installation](#installation)
- [Basic Usage](#basic-usage)
- [Testing Scenarios](#testing-scenarios)
- [Configuration Files](#configuration-files)
- [Output Formats](#output-formats)
- [Best Practices](#best-practices)
- [Troubleshooting](#troubleshooting)

## Installation

### Download Pre-built Binaries

1. Go to the [Releases](https://github.com/axonops/cassandra-probe-csharp/releases) page
2. Download the appropriate file for your platform:
   - Windows: `cassandra-probe-VERSION-win-x64.zip`
   - Linux: `cassandra-probe-VERSION-linux-x64.tar.gz`
   - macOS Intel: `cassandra-probe-VERSION-osx-x64.tar.gz`
   - macOS Apple Silicon: `cassandra-probe-VERSION-osx-arm64.tar.gz`

3. Extract and make executable:

**Linux/macOS:**
```bash
tar -xzf cassandra-probe-*.tar.gz
chmod +x cassandra-probe
./cassandra-probe --version
```

**Windows:**
```powershell
Expand-Archive cassandra-probe-*.zip
.\cassandra-probe.exe --version
```

### Verify Installation

Check the SHA256 checksum:
```bash
# Linux/macOS
sha256sum -c SHA256SUMS

# Windows (PowerShell)
Get-FileHash cassandra-probe.exe -Algorithm SHA256
```

## Basic Usage

### Simple Connectivity Test

Test connection to a single node:
```bash
./cassandra-probe --contact-points localhost:9042
```

### With Authentication

```bash
./cassandra-probe --contact-points node1:9042 -u cassandra -p cassandra
```

### Multiple Contact Points

```bash
./cassandra-probe --contact-points node1:9042,node2:9042,node3:9042
```

### Using Environment Variables

```bash
export CASSANDRA_CONTACT_POINTS="node1:9042,node2:9042"
export CASSANDRA_USERNAME="cassandra"
export CASSANDRA_PASSWORD="cassandra"
./cassandra-probe
```

## Testing Scenarios

### 1. Driver Reconnection Testing

This is the primary use case for Cassandra Probe. Test how your applications handle node failures:

```bash
# Start continuous monitoring with 5-second intervals
./cassandra-probe --contact-points cluster:9042 -i 5 --connection-events

# In another terminal, stop a Cassandra node
sudo systemctl stop cassandra

# Observe the output:
# - Connection failure detection time
# - Reconnection attempts
# - Which nodes take over the load
# - Time to recover

# Restart the node
sudo systemctl start cassandra

# Observe:
# - Reconnection success
# - Topology updates
# - Load rebalancing
```

### 2. Rolling Restart Validation

Test your maintenance procedures:

```bash
# Run continuous monitoring during maintenance
./cassandra-probe --contact-points cluster:9042 -i 10 -d 60 --all-probes

# Perform your rolling restart
# The probe will show:
# - Which nodes are down
# - Impact on query performance
# - Recovery times
# - Any failed probes
```

### 3. Network Latency Testing

Monitor response times across datacenters:

```bash
# Test specific queries with timing
./cassandra-probe --contact-points dc1-node:9042,dc2-node:9042 \
  --test-cql "SELECT * FROM system.local" \
  --consistency LOCAL_ONE \
  -i 30
```

### 4. Load Testing Support

Use alongside load testing tools:

```bash
# Monitor cluster health during load tests
./cassandra-probe --contact-points cluster:9042 \
  -i 5 \
  --output json \
  --output-file probe-metrics.json
```

## Configuration Files

### YAML Configuration

Create a `probe-config.yaml` file:

```yaml
contactPoints:
  - node1:9042
  - node2:9042
  - node3:9042

username: cassandra
password: cassandra

# Probe settings
probeInterval: 30
probeDuration: 300

# Enable specific probes
enabledProbes:
  - socket
  - native
  - storage
  - cql

# CQL test query
testCql: "SELECT * FROM system.local"
consistency: LOCAL_QUORUM

# Output settings
output: json
outputFile: probe-results.json

# Connection settings
connectionTimeout: 10000
requestTimeout: 12000

# SSL/TLS (if needed)
ssl: false
# sslCertPath: /path/to/cert.pem
```

Use the configuration:
```bash
./cassandra-probe --config probe-config.yaml
```

### Environment-Specific Configs

Create different configs for each environment:

```bash
# Development
./cassandra-probe --config config/dev.yaml

# Staging
./cassandra-probe --config config/staging.yaml

# Production
./cassandra-probe --config config/prod.yaml
```

## Output Formats

### Console Output (Default)

Standard human-readable output with timestamps and color coding.

### JSON Output

Machine-readable format for automation:

```bash
./cassandra-probe --contact-points cluster:9042 --output json
```

Example output:
```json
{
  "sessionId": "550e8400-e29b-41d4-a716-446655440000",
  "timestamp": "2024-01-20T10:15:30Z",
  "topology": {
    "clusterName": "MyCluster",
    "totalHosts": 3,
    "upHosts": 3,
    "downHosts": 0
  },
  "results": [
    {
      "host": "192.168.1.10:9042",
      "probeType": "SocketProbe",
      "success": true,
      "durationMs": 15.2,
      "timestamp": "2024-01-20T10:15:31Z"
    }
  ]
}
```

### CSV Output

For spreadsheet analysis:

```bash
./cassandra-probe --contact-points cluster:9042 --output csv --output-file results.csv
```

### Compact Output

Minimal output for scripting:

```bash
./cassandra-probe --contact-points cluster:9042 --output compact
```

## Best Practices

### 1. Production Monitoring

```bash
# Create a monitoring script
#!/bin/bash
./cassandra-probe \
  --contact-points prod-cluster:9042 \
  --config /etc/cassandra-probe/prod.yaml \
  -i 60 \
  --output json \
  --output-file /var/log/cassandra-probe/probe-$(date +%Y%m%d).json \
  --connection-events
```

### 2. Pre-Maintenance Checks

Before performing maintenance:

```bash
# Verify cluster health
./cassandra-probe --contact-points cluster:9042 --all-probes

# Start continuous monitoring
./cassandra-probe --contact-points cluster:9042 -i 5 --connection-events &
PROBE_PID=$!

# Perform maintenance
# ...

# Stop monitoring
kill $PROBE_PID
```

### 3. Integration with Monitoring Systems

Export metrics to your monitoring system:

```bash
# Output to file for Prometheus node exporter
./cassandra-probe --contact-points cluster:9042 \
  --output json \
  --output-file /var/lib/node_exporter/cassandra_probe.json

# Parse with jq for specific metrics
./cassandra-probe --contact-points cluster:9042 --output json | \
  jq '.results[] | select(.success == false)'
```

## Troubleshooting

### Connection Issues

**Problem**: Cannot connect to Cassandra
```
ERROR - Failed to connect to any contact point
```

**Solutions**:
1. Verify Cassandra is running: `nodetool status`
2. Check network connectivity: `telnet node 9042`
3. Verify authentication: `cqlsh -u cassandra -p cassandra`
4. Check firewall rules

### Authentication Failures

**Problem**: Authentication error
```
ERROR - Authentication failed: Provided username cassandra and/or password are incorrect
```

**Solutions**:
1. Verify credentials in cassandra.yaml
2. Check if authentication is enabled
3. Try with environment variables:
   ```bash
   export CASSANDRA_USERNAME=correct_user
   export CASSANDRA_PASSWORD=correct_pass
   ```

### Timeout Issues

**Problem**: Probe timeouts
```
ERROR - Probe timeout after 10000ms
```

**Solutions**:
1. Increase timeout: `--connection-timeout 30000`
2. Check cluster load: `nodetool tpstats`
3. Verify network latency: `ping -c 10 cassandra-node`

### SSL/TLS Issues

**Problem**: SSL handshake failure

**Solutions**:
1. Verify certificate path: `--ssl-cert /path/to/cert.pem`
2. Check certificate validity: `openssl x509 -in cert.pem -text`
3. Ensure cluster has SSL enabled

### Version Compatibility

**Problem**: Unsupported Cassandra version

**Solutions**:
1. Cassandra Probe requires Cassandra 4.0+
2. Check version: `nodetool version`
3. For Cassandra 3.x, use the original Java version

## Next Steps

- [CLI Reference](CLI-REFERENCE.md) - Complete command-line options
- [Architecture](ARCHITECTURE.md) - Understanding how Cassandra Probe works
- [Contributing](CONTRIBUTING.md) - Help improve Cassandra Probe