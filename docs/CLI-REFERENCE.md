# Cassandra Probe C# - CLI Reference

## Command Line Interface

### Basic Usage

```bash
cassandra-probe [options]
cassandra-probe probe [probe-options]
cassandra-probe schedule [schedule-options]
```

### Global Options

| Option | Short | Description | Default |
|--------|-------|-------------|---------|
| `--help` | `-h` | Show help information | - |
| `--version` | `-v` | Show version information | - |
| `--config` | - | Path to configuration file | - |
| `--log-level` | - | Set log level (Debug, Info, Warn, Error) | Info |

### Connection Options

| Option | Short | Description | Default |
|--------|-------|-------------|---------|
| `--contact-points` | `-cp` | Comma-separated list of contact points | Required |
| `--port` | `-P` | Native protocol port | 9042 |
| `--datacenter` | `-dc` | Local datacenter name | - |
| `--yaml` | `-y` | Path to cassandra.yaml for configuration | - |

### Authentication Options

| Option | Short | Description | Default |
|--------|-------|-------------|---------|
| `--username` | `-u` | Cassandra username | - |
| `--password` | `-p` | Cassandra password | - |
| `--cqlshrc` | `-c` | Path to CQLSHRC file | - |
| `--ssl` | - | Enable SSL/TLS | false |
| `--cert` | - | Path to client certificate | - |
| `--ca-cert` | - | Path to CA certificate | - |

### Probe Selection Options

| Option | Short | Description | Default |
|--------|-------|-------------|---------|
| `--native` | `-na` | Probe native protocol port | true |
| `--storage` | `-st` | Probe storage/gossip port | false |
| `--ping` | `-pi` | Execute ping/reachability probe | false |
| `--all-probes` | `-a` | Execute all probe types | false |
| `--socket-timeout` | - | Socket connection timeout (ms) | 10000 |

### Query Options

| Option | Short | Description | Default |
|--------|-------|-------------|---------|
| `--test-cql` | `-cql` | Test CQL query to execute | - |
| `--consistency` | `-con` | Consistency level for query | ONE |
| `--tracing` | `-tr` | Enable query tracing | false |
| `--query-timeout` | - | Query execution timeout (seconds) | 30 |

### Scheduling Options

| Option | Short | Description | Default |
|--------|-------|-------------|---------|
| `--interval` | `-i` | Seconds between probe executions | - |
| `--cron` | - | Cron expression for scheduling | - |
| `--duration` | `-d` | Total duration to run (minutes) | - |
| `--max-runs` | - | Maximum number of probe runs | - |

### Logging Options

| Option | Short | Description | Default |
|--------|-------|-------------|---------|
| `--log-dir` | `-ld` | Directory for log files | ./logs |
| `--log-max-days` | `-lmd` | Maximum days to keep logs | 7 |
| `--log-max-file-mb` | `-lmfs` | Max log file size before rotation | 100 |
| `--log-format` | - | Log format (text, json) | text |
| `--quiet` | `-q` | Suppress console output | false |
| `--verbose` | `-V` | Enable verbose output | false |
| `--log-reconnections` | - | Log all reconnection events | true |
| `--connection-events` | - | Show connection events in console | true |

### Output Options

| Option | Short | Description | Default |
|--------|-------|-------------|---------|
| `--output` | `-o` | Output format (console, json, csv) | console |
| `--output-file` | `-of` | File path for output | - |
| `--metrics` | `-m` | Enable metrics collection | false |
| `--metrics-export` | - | Metrics export format | - |

## Examples

### Basic Probe

```bash
# Simple connectivity test (no authentication required)
cassandra-probe -cp node1.example.com

# With authentication (only if cluster requires it)
cassandra-probe -cp node1.example.com -u cassandra -p cassandra

# Multiple contact points
cassandra-probe -cp "node1.example.com,node2.example.com,node3.example.com"
```

### Authentication Examples

```bash
# Cluster without authentication (most common for development)
cassandra-probe -cp localhost:9042

# Cluster with authentication
cassandra-probe -cp cluster.example.com -u myuser -p mypass

# Using CQLSHRC file
cassandra-probe -cp cluster.example.com -c ~/.cassandra/cqlshrc

# With SSL/TLS (only if cluster requires it)
cassandra-probe -cp secure.example.com --ssl --ca-cert /path/to/ca.pem

# Client certificate authentication
cassandra-probe -cp secure.example.com --ssl --cert /path/to/client.pem --ca-cert /path/to/ca.pem
```

### Probe Type Selection

```bash
# Only native protocol probe
cassandra-probe -cp node.example.com -na

# All network probes
cassandra-probe -cp node.example.com -na -st -pi

# All available probes
cassandra-probe -cp node.example.com --all-probes
```

### CQL Query Testing

