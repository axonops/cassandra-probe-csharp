# Cassandra Probe C# - Feature Documentation

## Core Features

### 1. Cluster Discovery and Node Enumeration

The probe connects to one or more contact points and discovers all nodes in the cluster.

**Functionality:**
- Uses Cassandra system tables to enumerate all nodes
- Retrieves comprehensive node metadata
- Handles multi-datacenter deployments
- Supports virtual nodes (vnodes)

**Retrieved Information:**
- Host IDs and addresses
- Datacenter and rack topology
- Node status (UP/DOWN)
- Cassandra version per node
- Token ranges (if needed)

### 2. Connection Probing

#### 2.1 Socket Probe
Tests raw TCP socket connectivity to specified ports.

**Features:**
- Configurable connection timeout
- Measures connection establishment time
- Handles various socket exceptions
- Supports all Cassandra ports

**Tested Ports:**
- Native Protocol (default: 9042)
- Storage/Gossip (default: 7000)

#### 2.2 Ping/Reachability Probe
Network-level reachability test.

**Features:**
- ICMP echo request (where supported)
- TCP echo as fallback
- Configurable timeout
- Round-trip time measurement

#### 2.3 Port-Specific Probes
Targeted testing of individual Cassandra services.

**Native Port Probe:**
- Validates CQL native protocol availability
- Tests authentication if configured
- Checks protocol version compatibility

**Storage Port Probe:**
- Verifies inter-node communication port
- Essential for cluster gossip validation

### 3. CQL Query Execution

Execute test queries to validate cluster functionality.

**Supported Query Types:**
- SELECT statements
- INSERT statements  
- UPDATE statements
- System table queries

**Features:**
- Query validation before execution
- Configurable consistency levels:
  - ANY, ONE, TWO, THREE
  - QUORUM, ALL
  - LOCAL_QUORUM, EACH_QUORUM
  - LOCAL_ONE
- Query execution timing
- Result set handling
- Error categorization

**Query Tracing:**
- Optional query tracing
- Trace event capture
- Performance bottleneck identification
- Coordinator and replica timing

### 4. Authentication and Security

The probe adapts to your cluster's security configuration - authentication and SSL/TLS are completely optional.

#### 4.1 No Authentication Mode
Default mode for development and unsecured clusters.

**Features:**
- Direct connection without credentials
- No configuration required
- Fastest setup for testing
- Common in development environments

#### 4.2 Basic Authentication
Username and password support when required.

**Features:**
- Command-line credential input
- Environment variable support
- Secure credential handling
- Default cassandra/cassandra for new clusters

#### 4.3 CQLSHRC File Support
Parse and use CQLSH configuration files.

**Supported Sections:**
- [authentication] - username/password
- [connection] - timeouts, ports
- [ssl] - certificate configuration

#### 4.4 SSL/TLS Support
Optional secure connection capabilities.

**Features:**
- Certificate validation
- Custom CA support
- Client certificate authentication
- TLS version configuration
- Not required for most deployments

### 5. Logging and Monitoring

#### 5.1 Structured Logging
Comprehensive logging system.

**Log Levels:**
- DEBUG: Detailed trace information
- INFO: Normal operation logs
- WARN: Failed probes, issues
- ERROR: Exceptions, critical failures

**Log Information:**
- Timestamp with millisecond precision
- Thread/Task identification
- Source component
- Structured data fields

#### 5.2 Log Rotation
Automatic log file management.

**Features:**
- Size-based rotation
- Time-based rotation
- Configurable retention
- Compression support

#### 5.3 Output Formats
Multiple output options.

**Formats:**
- Console output
- Rolling file logs
- JSON structured logs
- CSV export for analysis

### 6. Scheduling and Continuous Monitoring

#### 6.1 Single-Run Mode
One-time diagnostic execution.

**Use Cases:**
- Quick health checks
- Troubleshooting
- Pre-deployment validation

#### 6.2 Continuous Monitoring with Connection Persistence
Critical capability for testing driver resilience.

**Connection Management:**
- **Persistent Sessions**: Cluster and Session objects are created once and reused
- **No Reconnection on Success**: Successful probes continue using existing connections
- **Automatic Recovery**: Driver handles reconnection when nodes fail
- **Connection Pool Monitoring**: Tracks pool health and reconnection events

**Reconnection Testing Features:**
- Maintains single Cluster instance throughout monitoring lifecycle
- Reuses Session objects across all probe iterations
- Tests driver's built-in reconnection logic
- Validates connection pool recovery mechanisms
- Essential for production resilience validation

**Use Cases:**
- Test driver behavior during rolling restarts
- Validate recovery from network partitions
- Monitor reconnection timing and success rates
- Verify connection pool health after outages

#### 6.3 Reconnection Logging
Comprehensive logging of all connection events.

**Logged Events:**
- Initial connection establishment
- Connection pool state changes
- Reconnection attempts (with timestamps)
- Successful reconnections
- Failed connection attempts with reasons
- Node up/down events
- Connection pool metrics

**Log Format Example:**
```
[INFO] Establishing initial connection to cluster
[INFO] Connection pool initialized: 4 connections to 3 nodes
[WARN] Lost connection to node 192.168.1.10:9042
[INFO] Driver attempting reconnection to 192.168.1.10:9042 (attempt 1/10)
[INFO] Driver attempting reconnection to 192.168.1.10:9042 (attempt 2/10)
[INFO] Successfully reconnected to 192.168.1.10:9042 after 5.2 seconds
[INFO] Connection pool restored: 4 connections to 3 nodes
```

