#!/bin/bash

# Stable demo script for resilient client demonstration
# This version uses a more reliable approach for container management

set -e

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
MAGENTA='\033[0;35m'
CYAN='\033[0;36m'
NC='\033[0m'

# Script directory
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

# Source helpers
source "$SCRIPT_DIR/container-runtime.sh"
source "$SCRIPT_DIR/container-helper.sh"

# Setup runtime
if ! setup_container_runtime; then
    echo -e "${RED}Failed to setup container runtime${NC}"
    exit 1
fi

# Configuration
CONTAINER_NAME="cassandra-demo"
CONTAINER_PORT=19042
NETWORK_NAME="cassandra-demo-net"

# Logging functions
log() { echo -e "${BLUE}[$(date +'%H:%M:%S')] $1${NC}"; }
info() { echo -e "${CYAN}[$(date +'%H:%M:%S')] $1${NC}"; }
success() { echo -e "${GREEN}[$(date +'%H:%M:%S')] ✓ $1${NC}"; }
warn() { echo -e "${YELLOW}[$(date +'%H:%M:%S')] ⚠ $1${NC}"; }
error() { echo -e "${RED}[$(date +'%H:%M:%S')] ✗ $1${NC}"; }
header() {
    echo ""
    echo -e "${MAGENTA}=======================================${NC}"
    echo -e "${MAGENTA}$1${NC}"
    echo -e "${MAGENTA}=======================================${NC}"
    echo ""
}

# Cleanup function
cleanup() {
    log "Cleaning up..."
    $CONTAINER_RUNTIME stop $CONTAINER_NAME 2>/dev/null || true
    $CONTAINER_RUNTIME rm -f $CONTAINER_NAME 2>/dev/null || true
    $CONTAINER_RUNTIME network rm $NETWORK_NAME 2>/dev/null || true
}

# Start Cassandra
start_cassandra() {
    header "Starting Cassandra"
    
    # Check for existing container
    if $CONTAINER_RUNTIME ps -a --format "{{.Names}}" | grep -q "^${CONTAINER_NAME}$"; then
        warn "Container $CONTAINER_NAME already exists"
        read -p "Remove it and continue? (y/n) " -n 1 -r
        echo
        if [[ $REPLY =~ ^[Yy]$ ]]; then
            cleanup
        else
            error "Exiting to preserve existing container"
            exit 1
        fi
    fi
    
    # Check if port is available
    if lsof -i :$CONTAINER_PORT >/dev/null 2>&1; then
        error "Port $CONTAINER_PORT is already in use"
        error "Please stop the process using this port and try again"
        return 1
    fi
    
    # Create network
    $CONTAINER_RUNTIME network create $NETWORK_NAME 2>/dev/null || true
    
    # Start Cassandra
    log "Starting Cassandra container..."
    if ! $CONTAINER_RUNTIME run -d \
        --name $CONTAINER_NAME \
        --network $NETWORK_NAME \
        -p ${CONTAINER_PORT}:9042 \
        -e CASSANDRA_CLUSTER_NAME=DemoCluster \
        -e CASSANDRA_DC=datacenter1 \
        -e CASSANDRA_RACK=rack1 \
        -e MAX_HEAP_SIZE=512M \
        -e HEAP_NEWSIZE=128M \
        cassandra:4.1; then
        error "Failed to start Cassandra"
        return 1
    fi
    
    # Wait for Cassandra to be ready
    log "Waiting for Cassandra to be ready (may take 60-90 seconds)..."
    local attempts=0
    local max_attempts=45  # 45 * 2 = 90 seconds
    
    while [ $attempts -lt $max_attempts ]; do
        if $CONTAINER_RUNTIME exec $CONTAINER_NAME cqlsh -e "SELECT now() FROM system.local" >/dev/null 2>&1; then
            success "Cassandra is ready!"
            break
        fi
        
        # Show progress
        if [ $((attempts % 5)) -eq 0 ]; then
            echo -n "."
        fi
        
        sleep 2
        attempts=$((attempts + 1))
    done
    
    if [ $attempts -eq $max_attempts ]; then
        error "Cassandra failed to start within timeout"
        return 1
    fi
    
    # Create test keyspace
    log "Creating test keyspace..."
    $CONTAINER_RUNTIME exec $CONTAINER_NAME cqlsh -e "
        CREATE KEYSPACE IF NOT EXISTS resilient_test 
        WITH replication = {'class': 'SimpleStrategy', 'replication_factor': 1};
        
        CREATE TABLE IF NOT EXISTS resilient_test.test_table (
            id UUID PRIMARY KEY,
            timestamp timestamp,
            value text
        );"
    
    success "Cassandra is ready on port $CONTAINER_PORT"
    return 0
}

