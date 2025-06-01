# Cassandra Driver Cluster Events

## Overview

The Cassandra C# driver provides cluster topology event notifications through the `ICluster` interface. These events help track changes in the cluster topology.

> **See Also**: [Architecture](ARCHITECTURE.md) | [Configuration](CONFIGURATION.md) | [Troubleshooting](TROUBLESHOOTING.md)

## Available Events in C# Driver v3.18.0

### Cluster Events

1. **HostAdded**
   - Fired when a new host joins the cluster
   - Provides: `Host` object with address, datacenter, rack information
   - Logged as: `[CLUSTER EVENT] Node ADDED`

2. **HostRemoved**
   - Fired when a host is removed from the cluster
   - Provides: `Host` object with address information
   - Logged as: `[CLUSTER EVENT] Node REMOVED`

### Additional Monitoring Implemented

Since the C# driver doesn't expose HostUp/HostDown events directly, we've implemented:

1. **HostStateMonitor**
   - Polls cluster hosts every 10 seconds
   - Detects when hosts transition between UP and DOWN states
   - Logs: `[CLUSTER EVENT] Node UP detected` and `[CLUSTER EVENT] Node DOWN detected`

2. **MetadataMonitor**
   - Logs initial cluster metadata on connection
   - Polls cluster metadata every minute
   - Detects schema changes by tracking table counts per keyspace
   - Logs detailed schema information when changes are detected

## Cluster Metadata Logged

### Initial Connection
- Cluster name
- Total host count
- Hosts by datacenter
- Each host's address, datacenter, rack, state, and Cassandra version
- User keyspaces (excluding system keyspaces)

### After Cluster Events
- Full metadata refresh after any HostAdded/HostRemoved event
- Host state changes detected by polling

### Periodic Updates
- Metadata logged every minute
- Schema changes detected and logged with details

## What's Not Available in C# Driver

Unlike the Java driver, the C# driver doesn't expose:
- Direct HostUp/HostDown events (we poll for these)
- Schema change events
- Connection pool state change events
- Prepared statement events

## Implementation Details

All cluster events are handled in:
- `SessionManager.cs` - Registers event handlers and logs initial metadata
- `ConnectionMonitor.cs` - Tracks connection states and reconnection history
- `HostStateMonitor.cs` - Polls for host state changes
- `MetadataMonitor.cs` - Monitors metadata and schema changes

## Configuration

Cluster event monitoring is enabled by default. To see cluster events in the logs:

```bash
# Enable debug logging to see all cluster metadata updates
cassandra-probe --contact-points localhost --log-level Debug

# Standard logging will show CLUSTER EVENT and warnings
cassandra-probe --contact-points localhost --log-level Information
```

## Example Output

```
[2025-06-01 14:53:15.917 INF] [CLUSTER METADATA] Connected to cluster: Name=Test Cluster
[2025-06-01 14:53:15.917 INF] [CLUSTER METADATA] Cluster topology: 3 hosts across 2 datacenters
[2025-06-01 14:53:15.921 INF] [CLUSTER METADATA] Host: 10.0.0.1:9042 DC=dc1 Rack=rack1 State=UP Version=4.1.3
[2025-06-01 14:53:15.921 INF] [CLUSTER METADATA] Host: 10.0.0.2:9042 DC=dc1 Rack=rack2 State=UP Version=4.1.3
[2025-06-01 14:53:15.921 INF] [CLUSTER METADATA] Host: 10.0.0.3:9042 DC=dc2 Rack=rack1 State=UP Version=4.1.3
[2025-06-01 14:53:25.784 INF] [CLUSTER EVENT] Node DOWN detected: 10.0.0.2:9042 DC=dc1
[2025-06-01 14:53:35.784 INF] [CLUSTER EVENT] Node UP detected: 10.0.0.2:9042 DC=dc1
```

## Related Documentation

- [Logging Configuration](CONFIGURATION.md#logging-settings) - Configure logging levels and outputs
- [Troubleshooting Connection Issues](TROUBLESHOOTING.md#connection-issues) - Debug connectivity problems
- [Architecture Overview](ARCHITECTURE.md) - Understand the system design