#### 6.4 Job Management
Built on Quartz.NET scheduler with session persistence.

**Capabilities:**
- Cron expression support
- Job persistence
- Misfire handling
- Concurrent execution prevention
- Shared Cluster/Session across job executions

### 7. Configuration Management

#### 7.1 Command-Line Arguments
Comprehensive CLI interface.

**Categories:**
- Connection settings
- Authentication options
- Probe selection
- Query configuration
- Logging preferences
- Scheduling parameters

#### 7.2 Configuration Files
Support for external configuration.

**Formats:**
- JSON configuration
- YAML support
- Environment variable override

#### 7.3 Cassandra YAML Parsing
Read Cassandra configuration directly.

**Extracted Settings:**
- Listen addresses
- RPC addresses
- Port configurations
- SSL settings

### 8. Error Handling and Resilience

#### 8.1 Exception Categorization
Specific handling for different error types.

**Categories:**
- Network errors
- Authentication failures
- Authorization errors
- Timeout exceptions
- Query syntax errors
- Driver exceptions

#### 8.2 Retry Logic
Configurable retry mechanisms.

**Features:**
- Exponential backoff
- Maximum retry limits
- Retry-able error detection
- Circuit breaker pattern

#### 8.3 Graceful Degradation
Continue operation despite failures.

**Behaviors:**
- Skip unreachable nodes
- Continue with partial results
- Log all failures
- Summary reporting

### 9. Performance Features

#### 9.1 Async Operations
Leverage C# async/await throughout.

**Benefits:**
- Non-blocking I/O
- Parallel node probing
- Efficient resource usage
- Responsive UI/monitoring

#### 9.2 Connection Pooling
Efficient connection management.

**Features:**
- Configurable pool sizes
- Connection lifecycle management
- Health checking
- Load balancing

#### 9.3 Metrics Collection
Performance measurement.

**Metrics:**
- Probe execution times
- Success/failure rates
- Query latencies
- Connection pool statistics

### 10. Extensibility

#### 10.1 Custom Probe Types
Plugin architecture for new probes.

**Interface:**
- IProbeAction interface
- Probe registration
- Configuration support
- Result standardization

#### 10.2 Custom Loggers
Pluggable logging backends.

**Supported:**
- Serilog sinks
- Custom formatters
- External monitoring integration

#### 10.3 Export Formats
Extensible result export.

**Formats:**
- JSON reports
- CSV exports
- Monitoring system integration
- Custom formatters

## Connection Recovery and Resilience Testing

### Driver Reconnection Capabilities

The probe is specifically designed to test and validate Cassandra driver reconnection behavior:

**Session Lifecycle Management:**
- Single Cluster instance per monitoring session
- Persistent Session objects across all probes
- No manual reconnection - relies on driver's automatic recovery
- Connection pool shared across all probe iterations

**Reconnection Scenarios Tested:**
1. **Node Failures**: Individual node goes down and comes back
2. **Rolling Restarts**: Nodes restarted one by one
3. **Network Partitions**: Temporary network disruptions
4. **Cluster Resizing**: Nodes added or removed
5. **Complete Outages**: All nodes temporarily unavailable

**Driver Events Monitored:**
- `HostUp` - Node becomes available
- `HostDown` - Node becomes unavailable  
- `HostAdded` - New node joins cluster
- `HostRemoved` - Node leaves cluster
- Connection pool state changes
- Reconnection attempt metrics

**Logging Detail Levels:**
- **INFO**: Successful connections and reconnections
- **WARN**: Connection losses and retry attempts
- **ERROR**: Failed reconnection after all retries
- **DEBUG**: Detailed connection pool metrics

### Connection Pool Monitoring

Real-time visibility into connection health:

```
Connection Pool Status:
- Total Connections: 12
- Active Connections: 8
- Idle Connections: 4
- Pending Connections: 2
- Failed Hosts: [192.168.1.10:9042]
- Reconnecting Hosts: [192.168.1.10:9042 (attempt 3/10)]
```

## Cassandra Version Compatibility

### Cassandra 4.1+ Support

The probe is optimized for modern Cassandra deployments with special support for 4.1+ features:

**Virtual Tables Support:**
- Query `system_views` keyspace for metrics
- Access node-local monitoring data without JMX
- Read runtime configuration via Settings Virtual Table

**Enhanced Discovery:**
- Uses `system.local` and `system.peers` (unchanged)
- Compatible with new `system_schema` structure
- Handles protocol v4 features

**Security Enhancements:**
- Client-side password hashing support
- Plugin-based authentication compatibility
- Modern SSL/TLS configurations

**Removed Features:**
- No Thrift protocol support (removed in Cassandra 4.0)
- No legacy auth table queries
- No deprecated system table access

### Version Requirements

The probe requires Cassandra 4.0 or later:

**Minimum Version:**
- Cassandra 4.0+ required
- No support for 3.x versions
- Optimized for 4.1 and 5.0

**Version Detection:**
- Automatic version checking on connect
- Clear error messages for unsupported versions
- Feature enablement based on detected version