# Build project
build_project() {
    header "Building Project"
    
    cd "$PROJECT_ROOT"
    
    if dotnet build -c Debug > /dev/null 2>&1; then
        success "Build complete"
        return 0
    else
        error "Build failed"
        return 1
    fi
}

# Run standard client demo
run_standard_demo() {
    header "Standard Cassandra Client Demo"
    
    info "This demonstrates the standard client behavior"
    info "Watch for issues during node failures..."
    echo ""
    
    cd "$PROJECT_ROOT"
    
    # Run for a short time to demonstrate
    timeout 30s dotnet run --project src/CassandraProbe.Cli -- \
        --contact-points localhost:$CONTAINER_PORT \
        --test-cql "INSERT INTO resilient_test.test_table (id, timestamp, value) VALUES (uuid(), toTimestamp(now()), 'standard client test')" \
        -i 2 \
        --log-level Information \
        --connection-events || true
    
    echo ""
}

# Run resilient client demo
run_resilient_demo() {
    header "Resilient Cassandra Client Demo"
    
    info "This demonstrates the resilient client with automatic recovery"
    info "Notice the enhanced monitoring and recovery capabilities"
    echo ""
    
    cd "$PROJECT_ROOT"
    
    # Run for a short time to demonstrate
    timeout 30s dotnet run --project src/CassandraProbe.Cli -- \
        --contact-points localhost:$CONTAINER_PORT \
        --resilient-client \
        --test-cql "INSERT INTO resilient_test.test_table (id, timestamp, value) VALUES (uuid(), toTimestamp(now()), 'resilient client test')" \
        -i 2 \
        --log-level Information \
        --connection-events || true
    
    echo ""
}

# Show interactive menu
show_menu() {
    header "Recovery Scenarios"
    
    echo "You can now test various failure scenarios:"
    echo ""
    echo "1. Stop Cassandra:    ${CONTAINER_RUNTIME} stop $CONTAINER_NAME"
    echo "2. Start Cassandra:   ${CONTAINER_RUNTIME} start $CONTAINER_NAME"
    echo "3. Pause (network):   ${CONTAINER_RUNTIME} pause $CONTAINER_NAME"
    echo "4. Unpause:          ${CONTAINER_RUNTIME} unpause $CONTAINER_NAME"
    echo "5. Kill Cassandra:    ${CONTAINER_RUNTIME} kill $CONTAINER_NAME"
    echo ""
    echo "The resilient client will:"
    echo "- Detect failures within 5 seconds"
    echo "- Attempt automatic session/cluster recreation"
    echo "- Use circuit breakers to prevent cascading failures"
    echo "- Automatically recover when Cassandra returns"
    echo ""
}

# Main execution
main() {
    header "Cassandra Resilient Client Demo"
    
    # Build project
    if ! build_project; then
        exit 1
    fi
    
    # Start Cassandra
    if ! start_cassandra; then
        exit 1
    fi
    
    # Show both clients
    warn "First, showing STANDARD client behavior..."
    sleep 3
    run_standard_demo
    
    warn "Now, showing RESILIENT client behavior..."
    sleep 3
    run_resilient_demo
    
    # Show menu
    show_menu
    
    # Keep running for extended demo
    read -p "Press Enter to run extended demo (5 minutes) or Ctrl+C to exit..."
    
    header "Extended Resilient Client Demo"
    info "Running for 5 minutes - try the failure scenarios above"
    
    cd "$PROJECT_ROOT"
    dotnet run --project src/CassandraProbe.Cli -- \
        --contact-points localhost:$CONTAINER_PORT \
        --resilient-client \
        --test-cql "INSERT INTO resilient_test.test_table (id, timestamp, value) VALUES (uuid(), toTimestamp(now()), 'extended test')" \
        -i 2 \
        -d 300 \
        --log-level Information \
        --connection-events
}

# Trap cleanup
trap cleanup EXIT

# Run
main "$@"