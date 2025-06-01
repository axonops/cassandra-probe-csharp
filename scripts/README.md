# Cassandra Probe Demo Scripts

This directory contains demonstration and test scripts for the Cassandra Probe resilient client functionality.

## Container Runtime Support

All scripts support both **Docker** and **Podman**. The scripts automatically detect which runtime is available and use the appropriate commands.

## Scripts Overview

### 1. `verify-resilient-client.sh`
Quick verification script that tests if the resilient client is working correctly.
- Checks for Cassandra availability
- Tests basic connectivity
- Verifies monitoring is active
- No container management required

```bash
./verify-resilient-client.sh
```

### 2. `test-resilient-client-simple.sh`
Simple test script that runs the resilient client against an existing Cassandra instance.
- Requires Cassandra to be already running
- Shows resilient client features
- Good for quick testing

```bash
./test-resilient-client-simple.sh
```

### 3. `demo-resilient-client.sh`
Interactive demo with a 3-node Cassandra cluster managed via Docker/Podman Compose.
- Creates and manages a test cluster
- Demonstrates failure scenarios
- Shows recovery behavior
- Interactive menu system

```bash
# Interactive mode
./demo-resilient-client.sh

# Direct commands
./demo-resilient-client.sh setup      # Set up cluster
./demo-resilient-client.sh demo1     # Single node failure
./demo-resilient-client.sh demo2     # Rolling restart
./demo-resilient-client.sh demo3     # Complete outage
./demo-resilient-client.sh cleanup   # Clean up
```

### 4. `test-resilient-client.sh`
Comprehensive test suite that creates containers and runs various failure scenarios.
- Creates individual Cassandra containers
- Tests node failures
- Tests rolling restarts
- Compares standard vs resilient client

```bash
# Interactive mode
./test-resilient-client.sh

# Command line mode
./test-resilient-client.sh start          # Start cluster
./test-resilient-client.sh test           # Run full test
./test-resilient-client.sh rolling-restart # Test rolling restart
./test-resilient-client.sh stop           # Stop cluster
```

### 5. `container-runtime.sh`
Helper script that detects and configures the container runtime.
- Automatically sourced by other scripts
- Sets CONTAINER_RUNTIME and COMPOSE_CMD variables
- Handles Docker/Podman differences

## Requirements

### For Docker
- Docker Engine installed and running
- Docker Compose (usually included with Docker Desktop)
- User must have permissions to run Docker commands

### For Podman
- Podman installed
- Podman Compose installed (`pip install podman-compose` or package manager)
- Podman socket activated if needed

## Quick Start

1. **Verify your setup:**
   ```bash
   ./verify-resilient-client.sh
   ```

2. **Run a simple test:**
   ```bash
   ./test-resilient-client-simple.sh
   ```

3. **Run the full demo:**
   ```bash
   ./demo-resilient-client.sh
   ```

## Troubleshooting

### "Neither Docker nor Podman found"
Install either Docker or Podman:
- Docker: https://docs.docker.com/get-docker/
- Podman: https://podman.io/getting-started/installation

### "Connection refused" errors
- Ensure Cassandra is running on the expected port
- Check firewall settings
- For containers, ensure they're properly started

### "docker-compose: command not found" (when using Docker)
Install Docker Compose:
```bash
# For Linux
sudo apt-get install docker-compose  # Debian/Ubuntu
sudo yum install docker-compose       # RHEL/CentOS
```

### "podman-compose: command not found" (when using Podman)
Install Podman Compose:
```bash
pip install podman-compose
# or
sudo apt-get install podman-compose  # Debian/Ubuntu
sudo dnf install podman-compose       # Fedora
```

## Port Configuration

The scripts use these default ports:
- 19042: First Cassandra node (modified to avoid conflicts)
- 9043: Second Cassandra node
- 9044: Third Cassandra node

If you have conflicts, modify the `docker-compose.test.yml` file.