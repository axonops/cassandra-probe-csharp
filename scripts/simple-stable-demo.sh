#!/bin/bash

# Simple Stable Demo - Works reliably with Docker or Podman
# No docker-compose dependency

set -e

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
MAGENTA='\033[0;35m'
NC='\033[0m'

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CONTAINER_NAME="cassandra-probe-demo"
DEMO_PORT=19042

# Source container helper functions
source "$PROJECT_ROOT/scripts/container-helper.sh"

# Detect container runtime
if command -v docker >/dev/null 2>&1; then
    RUNTIME="docker"
elif command -v podman >/dev/null 2>&1; then
    RUNTIME="podman"
else
    echo -e "${RED}Error: Neither Docker nor Podman found${NC}"
    exit 1
fi

echo -e "${GREEN}Using: $RUNTIME${NC}"

# Functions
log() {
    echo -e "${BLUE}[$(date +'%H:%M:%S')] $1${NC}"
}

error() {
    echo -e "${RED}[$(date +'%H:%M:%S')] ERROR: $1${NC}"
}

success() {
    echo -e "${GREEN}[$(date +'%H:%M:%S')] SUCCESS: $1${NC}"
}

# Cleanup function
cleanup() {
    log "Cleaning up..."
    $RUNTIME stop $CONTAINER_NAME >/dev/null 2>&1 || true
    $RUNTIME rm -f $CONTAINER_NAME >/dev/null 2>&1 || true
    success "Cleanup complete"
}

# Start Cassandra
start_cassandra() {
    log "Starting Cassandra..."
    
    # Check for existing containers
    if check_existing_containers "$RUNTIME"; then
        if ! prompt_container_cleanup "$RUNTIME"; then
            return 1
        fi
    fi
    
    # Check port availability
    if lsof -i :$DEMO_PORT >/dev/null 2>&1; then
        error "Port $DEMO_PORT is already in use"
        return 1
    fi
    
    # Start container
    log "Starting Cassandra container on port $DEMO_PORT..."
    if ! $RUNTIME run -d \
        --name $CONTAINER_NAME \
        -p $DEMO_PORT:9042 \
        -e CASSANDRA_CLUSTER_NAME=DemoCluster \
        -e MAX_HEAP_SIZE=512M \
        -e HEAP_NEWSIZE=128M \
        cassandra:4.1; then
        error "Failed to start Cassandra container"
        return 1
    fi
    
    # Wait for Cassandra to be ready
    log "Waiting for Cassandra to start (60-90 seconds)..."
    local attempts=0
    local max_attempts=45  # 90 seconds
    
    while [ $attempts -lt $max_attempts ]; do
        if $RUNTIME exec $CONTAINER_NAME cqlsh -e "SELECT now() FROM system.local" >/dev/null 2>&1; then
            success "Cassandra is ready!"
            break
        fi
        
        # Check if container is still running
        if ! $RUNTIME ps | grep -q $CONTAINER_NAME; then
            error "Container stopped unexpectedly"
            $RUNTIME logs --tail 20 $CONTAINER_NAME
            return 1
        fi
        
        echo -n "."
        sleep 2
        ((attempts++))
    done
    echo ""
    
    if [ $attempts -eq $max_attempts ]; then
        error "Cassandra failed to start in time"
        return 1
    fi
    
    # Create test schema
    log "Creating test schema..."
    $RUNTIME exec $CONTAINER_NAME cqlsh -e "
        CREATE KEYSPACE IF NOT EXISTS demo 
        WITH replication = {'class': 'SimpleStrategy', 'replication_factor': 1};
        
        CREATE TABLE IF NOT EXISTS demo.test (
            id UUID PRIMARY KEY,
            timestamp timestamp,
            client text,
            message text
        );"
    
    success "Cassandra ready on localhost:$DEMO_PORT"
    return 0
}

# Build project
build_project() {
    log "Building project..."
    cd "$PROJECT_ROOT"
    if dotnet build -c Release >/dev/null 2>&1; then
        success "Build complete"
        return 0
    else
        error "Build failed"
        return 1
    fi
}

# Run probe
run_probe() {
    local client_type=$1
    local duration=$2
    
    echo ""
    if [ "$client_type" = "resilient" ]; then
        log "Running RESILIENT client for $duration seconds..."
        local args="--resilient-client"
    else
        log "Running STANDARD client for $duration seconds..."
        local args=""
    fi
    
    cd "$PROJECT_ROOT"
    dotnet run --project src/CassandraProbe.Cli -- \
        --contact-points "localhost:$DEMO_PORT" \
        $args \
        --test-cql "INSERT INTO demo.test (id, timestamp, client, message) VALUES (uuid(), toTimestamp(now()), '$client_type', 'test')" \
        -i 2 \
        -d $duration \
        --log-level Information \
        --connection-events
}

# Main execution
main() {
    echo -e "${MAGENTA}=======================================${NC}"
    echo -e "${MAGENTA}Cassandra Resilient Client Demo${NC}"
    echo -e "${MAGENTA}=======================================${NC}"
    echo ""
    
    # Build
    if ! build_project; then
        exit 1
    fi
    
    # Start Cassandra
    if ! start_cassandra; then
        cleanup
        exit 1
    fi
    
    # Run demos
    echo ""
    echo -e "${MAGENTA}=== Side-by-Side Comparison ===${NC}"
    
    run_probe "standard" 10
    run_probe "resilient" 10
    
    # Show results
    echo ""
    log "Query results:"
    $RUNTIME exec $CONTAINER_NAME cqlsh -e "
        SELECT client, count(*) as total_queries 
        FROM demo.test 
        GROUP BY client;"
    
    echo ""
    success "Demo complete!"
    echo ""
    echo "Key observations:"
    echo "• Resilient client logs host monitoring activities"
    echo "• Connection pool refresh happens periodically"
    echo "• Enhanced failure detection capabilities"
    echo ""
    
    # Cleanup prompt
    read -p "Press Enter to cleanup and exit..."
    cleanup
}

# Handle cleanup on exit
trap cleanup EXIT INT TERM

# Run main
main "$@"