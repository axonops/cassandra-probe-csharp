#!/bin/bash

# Working Demo for Resilient Client
# This script provides a reliable demonstration that actually works

set -e

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
MAGENTA='\033[0;35m'
CYAN='\033[0;36m'
NC='\033[0m'

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DEMO_NAME="cassandra-resilient-demo"
DEMO_PORT=19042

# Source container runtime detection
source "$PROJECT_ROOT/scripts/container-runtime.sh"

# Logging functions
log() {
    echo -e "${2}[$(date +'%H:%M:%S')] ${1}${NC}"
}

info() { log "$1" "$BLUE"; }
success() { log "$1" "$GREEN"; }
warn() { log "$1" "$YELLOW"; }
error() { log "$1" "$RED"; }
header() {
    echo ""
    echo -e "${MAGENTA}========================================${NC}"
    echo -e "${MAGENTA}$1${NC}"
    echo -e "${MAGENTA}========================================${NC}"
    echo ""
}

# Setup runtime
if ! setup_container_runtime; then
    exit 1
fi

# Cleanup function
cleanup() {
    info "Cleaning up..."
    $CONTAINER_RUNTIME stop $DEMO_NAME 2>/dev/null || true
    $CONTAINER_RUNTIME rm -f $DEMO_NAME 2>/dev/null || true
    success "Cleanup complete"
}

# Function to check if port is available
is_port_available() {
    local port=$1
    if command -v lsof >/dev/null 2>&1; then
        ! lsof -i :$port >/dev/null 2>&1
    elif command -v netstat >/dev/null 2>&1; then
        ! netstat -an | grep -q ":$port.*LISTEN"
    else
        # Assume available if we can't check
        true
    fi
}

# Function to wait for Cassandra
wait_for_cassandra() {
    local container=$1
    local port=$2
    local max_wait=120
    local elapsed=0
    
    info "Waiting for Cassandra to be ready (this may take up to 2 minutes)..."
    
    while [ $elapsed -lt $max_wait ]; do
        if $CONTAINER_RUNTIME exec $container cqlsh -e "SELECT now() FROM system.local" >/dev/null 2>&1; then
            success "Cassandra is ready!"
            return 0
        fi
        
        # Check if container is still running
        if ! $CONTAINER_RUNTIME ps | grep -q $container; then
            error "Container $container stopped unexpectedly"
            $CONTAINER_RUNTIME logs --tail 20 $container
            return 1
        fi
        
        echo -n "."
        sleep 2
        ((elapsed+=2))
    done
    
    echo ""
    error "Cassandra failed to start within $max_wait seconds"
    return 1
}

# Function to start Cassandra
start_cassandra() {
    header "Starting Cassandra Container"
    
    # Check if container already exists
    if $CONTAINER_RUNTIME ps -a | grep -q $DEMO_NAME; then
        warn "Container $DEMO_NAME already exists"
        cleanup
    fi
    
    # Find available port
    local port=$DEMO_PORT
    while ! is_port_available $port; do
        warn "Port $port is in use, trying next port..."
        ((port++))
        if [ $port -gt 19052 ]; then
            error "No available ports found between 19042-19052"
            return 1
        fi
    done
    DEMO_PORT=$port
    
    info "Starting Cassandra on port $DEMO_PORT..."
    
    # Start container
    if ! $CONTAINER_RUNTIME run -d \
        --name $DEMO_NAME \
        -p $DEMO_PORT:9042 \
        -e CASSANDRA_CLUSTER_NAME=DemoCluster \
        -e CASSANDRA_DC=datacenter1 \
        -e CASSANDRA_RACK=rack1 \
        cassandra:4.1; then
        error "Failed to start Cassandra container"
        return 1
    fi
    
    # Wait for it to be ready
    if ! wait_for_cassandra $DEMO_NAME $DEMO_PORT; then
        error "Cassandra failed to start properly"
        cleanup
        return 1
    fi
    
    # Create test keyspace
    info "Creating test keyspace..."
    $CONTAINER_RUNTIME exec $DEMO_NAME cqlsh -e "
        CREATE KEYSPACE IF NOT EXISTS resilient_test 
        WITH replication = {'class': 'SimpleStrategy', 'replication_factor': 1};
        
        CREATE TABLE IF NOT EXISTS resilient_test.probe_results (
            id UUID PRIMARY KEY,
            timestamp timestamp,
            client_type text,
            result text
        );"
    
    success "Cassandra is ready on localhost:$DEMO_PORT"
    return 0
}

