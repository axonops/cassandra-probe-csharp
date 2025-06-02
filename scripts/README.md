# Cassandra Probe Demo Scripts

This directory contains demonstration scripts for the Cassandra Probe resilient client functionality.

## 🚀 Quick Start

```bash
# Most reliable demo (RECOMMENDED)
./stable-demo.sh

# Test with existing Cassandra
./test-resilient.sh localhost:9042
```

## Container Runtime Support

All scripts support both **Docker** and **Podman**. Scripts automatically detect which runtime is available using the `container-runtime.sh` helper.

## Scripts Overview

### Core Demo Scripts

#### 1. `stable-demo.sh` ⭐ RECOMMENDED
The most reliable demo using docker-compose for proper orchestration.
- Uses docker-compose for container management
- Proper health checks ensure Cassandra is ready
- Single node on port 19042 (avoids conflicts)
- Clean side-by-side comparison of standard vs resilient client
- Consistent and reliable results

```bash
./stable-demo.sh
```

#### 2. `test-resilient.sh`
Simple test script for existing Cassandra installations.
- No container management needed
- Works with any running Cassandra instance
- Accepts custom contact points
- Quick 10-second comparison test
- Minimal setup required

```bash
# Test with local Cassandra (default port 9042)
./test-resilient.sh

# Test with remote Cassandra
./test-resilient.sh cassandra.example.com:9042

# Test with custom port
./test-resilient.sh localhost:19042
```

#### 3. `working-demo.sh`
Standalone container demo with optional failure simulation.
- Manages single Cassandra container
- Includes failure recovery demonstration
- Interactive mode with user prompts
- Good for understanding failure scenarios
- Port detection to avoid conflicts

```bash
# Full interactive demo with failure simulation
./working-demo.sh

# Quick comparison only (no failure simulation)
./working-demo.sh --quick
```

### Advanced Test Scripts

#### 4. `demo-resilient-client.sh`
Comprehensive demonstration with a 3-node cluster.
- Creates full 3-node Cassandra cluster
- Multiple failure scenarios (node failure, rolling restart, cluster outage)
- Interactive menu system
- Detailed logging and analysis
- Best for understanding complex failure patterns

```bash
# Interactive menu
./demo-resilient-client.sh

# Run specific demo
./demo-resilient-client.sh demo1  # Single node failure
./demo-resilient-client.sh demo2  # Rolling restart
./demo-resilient-client.sh demo3  # Cluster outage

# Run all demos
./demo-resilient-client.sh all
```

#### 5. `test-resilient-client.sh`
Comprehensive test suite with multiple scenarios.
- 3-node cluster setup
- Rolling restart testing
- Node failure simulation
- Comparison tests
- Full test suite option

```bash
# Interactive menu
./test-resilient-client.sh

# Quick commands
./test-resilient-client.sh start   # Start cluster
./test-resilient-client.sh stop    # Stop cluster
./test-resilient-client.sh test    # Run full test suite
```

### Helper Scripts

#### 6. `container-runtime.sh`
Helper script for Docker/Podman detection.
- Automatically detects available container runtime
- Sets up environment variables
- Used by other scripts (not meant to be run directly)

## Choosing the Right Script

| Use Case | Recommended Script |
|----------|-------------------|
| Quick demo to see the difference | `stable-demo.sh` |
| Test with existing Cassandra | `test-resilient.sh` |
| See failure recovery in action | `working-demo.sh` |
| Deep dive into failure scenarios | `demo-resilient-client.sh` |
| Automated testing | `test-resilient-client.sh` |

## What to Expect

When running the demos, you'll observe key differences between standard and resilient clients:

**Standard Client:**
- Basic connection pooling
- Minimal retry logic
- Standard DataStax driver behavior
- Limited visibility into connection state

**Resilient Client:**
- Host monitoring messages every 5 seconds
- Connection pool refresh every 60 seconds
- Enhanced failure detection and recovery
- Retry logic with exponential backoff
- Detailed logging of state changes
- Better handling of transient failures

## Example Output

```
[14:32:15] Running STANDARD client for 10 seconds...
[2025-01-06 14:32:15.123 INF] Cassandra Probe starting...
[2025-01-06 14:32:16.456 INF] Session established
[2025-01-06 14:32:16.789 INF] Query successful

[14:32:25] Running RESILIENT client for 10 seconds...
[2025-01-06 14:32:25.123 INF] Initializing ResilientCassandraClient
[2025-01-06 14:32:25.456 INF] Host monitoring timer started
[2025-01-06 14:32:25.789 INF] Initialized host state for /127.0.0.1: UP
[2025-01-06 14:32:26.123 INF] Query succeeded after 0 attempts
[2025-01-06 14:32:30.456 INF] [RESILIENT CLIENT] Monitoring 1 hosts...
```

## Troubleshooting

### Common Issues

**"Port already in use"**
- The demos use port 19042 to avoid conflicts with default Cassandra (9042)
- Stop any containers: `docker stop cassandra-demo` or `podman stop cassandra-demo`
- Check what's using the port: `lsof -i :19042` or `netstat -an | grep 19042`

**"Cannot connect to Cassandra"**
- Ensure Cassandra is running: `docker ps` or `podman ps`
- Check logs: `docker logs cassandra-demo` or `podman logs cassandra-demo`
- Wait longer - Cassandra can take 60-90 seconds to start
- Verify the contact point is correct

**"Build failed"**
- Ensure .NET SDK is installed: `dotnet --version`
- Try manual build: `cd .. && dotnet build -c Release`
- Check for build errors in the output

**"Container runtime not found"**
- Install Docker Desktop or Podman
- Ensure the service is running
- For Podman, you may need: `systemctl --user start podman.socket`

### Docker vs Podman

**Docker users:**
- Ensure Docker Desktop is running
- May need `sudo` on Linux
- Docker Compose should be included with Docker Desktop

**Podman users:**
- Install podman-compose: `pip install podman-compose`
- Start podman socket: `systemctl --user start podman.socket`
- May need to configure rootless containers

## Best Practices

1. **Start with `stable-demo.sh`** - Most reliable and straightforward
2. **Use `test-resilient.sh`** for existing Cassandra clusters
3. **Allow sufficient startup time** - Cassandra needs 60-90 seconds
4. **Check port availability** before running demos
5. **Clean up containers** when done:
   ```bash
   # For stable-demo.sh
   docker compose -f ../docker-compose.demo.yml down
   
   # For other scripts
   docker stop cassandra-demo && docker rm cassandra-demo
   ```
6. **Review logs** if something goes wrong - they contain valuable debugging info

## Understanding the Results

The resilient client provides several advantages:

1. **Better Observability**: Detailed logging of connection states and recovery attempts
2. **Proactive Monitoring**: Regular health checks prevent stale connections
3. **Graceful Degradation**: Continues operating even when some nodes fail
4. **Automatic Recovery**: Detects when failed nodes return and reconnects
5. **Retry Logic**: Intelligent retries with backoff for transient failures

These features make the resilient client more suitable for production environments where high availability is critical.