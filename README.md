# Apache CassandraⓇ C# Probe

A diagnostic and monitoring tool for Apache Cassandra® clusters, designed to test C# driver reconnection behavior and cluster resilience.

[![Build Status](https://github.com/axonops/cassandra-probe-csharp/actions/workflows/build.yml/badge.svg)](https://github.com/axonops/cassandra-probe-csharp/actions)
[![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](https://opensource.org/licenses/Apache-2.0)

## 🎯 What is Cassandra Probe?

Cassandra Probe is a standalone diagnostic tool that helps you:

- **Test Cassandra driver resilience** - Validate how your applications handle node failures and recoveries
- **Monitor cluster health** - Continuously probe your Cassandra nodes to detect issues early
- **Verify rolling restarts** - Ensure your maintenance procedures don't impact availability
- **Diagnose connectivity issues** - Identify network, authentication, or configuration problems
- **Validate failover scenarios** - Test your disaster recovery procedures

Unlike simple connectivity checkers, Cassandra Probe maintains persistent connections to simulate real application behavior, making it ideal for testing production scenarios.

## 🚀 Quick Start

### Download

Download the latest release for your platform from the [Releases](https://github.com/axonops/cassandra-probe-csharp/releases) page.

| Platform | File | Description |
|----------|------|-------------|
| Windows | `cassandra-probe-*-win-x64.zip` | 64-bit Windows executable |
| Linux | `cassandra-probe-*-linux-x64.tar.gz` | 64-bit Linux executable |
| macOS Intel | `cassandra-probe-*-osx-x64.tar.gz` | Intel Mac executable |
| macOS Apple Silicon | `cassandra-probe-*-osx-arm64.tar.gz` | M1/M2/M3 Mac executable |

### Install and Run

**Linux/macOS:**
```bash
# Extract
tar -xzf cassandra-probe-*.tar.gz

# Make executable
chmod +x cassandra-probe

# Run basic connectivity test
./cassandra-probe --contact-points localhost:9042
```

**Windows:**
```powershell
# Extract
Expand-Archive cassandra-probe-*.zip

# Run basic connectivity test
.\cassandra-probe.exe --contact-points localhost:9042
```

### Common Use Cases

```bash
# Test cluster with authentication
./cassandra-probe --contact-points node1:9042,node2:9042 -u cassandra -p cassandra

# Monitor cluster continuously (every 30 seconds)
./cassandra-probe --contact-points cluster:9042 -i 30 --connection-events

# Run all diagnostic probes
./cassandra-probe --contact-points cluster:9042 --all-probes

# Test specific CQL query
./cassandra-probe --contact-points cluster:9042 --test-cql "SELECT * FROM system.local"

# Run resilient client demonstration (shows how to handle C# driver limitations)
./cassandra-probe --contact-points cluster:9042 --resilient-client
```

## 📋 Requirements

- **Apache Cassandra**: 4.0 or later (Cassandra 3.x is not supported)
- **Operating System**: Windows, Linux, or macOS
- **Runtime**: None required (self-contained executable)

## ✨ Key Features

### Driver Reconnection Testing
The primary purpose of Cassandra Probe is to validate Cassandra driver behavior during failure scenarios:
- Maintains persistent connections across probe iterations
- Logs all reconnection events and attempts
- Simulates real application connection patterns
- Helps validate production failover behavior

### Comprehensive Diagnostics
- **Multiple probe types**: Socket, ping, CQL, port-specific tests
- **Cluster discovery**: Automatically finds all nodes in the topology
- **Detailed metrics**: Response times, error rates, success counts
- **Flexible output**: Console, JSON, CSV, or compact formats

### Production-Ready
- **Zero dependencies**: Single executable file
- **Cross-platform**: Native builds for all major platforms
- **Lightweight**: Minimal resource usage
- **Configurable**: YAML configuration files supported

## 📖 Documentation

For comprehensive documentation, see the **[Documentation Index](docs/README.md)**.

### Quick Links
- [**User Guide**](docs/USER-GUIDE.md) - Getting started and common usage patterns
- [**CLI Reference**](docs/CLI-REFERENCE.md) - All command-line options
- [**Configuration**](docs/CONFIGURATION.md) - YAML configuration examples
- [**Troubleshooting**](docs/TROUBLESHOOTING.md) - Common issues and solutions
- [**Building from Source**](docs/BUILD.md) - Build instructions for developers

## ⚠️ C# Driver Limitations

**Important**: The DataStax C# driver has significant limitations compared to the Java driver that affect failure detection and recovery:

- **No HostUp/HostDown events** - The driver doesn't notify when nodes fail or recover ([CSHARP-183](https://datastax-oss.atlassian.net/browse/CSHARP-183))
- **No proactive failure detection** - Failed connections discovered only when used
- **Poor recovery during rolling restarts** - Applications may not reconnect automatically
- **Stale connection pools** - Dead connections remain until manually refreshed

These limitations can cause your C# applications to:
- Continue using failed connections
- Experience timeouts instead of failover
- Require manual intervention or restart to recover

### Documentation and References
- **[Detailed C# Driver Limitations Guide](docs/CSHARP_DRIVER_LIMITATIONS.md)** - Comprehensive guide with workarounds
- **[Resilient Client Implementation](docs/RESILIENT_CLIENT_IMPLEMENTATION.md)** - Production-ready solution
- [DataStax JIRA: CSHARP-183](https://datastax-oss.atlassian.net/browse/CSHARP-183) - HostUp/HostDown events (open since 2014)
- [Known Limitations](https://docs.datastax.com/en/developer/csharp-driver/latest/features/connection-pooling/#known-limitations) - Official driver docs
- [Driver Comparison](https://docs.datastax.com/en/developer/csharp-driver/latest/faq/#how-does-the-c-driver-compare-to-the-java-driver) - C# vs Java

### Try the Resilient Client Demo
```bash
# See how to handle these limitations in production
./cassandra-probe --contact-points cluster:9042 --resilient-client
```

## 🧪 Testing Driver Reconnection

One of the most valuable uses of Cassandra Probe is testing how your applications handle node failures:

```bash
# Start continuous monitoring
./cassandra-probe --contact-points cluster:9042 -i 5 --connection-events

# In another terminal, stop a Cassandra node
# Watch the probe output to see:
# - Connection failure detection
# - Reconnection attempts
# - Recovery time
# - Which nodes take over

# Restart the node and observe:
# - Reconnection success
# - Topology changes
# - Load rebalancing
```

This helps you understand:
- How quickly your drivers detect failures
- Whether reconnection policies work correctly
- If your application can maintain availability during maintenance

## 🔍 Example Output

```
[2025-05-31 21:40:18.647 INF] Cassandra Probe starting... <CassandraProbe.Cli.Program>
[2025-05-31 21:40:18.688 INF] Initialized Scheduler Signaller of type: Quartz.Core.SchedulerSignalerImpl <Quartz.Core.SchedulerSignalerImpl>
[2025-05-31 21:40:18.688 INF] Quartz Scheduler created <Quartz.Core.QuartzScheduler>
[2025-05-31 21:40:18.688 INF] JobFactory set to: Quartz.Simpl.MicrosoftDependencyInjectionJobFactory <Quartz.Core.QuartzScheduler>
[2025-05-31 21:40:18.688 INF] RAMJobStore initialized. <Quartz.Simpl.RAMJobStore>
[2025-05-31 21:40:18.689 INF] Quartz Scheduler 3.13.0.0 - 'QuartzScheduler' with instanceId 'NON_CLUSTERED' initialized <Quartz.Impl.StdSchedulerFactory>
[2025-05-31 21:40:18.689 INF] Using thread pool 'Quartz.Simpl.DefaultThreadPool', size: 10 <Quartz.Impl.StdSchedulerFactory>
[2025-05-31 21:40:18.689 INF] Using job store 'Quartz.Simpl.RAMJobStore', supports persistence: False, clustered: False <Quartz.Impl.StdSchedulerFactory>
[2025-05-31 21:40:18.694 INF] Adding 0 jobs, 0 triggers. <Quartz.ContainerConfigurationProcessor>
[2025-05-31 21:40:18.696 INF] Scheduler QuartzScheduler_$_NON_CLUSTERED started. <Quartz.Core.QuartzScheduler>
[2025-05-31 21:40:18.696 INF] Scheduler started <CassandraProbe.Scheduling.JobScheduler>
[2025-05-31 21:40:18.698 INF] Scheduling probe job with interval: 10 seconds <CassandraProbe.Scheduling.JobScheduler>
[2025-05-31 21:40:18.702 INF] Probe job scheduled successfully. First run: 05/31/2025 19:40:18 <CassandraProbe.Scheduling.JobScheduler>
[2025-05-31 21:40:18.703 INF] Probe scheduled. Press Ctrl+C to stop... <CassandraProbe.Cli.Program>
[2025-05-31 21:40:18.711 INF] Starting scheduled probe job ProbeJob <CassandraProbe.Scheduling.ProbeJob>
[2025-05-31 21:40:18.713 INF] Starting probe session a3d8a1ef-8427-489e-8e83-a85026436e6c <CassandraProbe.Services.ProbeOrchestrator>
[2025-05-31 21:40:18.713 INF] Creating new Cluster instance (one-time operation) <CassandraProbe.Services.SessionManager>
[2025-05-31 21:40:18.734 INF] Connection monitor registered with cluster, tracking 0 hosts <CassandraProbe.Services.ConnectionMonitor>
[2025-05-31 21:40:18.981 INF] Session established and will be reused for all operations <CassandraProbe.Services.SessionManager>
[2025-05-31 21:40:18.982 INF] Starting cluster discovery... <CassandraProbe.Services.ClusterDiscoveryService>
[2025-05-31 21:40:19.029 INF] Discovered 11 peer nodes <CassandraProbe.Services.ClusterDiscoveryService>
[2025-05-31 21:40:19.029 INF] Discovered 12 nodes in cluster 'axonops-webinar' <CassandraProbe.Services.ClusterDiscoveryService>
[2025-05-31 21:40:19.029 INF] Nodes by status - Up: 12, Down: 0 <CassandraProbe.Services.ClusterDiscoveryService>
[2025-05-31 21:40:19.030 INF] Executing 5 probe types on 12 hosts <CassandraProbe.Services.ProbeOrchestrator>
[2025-05-31 21:40:19.117 INF] Probe session a3d8a1ef-8427-489e-8e83-a85026436e6c completed in 0.40s. Success: 60, Failures: 0 <CassandraProbe.Services.ProbeOrchestrator>
[2025-05-31 21:40:19.125 INF] Connection pool status - Total: 0, Active: 0, Failed: 0, Reconnecting: 0 <CassandraProbe.Scheduling.ProbeJob>
[2025-05-31 21:40:19.125 INF] Scheduled probe job ProbeJob completed in 0.41s. Next run: 05/31/2025 19:40:28 <CassandraProbe.Scheduling.ProbeJob>
```

## 📦 Software Bill of Materials (SBOM)

Every release includes SBOM files in CycloneDX format for security scanning and compliance:
- `cassandra-probe-*-sbom.json` - JSON format
- `cassandra-probe-*-sbom.xml` - XML format

## 🤝 Contributing

We welcome contributions! Please see our [Contributing Guide](docs/CONTRIBUTING.md) for details.

## 📄 License

Copyright © 2025 AxonOps Limited

This project is licensed under the Apache License 2.0 - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

This project builds upon the excellent work of:

- The [Apache Cassandra](https://cassandra.apache.org/) community for creating and maintaining the powerful distributed database that makes this tool necessary
- [DataStax](https://www.datastax.com/) for the C# Driver for Apache Cassandra
- The original [cassandra-probe](https://github.com/digitalis-io/cassandra-probe) project by Digitalis.IO, which inspired this C# implementation

Apache Cassandra is a registered trademark of the Apache Software Foundation.

## 🔗 Links

- **Report Issues**: [GitHub Issues](https://github.com/axonops/cassandra-probe-csharp/issues)
- **Releases**: [GitHub Releases](https://github.com/axonops/cassandra-probe-csharp/releases)
- **AxonOps**: [https://axonops.com](https://axonops.com)

***

*This project may contain trademarks or logos for projects, products, or services. Any use of third-party trademarks or logos are subject to those third-party's policies. AxonOps is a registered trademark of AxonOps Limited. Apache, Apache Cassandra, Cassandra, Apache Spark, Spark, Apache TinkerPop, TinkerPop, Apache Kafka and Kafka are either registered trademarks or trademarks of the Apache Software Foundation or its subsidiaries in Canada, the United States and/or other countries. Elasticsearch is a trademark of Elasticsearch B.V., registered in the U.S. and in other countries. Docker is a trademark or registered trademark of Docker, Inc. in the United States and/or other countries.*