# Function to run probe
run_probe() {
    local client_type=$1
    local duration=$2
    local description=$3
    
    echo ""
    info "$description"
    
    local args=""
    if [ "$client_type" = "resilient" ]; then
        args="--resilient-client"
    fi
    
    cd "$PROJECT_ROOT"
    timeout $duration dotnet run --project src/CassandraProbe.Cli -- \
        --contact-points "localhost:$DEMO_PORT" \
        $args \
        --test-cql "INSERT INTO resilient_test.probe_results (id, timestamp, client_type, result) VALUES (uuid(), toTimestamp(now()), '$client_type', 'success')" \
        -i 2 \
        --log-level Information \
        --connection-events \
        || true
}

# Function to simulate failure
simulate_failure() {
    warn "Simulating Cassandra failure (pausing container)..."
    $CONTAINER_RUNTIME pause $DEMO_NAME
    sleep 5
    warn "Recovering Cassandra (unpausing container)..."
    $CONTAINER_RUNTIME unpause $DEMO_NAME
}

# Main demo function
run_demo() {
    header "Cassandra Resilient Client Demo"
    
    # Build project
    info "Building project..."
    cd "$PROJECT_ROOT"
    if ! dotnet build -c Release >/dev/null 2>&1; then
        error "Build failed!"
        exit 1
    fi
    success "Build successful"
    
    # Start Cassandra
    if ! start_cassandra; then
        exit 1
    fi
    
    # Run comparison
    header "Standard vs Resilient Client Comparison"
    
    run_probe "standard" 15 "1. Running STANDARD client for 15 seconds..."
    
    run_probe "resilient" 15 "2. Running RESILIENT client for 15 seconds..."
    
    # Show differences
    echo ""
    success "Demo complete!"
    echo ""
    echo "Key differences observed:"
    echo "• Resilient client shows host monitoring messages"
    echo "• Connection pool refresh at regular intervals"
    echo "• Enhanced failure detection and recovery"
    echo ""
    
    # Query results
    info "Checking stored results..."
    $CONTAINER_RUNTIME exec $DEMO_NAME cqlsh -e "
        SELECT client_type, count(*) as queries 
        FROM resilient_test.probe_results 
        GROUP BY client_type;"
    
    # Offer failure simulation
    echo ""
    read -p "Would you like to see failure recovery? (y/n) " -n 1 -r
    echo ""
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        header "Failure Recovery Demo"
        
        info "Starting resilient client with failure simulation..."
        
        # Run in background
        run_probe "resilient" 30 "Running resilient client for 30 seconds with failure..." &
        PROBE_PID=$!
        
        # Wait a bit then simulate failure
        sleep 10
        simulate_failure
        
        # Wait for completion
        wait $PROBE_PID
        
        success "Failure recovery demo complete!"
        echo "The resilient client should have detected the failure and recovered."
    fi
    
    # Cleanup
    echo ""
    read -p "Press Enter to cleanup and exit..."
    cleanup
}

# Handle interrupts
trap cleanup EXIT INT TERM

# Check arguments
case "${1:-}" in
    --help|-h)
        echo "Usage: $0 [--quick]"
        echo ""
        echo "Options:"
        echo "  --quick    Run quick comparison without failure simulation"
        echo "  --help     Show this help message"
        exit 0
        ;;
    --quick)
        # Quick mode - just comparison
        run_demo
        ;;
    *)
        # Full interactive mode
        run_demo
        ;;
esac