# Configuration Guide

Cassandra Probe can be configured using command-line arguments, environment variables, or YAML configuration files.

## Configuration Priority

Configuration is applied in the following order (later sources override earlier ones):
1. Default values
2. Configuration file (YAML)
3. Environment variables
4. Command-line arguments

## Configuration File

Create a YAML file with your settings:

```yaml
# Connection settings
contactPoints:
  - cassandra1:9042
  - cassandra2:9042
  - cassandra3:9042

# Authentication
username: cassandra
password: cassandra

# Probe settings
probeInterval: 30        # seconds between probe runs
probeDuration: 300       # total duration in seconds (0 = run once)

# Probe types to run
enabledProbes:
  - socket              # Basic socket connectivity
  - native              # Native protocol (CQL) port
  - storage             # Storage port (7000)
  - cql                 # Execute CQL queries

# CQL probe settings
testCql: "SELECT * FROM system.local"
consistency: LOCAL_ONE
tracing: false

# Timeouts (milliseconds)
connectionTimeout: 10000
requestTimeout: 12000

# Output settings
output: console         # console, json, csv, compact
outputFile: null        # Path to output file (optional)

# SSL/TLS settings
ssl: false
sslCertPath: null
sslValidation: true

# Advanced settings
connectionEvents: true  # Log reconnection events
datacenter: null        # Preferred datacenter
maxRetries: 3
retryDelay: 1000       # milliseconds

# Logging
logLevel: INFO         # DEBUG, INFO, WARNING, ERROR
logFile: null          # Path to log file (optional)
```

## Environment Variables

All settings can be configured via environment variables:

```bash
# Connection
export CASSANDRA_CONTACT_POINTS="node1:9042,node2:9042"
export CASSANDRA_USERNAME="cassandra"
export CASSANDRA_PASSWORD="cassandra"

# Probe settings
export CASSANDRA_PROBE_INTERVAL="30"
export CASSANDRA_PROBE_DURATION="300"

# SSL
export CASSANDRA_SSL="true"
export CASSANDRA_SSL_CERT_PATH="/path/to/cert.pem"

# Output
export CASSANDRA_OUTPUT_FORMAT="json"
export CASSANDRA_OUTPUT_FILE="/var/log/probe.json"
```

## Common Configurations

### Development Environment

`config/dev.yaml`:
```yaml
contactPoints:
  - localhost:9042
  
username: cassandra
password: cassandra

probeInterval: 5
output: console
logLevel: DEBUG
```

### Production Monitoring

`config/prod.yaml`:
```yaml
contactPoints:
  - prod-dc1-node1:9042
  - prod-dc1-node2:9042
  - prod-dc2-node1:9042

username: ${CASSANDRA_USERNAME}  # Use environment variable
password: ${CASSANDRA_PASSWORD}

probeInterval: 60
probeDuration: 0  # Run continuously

enabledProbes:
  - socket
  - native
  - cql

testCql: "SELECT * FROM system.local"
consistency: LOCAL_QUORUM

output: json
outputFile: /var/log/cassandra-probe/probe.json

connectionTimeout: 15000
requestTimeout: 20000

connectionEvents: true
maxRetries: 5
```

### High-Security Environment

`config/secure.yaml`:
```yaml
contactPoints:
  - secure-cluster:9042

# Credentials from environment
username: ${CASSANDRA_USERNAME}
password: ${CASSANDRA_PASSWORD}

# SSL/TLS
ssl: true
sslCertPath: /etc/cassandra/certs/client.pem
sslValidation: true

# Strict timeouts
connectionTimeout: 5000
requestTimeout: 10000

# Minimal logging
logLevel: ERROR
```

## Usage Examples

### Using Configuration File

```bash
# Use specific config file
./cassandra-probe --config /etc/cassandra-probe/prod.yaml

# Override config file settings
./cassandra-probe --config prod.yaml --probe-interval 10

# Use environment variables with config file
export CASSANDRA_PASSWORD="secret"
./cassandra-probe --config prod.yaml
```

### Multiple Environments

```bash
# Development
./cassandra-probe --config config/dev.yaml

# Staging (with overrides)
./cassandra-probe --config config/staging.yaml --output json

# Production (with monitoring)
./cassandra-probe --config config/prod.yaml --connection-events
```

### Docker/Kubernetes

```dockerfile
# Dockerfile
FROM ubuntu:22.04
COPY cassandra-probe /usr/local/bin/
COPY config/prod.yaml /etc/cassandra-probe/config.yaml

ENV CASSANDRA_USERNAME=cassandra
ENV CASSANDRA_PASSWORD=cassandra

CMD ["cassandra-probe", "--config", "/etc/cassandra-probe/config.yaml"]
```

```yaml
# Kubernetes ConfigMap
apiVersion: v1
kind: ConfigMap
metadata:
  name: cassandra-probe-config
data:
  config.yaml: |
    contactPoints:
      - cassandra-service:9042
    probeInterval: 30
    output: json
    outputFile: /var/log/probe.json
```

## Validation

Validate your configuration:

```bash
# Test configuration without running probes
./cassandra-probe --config myconfig.yaml --dry-run

# Show effective configuration
./cassandra-probe --config myconfig.yaml --show-config
```