```bash
# Query test without authentication
cassandra-probe -cp node.example.com -cql "SELECT * FROM system.local"

# Query test with authentication
cassandra-probe -cp node.example.com -u user -p pass -cql "SELECT * FROM system.local"

# Query with consistency level
cassandra-probe -cp node.example.com -cql "SELECT * FROM keyspace.table" -con QUORUM

# Query with tracing
cassandra-probe -cp node.example.com -cql "SELECT * FROM keyspace.table" -tr

# Insert query test
cassandra-probe -cp node.example.com -cql "INSERT INTO test.data (id, value) VALUES (1, 'test')"
```

### Scheduled Execution

```bash
# Probe every 30 seconds
cassandra-probe -cp cluster.example.com -i 30

# Probe every 5 minutes for 1 hour
cassandra-probe -cp cluster.example.com -i 300 -d 60

# Using cron expression (every hour at minute 0)
cassandra-probe -cp cluster.example.com --cron "0 * * * *"

# Limited number of runs
cassandra-probe -cp cluster.example.com -i 60 --max-runs 10
```

### Logging Configuration

```bash
# Custom log directory
cassandra-probe -cp node.example.com -ld /var/log/cassandra-probe

# JSON logging with rotation
cassandra-probe -cp node.example.com --log-format json -lmd 30 -lmfs 50

# Verbose debugging
cassandra-probe -cp node.example.com --log-level Debug --verbose

# Quiet mode (no console output)
cassandra-probe -cp node.example.com -q -ld /var/log/probe
```

### Output Formats

```bash
# JSON output to file
cassandra-probe -cp cluster.example.com -o json -of probe-results.json

# CSV export
cassandra-probe -cp cluster.example.com -o csv -of probe-results.csv

# Metrics collection
cassandra-probe -cp cluster.example.com -m --metrics-export prometheus
```

### Complex Examples

```bash
# Full diagnostic with all probes and query
cassandra-probe \
  -cp "n1.cluster.com,n2.cluster.com,n3.cluster.com" \
  -c ~/.cassandra/cqlshrc \
  --all-probes \
  -cql "SELECT * FROM system_schema.keyspaces" \
  -tr \
  -ld /var/log/cassandra-probe \
  -o json \
  -of /tmp/probe-report.json

# Continuous monitoring with notifications
cassandra-probe \
  -cp production.cluster.com \
  -u monitor_user \
  -p $MONITOR_PASS \
  -na -st -pi \
  -i 60 \
  -ld /var/log/monitoring \
  --log-format json \
  -m

# Development environment probe
cassandra-probe \
  -cp localhost:9042 \
  --all-probes \
  -cql "SELECT * FROM test.data LIMIT 10" \
  --verbose \
  --log-level Debug
```

### Testing Driver Reconnection

```bash
# Monitor reconnection behavior during node failures
cassandra-probe \
  -cp "node1:9042,node2:9042,node3:9042" \
  -i 5 \
  --connection-events \
  --verbose \
  -ld ./reconnect-test-logs

# Test with single contact point (driver discovers others)
cassandra-probe \
  -cp node1:9042 \
  -i 10 \
  -d 60 \
  --log-reconnections \
  --metrics

# Minimal output, focus on connection events
cassandra-probe \
  -cp cluster:9042 \
  -i 30 \
  --quiet \
  --connection-events \
  -of connection-events.log
```

### Configuration File Usage

Instead of command-line arguments, you can use a configuration file:

```bash
# Using JSON configuration
cassandra-probe --config probe-config.json

# Using YAML configuration
cassandra-probe --config probe-config.yaml
```

Example configuration file (JSON):
```json
{
  "contactPoints": ["node1.example.com", "node2.example.com"],
  "authentication": {
    "username": "cassandra",
    "password": "cassandra"
  },
  "probes": {
    "native": true,
    "storage": true,
    "ping": true
  },
  "logging": {
    "directory": "/var/log/cassandra-probe",
    "maxDays": 30,
    "format": "json"
  },
  "scheduling": {
    "interval": 300
  }
}
```

## Environment Variables

The following environment variables are supported:

| Variable | Description |
|----------|-------------|
| `CASSANDRA_CONTACT_POINTS` | Default contact points |
| `CASSANDRA_USERNAME` | Default username |
| `CASSANDRA_PASSWORD` | Default password |
| `CASSANDRA_PROBE_LOG_DIR` | Default log directory |
| `CASSANDRA_PROBE_CONFIG` | Default config file path |

## Exit Codes

| Code | Description |
|------|-------------|
| 0 | Success |
| 1 | General error |
| 2 | Configuration error |
| 3 | Connection error |
| 4 | Authentication error |
| 5 | Query execution error |
| 10 | Partial failure (some probes failed) |