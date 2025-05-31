# Cassandra C# Probe

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

- [**User Guide**](docs/USER-GUIDE.md) - Comprehensive usage instructions
- [**CLI Reference**](docs/CLI-REFERENCE.md) - All command-line options
- [**Configuration**](docs/CONFIGURATION.md) - YAML configuration examples
- [**Troubleshooting**](docs/TROUBLESHOOTING.md) - Common issues and solutions

### For Developers
- [**Building from Source**](docs/BUILD.md) - Build instructions for all platforms
- [**Architecture**](docs/ARCHITECTURE.md) - System design and components
- [**Contributing**](docs/CONTRIBUTING.md) - How to contribute to the project

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
[2024-01-20 10:15:30] INFO - Starting probe session
[2024-01-20 10:15:30] INFO - Connected to cluster: MyCluster (3 nodes)
[2024-01-20 10:15:30] INFO - Datacenter: dc1, Hosts: 3 (3 up, 0 down)
[2024-01-20 10:15:31] SUCCESS - Probe: 192.168.1.10:9042 - SocketProbe (15ms)
[2024-01-20 10:15:31] SUCCESS - Probe: 192.168.1.11:9042 - SocketProbe (12ms)
[2024-01-20 10:15:31] SUCCESS - Probe: 192.168.1.12:9042 - SocketProbe (14ms)
[2024-01-20 10:15:32] INFO - All probes completed successfully
```

## 📦 Software Bill of Materials (SBOM)

Every release includes SBOM files in CycloneDX format for security scanning and compliance:
- `cassandra-probe-*-sbom.json` - JSON format
- `cassandra-probe-*-sbom.xml` - XML format

## 🤝 Contributing

We welcome contributions! Please see our [Contributing Guide](docs/CONTRIBUTING.md) for details.

## 📄 License

Copyright © 2024 AxonOps Limited

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
