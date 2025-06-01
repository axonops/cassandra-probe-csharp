#!/bin/bash

# Quick Demo Script for Resilient Client
# This script provides a simple demonstration of the resilient client

set -e

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
MAGENTA='\033[0;35m'
NC='\033[0m'

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

# Source container runtime detection
source "$PROJECT_ROOT/scripts/container-runtime.sh"

# Setup runtime
if ! setup_container_runtime; then
    exit 1
fi

echo -e "${MAGENTA}======================================${NC}"
echo -e "${MAGENTA}Resilient Client Quick Demo${NC}"
echo -e "${MAGENTA}======================================${NC}"
echo ""

# Function to check if Cassandra is accessible
check_cassandra() {
    local host=${1:-localhost}
    local port=${2:-9042}
    
    cd "$PROJECT_ROOT"
    timeout 5 dotnet run --project src/CassandraProbe.Cli -- \
        --contact-points "$host:$port" \
        --test-cql "SELECT now() FROM system.local" \
        -d 1 \
        --log-level Error >/dev/null 2>&1
}

# Build the project
echo -e "${YELLOW}Building the project...${NC}"
cd "$PROJECT_ROOT"
if ! dotnet build -c Release >/dev/null 2>&1; then
    echo -e "${RED}Build failed!${NC}"
    exit 1
fi
echo -e "${GREEN}Build successful!${NC}"
echo ""

# Check if Cassandra is already running
echo -e "${YELLOW}Checking for existing Cassandra instance...${NC}"
if check_cassandra localhost 9042; then
    CASSANDRA_HOST="localhost:9042"
    echo -e "${GREEN}Found Cassandra on localhost:9042${NC}"
elif check_cassandra localhost 19042; then
    CASSANDRA_HOST="localhost:19042"
    echo -e "${GREEN}Found Cassandra on localhost:19042${NC}"
else
    echo -e "${YELLOW}No Cassandra instance found. Starting a single-node instance...${NC}"
    
    # Try to find an available port
    DEMO_PORT=29042
    
    # Check if our demo port is in use
    if command -v lsof >/dev/null 2>&1; then
        if lsof -i :$DEMO_PORT >/dev/null 2>&1; then
            echo -e "${RED}Port $DEMO_PORT is already in use!${NC}"
            echo "Please stop the process using port $DEMO_PORT."
            exit 1
        fi
    fi
    
    # Remove any existing cassandra-demo container
    $CONTAINER_RUNTIME rm -f cassandra-demo 2>/dev/null || true
    
    # Start a single Cassandra container on alternative port
    echo -e "${YELLOW}Starting Cassandra on port $DEMO_PORT...${NC}"
    if ! $CONTAINER_RUNTIME run -d \
        --name cassandra-demo \
        -p $DEMO_PORT:9042 \
        -e CASSANDRA_CLUSTER_NAME=DemoCluster \
        cassandra:4.1; then
        echo -e "${RED}Failed to start Cassandra container!${NC}"
        exit 1
    fi
    
    echo -e "${YELLOW}Waiting for Cassandra to start (this may take 30-60 seconds)...${NC}"
    
    # Check if container started successfully
    sleep 2
    if ! $CONTAINER_RUNTIME ps | grep -q cassandra-demo; then
        echo -e "${RED}Container failed to start. Checking logs...${NC}"
        $CONTAINER_RUNTIME logs cassandra-demo 2>&1 | tail -20
        $CONTAINER_RUNTIME rm cassandra-demo 2>/dev/null || true
        exit 1
    fi
    
    # Wait for Cassandra to be ready
    attempt=1
    max_attempts=60  # 2 minutes total
    while [ $attempt -le $max_attempts ]; do
        if check_cassandra localhost $DEMO_PORT; then
            break
        fi
        echo -n "."
        sleep 2
        ((attempt++))
    done
    echo ""
    
    if [ $attempt -gt $max_attempts ]; then
        echo -e "${RED}Cassandra failed to start!${NC}"
        $CONTAINER_RUNTIME stop cassandra-demo 2>/dev/null || true
        $CONTAINER_RUNTIME rm cassandra-demo 2>/dev/null || true
        exit 1
    fi
    
    CASSANDRA_HOST="localhost:$DEMO_PORT"
    echo -e "${GREEN}Cassandra is ready on port $DEMO_PORT!${NC}"
fi

echo ""
echo -e "${BLUE}=== Demonstration: Standard vs Resilient Client ===${NC}"
echo ""

# Run standard client
echo -e "${YELLOW}1. Running STANDARD client for 10 seconds...${NC}"
echo -e "${YELLOW}   Watch for any connection issues or timeouts.${NC}"
echo ""

timeout 10 dotnet run --project src/CassandraProbe.Cli -- \
    --contact-points "$CASSANDRA_HOST" \
    --test-cql "SELECT now() FROM system.local" \
    -i 1 \
    --log-level Information \
    --connection-events || true

echo ""
echo -e "${YELLOW}2. Running RESILIENT client for 10 seconds...${NC}"
echo -e "${YELLOW}   Notice the enhanced monitoring and recovery features.${NC}"
echo ""

timeout 10 dotnet run --project src/CassandraProbe.Cli -- \
    --contact-points "$CASSANDRA_HOST" \
    --resilient-client \
    --test-cql "SELECT now() FROM system.local" \
    -i 1 \
    --log-level Information \
    --connection-events || true

echo ""
echo -e "${GREEN}=== Demo Complete ===${NC}"
echo ""
echo "The resilient client provides:"
echo "  • Host monitoring every 5 seconds"
echo "  • Connection pool refresh every 60 seconds"
echo "  • Retry logic with exponential backoff"
echo "  • Enhanced failure detection and recovery"
echo ""

# Cleanup if we started a container
if $CONTAINER_RUNTIME ps --format "table {{.Names}}" | grep -q "cassandra-demo"; then
    echo -e "${YELLOW}Cleaning up demo container...${NC}"
    $CONTAINER_RUNTIME stop cassandra-demo >/dev/null 2>&1
    $CONTAINER_RUNTIME rm cassandra-demo >/dev/null 2>&1
    echo -e "${GREEN}Cleanup complete!${NC}"
fi

echo -e "${BLUE}For more comprehensive demos, try:${NC}"
echo "  ./scripts/test-resilient-client.sh    # Multi-node cluster tests"
echo "  ./scripts/demo-resilient-client.sh    # Interactive failure scenarios"