#!/bin/bash

# Simple Resilient Client Demo
# This script demonstrates the resilient client against your existing Cassandra instance

set -e

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
MAGENTA='\033[0;35m'
NC='\033[0m'

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

echo -e "${MAGENTA}======================================${NC}"
echo -e "${MAGENTA}Resilient Client Demo${NC}"
echo -e "${MAGENTA}======================================${NC}"
echo ""

# Build the project
echo -e "${YELLOW}Building the project...${NC}"
cd "$PROJECT_ROOT"
if ! dotnet build -c Release >/dev/null 2>&1; then
    echo -e "${RED}Build failed!${NC}"
    exit 1
fi
echo -e "${GREEN}Build successful!${NC}"
echo ""

# Check what's on port 9042
echo -e "${YELLOW}Checking connections on port 9042...${NC}"
if command -v lsof >/dev/null 2>&1; then
    # Check for listening process
    if lsof -i :9042 | grep -q LISTEN; then
        echo "Cassandra is listening on port 9042"
    else
        # Check for connections to remote Cassandra
        REMOTE_HOST=$(lsof -i :9042 | grep -m1 "9042 (" | awk '{print $9}' | cut -d'>' -f2 | cut -d':' -f1)
        if [ -n "$REMOTE_HOST" ]; then
            echo "Found connections to remote Cassandra at: $REMOTE_HOST"
        fi
    fi
fi
echo ""

# Try to find Cassandra
echo -e "${YELLOW}Looking for Cassandra...${NC}"
CASSANDRA_HOST=""

# Function to test connection
test_connection() {
    local host_port=$1
    cd "$PROJECT_ROOT"
    if timeout 5 dotnet run --project src/CassandraProbe.Cli -- \
        --contact-points "$host_port" \
        --test-cql "SELECT now() FROM system.local" \
        -d 1 \
        --log-level Error >/dev/null 2>&1; then
        return 0
    else
        return 1
    fi
}

# Check various possible Cassandra locations
if test_connection "localhost:9042"; then
    CASSANDRA_HOST="localhost:9042"
    echo -e "${GREEN}Found Cassandra on localhost:9042${NC}"
elif test_connection "127.0.0.1:9042"; then
    CASSANDRA_HOST="127.0.0.1:9042"
    echo -e "${GREEN}Found Cassandra on 127.0.0.1:9042${NC}"
elif test_connection "localhost:19042"; then
    CASSANDRA_HOST="localhost:19042"
    echo -e "${GREEN}Found Cassandra on localhost:19042${NC}"
elif test_connection "localhost:9043"; then
    CASSANDRA_HOST="localhost:9043"
    echo -e "${GREEN}Found Cassandra on localhost:9043${NC}"
elif [ -n "$REMOTE_HOST" ] && test_connection "$REMOTE_HOST:9042"; then
    CASSANDRA_HOST="$REMOTE_HOST:9042"
    echo -e "${GREEN}Found Cassandra on $REMOTE_HOST:9042${NC}"
else
    echo -e "${RED}No Cassandra instance found!${NC}"
    echo ""
    echo "Please ensure Cassandra is running and accessible."
    echo ""
    echo "Common issues:"
    echo "1. Cassandra is not running - start it with:"
    echo "   docker run -d --name cassandra -p 9042:9042 cassandra:4.1"
    echo "   or"
    echo "   podman run -d --name cassandra -p 9042:9042 cassandra:4.1"
    echo ""
    echo "2. Cassandra is running on a different port"
    echo "3. Firewall or security settings blocking access"
    exit 1
fi

echo ""
echo -e "${BLUE}=== Running Demonstrations ===${NC}"
echo ""

# Demo 1: Standard Client
echo -e "${YELLOW}1. Standard Client (10 seconds)${NC}"
echo "   Watch for connection behavior..."
echo ""

timeout 10 dotnet run --project src/CassandraProbe.Cli -- \
    --contact-points "$CASSANDRA_HOST" \
    --test-cql "SELECT now() FROM system.local" \
    -i 1 \
    --log-level Information \
    --connection-events || true

echo ""
echo -e "${YELLOW}2. Resilient Client (10 seconds)${NC}"
echo "   Notice enhanced monitoring and recovery features..."
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
echo "Key differences:"
echo "• Resilient client monitors host states every 5 seconds"
echo "• Connection pools are refreshed every 60 seconds"
echo "• Automatic retry with exponential backoff"
echo "• Better failure detection and recovery"
echo ""
echo "To see failure handling in action, try:"
echo "1. Stop/start your Cassandra node while the probe is running"
echo "2. Simulate network issues"
echo "3. Perform a rolling restart"