# Cassandra Compatibility Guide

## Overview

Cassandra Probe C# is designed exclusively for modern Apache Cassandra deployments starting from version 4.0. This guide outlines compatibility considerations and version-specific features.

## Supported Versions

- **Minimum**: Apache Cassandra 4.0
- **Recommended**: Apache Cassandra 4.1+
- **Tested**: 4.0, 4.1, 5.0

**Important**: Cassandra 3.x versions (3.0, 3.11) are NOT supported due to missing features and system table differences.

## Major Changes in Cassandra 4.x

### 1. Thrift Protocol Removal (4.0)

The Thrift protocol was completely removed in Cassandra 4.0. This probe uses only the CQL native protocol, ensuring full compatibility with modern Cassandra deployments.

**Impact:**
- No Thrift port (9160) probing
- All operations use CQL native protocol (port 9042)
- No legacy API support

### 2. System Table Changes

While core discovery tables remain stable, some system tables have evolved:

**Stable Tables (used by probe):**
- `system.local` - Local node information
- `system.peers` - Cluster topology
- `system_schema.*` - Schema information

**New in 4.0+:**
- `system_views.*` - Virtual tables for metrics
- `system_virtual_schema.*` - Virtual table schemas
- `system.top_partitions` - Performance monitoring (4.1+)

### 3. Authentication Updates

**Changes:**
- Legacy auth tables removed (`system_auth.users`, `permissions`, `credentials`)
- Client-side password hashing support (4.1+)
- Plugin-based authentication (4.1+)

**Probe Compatibility:**
- Uses standard `PasswordAuthenticator`
- Supports CQLSHRC file parsing
- Compatible with custom auth providers

### 4. Protocol Enhancements

**Native Protocol v4:**
- Primary protocol version in 4.x
- Automatic version negotiation
- Enhanced error reporting
- Custom payloads support

### 5. Virtual Tables (4.0+)

Virtual tables provide node-local metrics without JMX:

```cql
-- Example: Query virtual tables
SELECT * FROM system_views.clients;
SELECT * FROM system_views.sstable_tasks;
SELECT * FROM system_views.settings;
```

**Note:** Virtual table queries are always node-local regardless of consistency level.

## Compatibility Features

### 1. Version Detection

The probe automatically detects Cassandra version:

```csharp
// Pseudo-code for version detection
var version = await QuerySystemLocal("SELECT release_version FROM system.local");
```

### 2. Adaptive Behavior

Based on detected version:
- Adjusts system table queries
- Enables/disables features
- Handles protocol differences

### 3. Mixed-Version Clusters

Supports clusters during rolling upgrades:
- Protocol version negotiation
- Graceful feature degradation
- Version-aware error handling

## Best Practices

### 1. Connection Configuration

```csharp
// Use native protocol only
builder.WithPort(9042)  // Native protocol port
       .WithProtocolVersion(ProtocolVersion.V4);  // Auto-negotiates down if needed
```

### 2. Query Patterns

```csharp
// Use stable system tables
"SELECT * FROM system.local"     // ✓ Stable across versions
"SELECT * FROM system.peers"     // ✓ Stable across versions
"SELECT * FROM system_auth.users" // ✗ Removed in 4.0
```

### 3. Feature Detection

```csharp
// Check for virtual tables support
if (cassandraVersion >= Version.Parse("4.0.0"))
{
    // Can use virtual tables
    await QueryVirtualTables();
}
```

## Why Cassandra 3.x is Not Supported

1. **Missing Virtual Tables**: No `system_views` keyspace for advanced monitoring
2. **Different Auth Tables**: Legacy authentication system incompatible with modern approaches
3. **Protocol Limitations**: Older protocol versions lack necessary features
4. **System Table Differences**: Schema and structure changes between 3.x and 4.x
5. **Feature Requirements**: The probe relies on capabilities introduced in 4.0+

### Configuration Updates

**Remove deprecated options:**
```yaml
# Old configuration (remove these)
thrift_port: 9160
probe_thrift: true

# Modern configuration
native_port: 9042
probe_native: true
```

## Known Limitations

### 1. Streaming Incompatibility
- Cannot stream between major versions
- Affects repairs during upgrades

### 2. Schema Changes
- No schema changes during rolling upgrades
- Wait for full cluster upgrade

### 3. Mixed Protocols
- Some features unavailable in mixed clusters
- Virtual tables require all nodes on 4.0+

## Future Compatibility

The probe is designed to adapt to future Cassandra versions:

1. **Pluggable Architecture**: Easy to add new probe types
2. **Version Adapters**: Abstract version-specific logic
3. **Configuration-Driven**: New features via configuration
4. **Graceful Degradation**: Works with reduced features on older versions

## Testing Matrix

| Cassandra Version | Native Protocol | Discovery | Auth | Virtual Tables | Status |
|-------------------|-----------------|-----------|------|----------------|--------|
| 3.0.x | - | - | - | - | Not Supported |
| 3.11.x | - | - | - | - | Not Supported |
| 4.0.x | ✓ | ✓ | ✓ | ✓ | Fully Supported |
| 4.1.x | ✓ | ✓ | ✓ | ✓ | Fully Supported (Recommended) |
| 5.0.x | ✓ | ✓ | ✓ | ✓ | Fully Supported |

## Troubleshooting

### Common Issues

1. **"Unknown keyspace system_views"**
   - Cause: Querying virtual tables on Cassandra < 4.0
   - Solution: Version detection before virtual table queries

2. **"Invalid consistency level"**
   - Cause: Using new consistency levels on old versions
   - Solution: Check version before using advanced features

3. **Authentication failures after upgrade**
   - Cause: Legacy auth table queries
   - Solution: Use proper authentication methods

### Debug Mode

Enable verbose logging to see version detection:
```bash
cassandra-probe --log-level Debug --contact-points node1.example.com
```

## References

- [Cassandra 4.0 Release Notes](https://cassandra.apache.org/doc/latest/cassandra/new/index.html)
- [Cassandra 4.1 Release Notes](https://cassandra.apache.org/doc/4.1/cassandra/new/index.html)
- [Native Protocol Specification](https://github.com/apache/cassandra/blob/trunk/doc/native_protocol_v4.spec)