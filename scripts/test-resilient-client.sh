#!/bin/bash

# Resilient Client Testing Script
# This script demonstrates how the resilient client handles rolling restarts
# It creates a 3-node Cassandra cluster and performs various failure scenarios

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration
NETWORK_NAME="cassandra-test-network"
CASSANDRA_VERSION="4.1"
NUM_NODES=3
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

# Detect container runtime (Docker or Podman)
detect_container_runtime() {
    if command -v docker &> /dev/null; then
        if docker version &> /dev/null; then
            echo "docker"
            return
        fi
    fi
    
    if command -v podman &> /dev/null; then
        if podman version &> /dev/null; then
            echo "podman"
            return
        fi
    fi
    
    echo "error"
}

# Set container runtime
CONTAINER_RUNTIME=$(detect_container_runtime)
if [ "$CONTAINER_RUNTIME" = "error" ]; then
    echo "Error: Neither Docker nor Podman is available or running"
    exit 1
fi

echo -e "${GREEN}Using container runtime: $CONTAINER_RUNTIME${NC}"

# Function to print colored output
log() {
    local color=$1
    local message=$2
    echo -e "${color}[$(date +'%Y-%m-%d %H:%M:%S')] ${message}${NC}"
}

# Function to check if container is running
is_container_running() {
    local container=$1
    $CONTAINER_RUNTIME ps --format "table {{.Names}}" | grep -q "^${container}$"
}

# Function to wait for Cassandra node to be ready
wait_for_cassandra() {
    local container=$1
    local max_attempts=60
    local attempt=1
    
    log $YELLOW "Waiting for $container to be ready..."
    
    while [ $attempt -le $max_attempts ]; do
        if $CONTAINER_RUNTIME exec $container cqlsh -e "SELECT now() FROM system.local" >/dev/null 2>&1; then
            log $GREEN "$container is ready!"
            return 0
        fi
        
        echo -n "."
        sleep 2
        ((attempt++))
    done
    
    log $RED "$container failed to start within 2 minutes"
    return 1
}

# Function to create test schema
create_test_schema() {
    log $BLUE "Creating test keyspace and table..."
    $CONTAINER_RUNTIME exec cassandra-node-1 cqlsh -e "
        CREATE KEYSPACE IF NOT EXISTS resilient_test 
        WITH replication = {'class': 'SimpleStrategy', 'replication_factor': 3};
        
        CREATE TABLE IF NOT EXISTS resilient_test.test_data (
            id UUID PRIMARY KEY,
            timestamp timestamp,
            value text,
            client_type text
        );
    "
}

# Function to start Cassandra cluster
start_cluster() {
    log $BLUE "Starting Cassandra cluster with $NUM_NODES nodes..."
    
    # Check if containers already exist
    if is_container_running "cassandra-node-1"; then
        log $YELLOW "Cassandra cluster is already running."
        log $YELLOW "Use option 2 to stop it first if you want to restart."
        return 0
    fi
    
    # Create network if it doesn't exist
    $CONTAINER_RUNTIME network create $NETWORK_NAME 2>/dev/null || true
    
    # Start first node
    log $YELLOW "Starting cassandra-node-1 (seed node)..."
    $CONTAINER_RUNTIME run -d \
        --name cassandra-node-1 \
        --network $NETWORK_NAME \
        -p 9042:9042 \
        -e CASSANDRA_CLUSTER_NAME=TestCluster \
        -e CASSANDRA_DC=dc1 \
        -e CASSANDRA_ENDPOINT_SNITCH=GossipingPropertyFileSnitch \
        cassandra:$CASSANDRA_VERSION
    
    wait_for_cassandra "cassandra-node-1"
    
    # Start additional nodes
    for i in $(seq 2 $NUM_NODES); do
        log $YELLOW "Starting cassandra-node-$i..."
        $CONTAINER_RUNTIME run -d \
            --name cassandra-node-$i \
            --network $NETWORK_NAME \
            -e CASSANDRA_SEEDS=cassandra-node-1 \
            -e CASSANDRA_CLUSTER_NAME=TestCluster \
            -e CASSANDRA_DC=dc1 \
            -e CASSANDRA_ENDPOINT_SNITCH=GossipingPropertyFileSnitch \
            cassandra:$CASSANDRA_VERSION
        
        wait_for_cassandra "cassandra-node-$i"
    done
    
    # Wait for cluster to stabilize
    log $YELLOW "Waiting for cluster to stabilize..."
    sleep 10
    
    # Show cluster status
    log $GREEN "Cluster status:"
    $CONTAINER_RUNTIME exec cassandra-node-1 nodetool status
}

# Function to stop cluster
stop_cluster() {
    log $BLUE "Stopping Cassandra cluster..."
    
    for i in $(seq 1 $NUM_NODES); do
        $CONTAINER_RUNTIME stop cassandra-node-$i 2>/dev/null || true
        $CONTAINER_RUNTIME rm cassandra-node-$i 2>/dev/null || true
    done
    
    $CONTAINER_RUNTIME network rm $NETWORK_NAME 2>/dev/null || true
}

# Function to run the probe with resilient client
run_resilient_probe() {
    local duration=${1:-120}  # Default 2 minutes
    
    # Check if cluster is running
    if ! is_container_running "cassandra-node-1"; then
        log $RED "Cassandra cluster is not running. Please start it first (option 1)."
        return 1
    fi
    
    log $BLUE "Building the application..."
    cd "$PROJECT_ROOT"
    dotnet build -c Release
    
    log $BLUE "Starting resilient client probe (will run for ${duration}s)..."
    
    # Use localhost since we're mapping port 9042
    local contact_points="localhost:9042"
    
    # Run the probe with resilient client
    timeout $duration dotnet run --project src/CassandraProbe.Cli -- \
        --contact-points "$contact_points" \
        --resilient-client \
        --log-level Debug \
        --verbose \
        || true
}

# Function to perform rolling restart
rolling_restart() {
    log $BLUE "Performing rolling restart of Cassandra nodes..."
    
    for i in $(seq 1 $NUM_NODES); do
        log $YELLOW "Stopping cassandra-node-$i..."
        $CONTAINER_RUNTIME stop cassandra-node-$i
        
        log $YELLOW "Waiting 10 seconds..."
        sleep 10
        
        log $YELLOW "Starting cassandra-node-$i..."
        $CONTAINER_RUNTIME start cassandra-node-$i
        
        wait_for_cassandra "cassandra-node-$i"
        
        log $GREEN "cassandra-node-$i restarted successfully"
        
        # Wait between node restarts
        if [ $i -lt $NUM_NODES ]; then
            log $YELLOW "Waiting 15 seconds before next node..."
            sleep 15
        fi
    done
    
    log $GREEN "Rolling restart completed!"
}

# Function to simulate node failure
simulate_node_failure() {
    local node_num=${1:-2}
    local duration=${2:-30}
    
    log $RED "Simulating failure of cassandra-node-$node_num for ${duration}s..."
    $CONTAINER_RUNTIME pause cassandra-node-$node_num
    
    sleep $duration
    
    log $GREEN "Recovering cassandra-node-$node_num..."
    $CONTAINER_RUNTIME unpause cassandra-node-$node_num
}

# Function to run comparison test
run_comparison_test() {
    log $BLUE "Running comparison test: Standard vs Resilient client..."
    
    # Check if cluster is running
    if ! is_container_running "cassandra-node-1"; then
        log $RED "Cassandra cluster is not running. Please start it first (option 1)."
        return 1
    fi
    
    # Create a simple test script
    cat > /tmp/cassandra_probe_test.sh << EOF
#!/bin/bash
CONTAINER_RUNTIME="$CONTAINER_RUNTIME"
echo "=== STANDARD CLIENT TEST ==="
echo "Running queries with standard client during node failure..."

# TODO: Run standard probe

echo ""
echo "=== RESILIENT CLIENT TEST ==="
echo "Running queries with resilient client during node failure..."

# Run resilient probe
dotnet run --project src/CassandraProbe.Cli -- \\
    --contact-points "localhost:9042" \\
    --resilient-client \\
    --test-cql "INSERT INTO resilient_test.test_data (id, timestamp, value, client_type) VALUES (uuid(), toTimestamp(now()), 'test', 'resilient')" \\
    -i 2 \\
    --log-level Information &

PROBE_PID=\$!

# Let it run for a bit
sleep 10

# Simulate node failure
\$CONTAINER_RUNTIME stop cassandra-node-2

# Run for 30 seconds with node down
sleep 30

# Bring node back
\$CONTAINER_RUNTIME start cassandra-node-2

# Run for another 20 seconds
sleep 20

# Stop the probe
kill \$PROBE_PID 2>/dev/null || true

echo "Test completed!"
EOF
    
    chmod +x /tmp/cassandra_probe_test.sh
    cd "$PROJECT_ROOT"
    /tmp/cassandra_probe_test.sh
    rm -f /tmp/cassandra_probe_test.sh
}

# Function to show logs
show_logs() {
    local lines=${1:-50}
    log $BLUE "Showing last $lines lines of cassandra-node-1 logs:"
    $CONTAINER_RUNTIME logs --tail $lines cassandra-node-1
}

# Main menu
show_menu() {
    echo ""
    echo "Cassandra Resilient Client Test Suite"
    echo "====================================="
    echo "1) Start Cassandra cluster"
    echo "2) Stop Cassandra cluster"
    echo "3) Run resilient client demo"
    echo "4) Perform rolling restart test"
    echo "5) Simulate node failure"
    echo "6) Run comparison test (standard vs resilient)"
    echo "7) Show cluster status"
    echo "8) Show logs"
    echo "9) Run full test suite"
    echo "0) Exit"
    echo ""
    echo -n "Select option: "
}

# Function to run full test suite
run_full_test() {
    log $BLUE "Running full resilient client test suite..."
    
    # Clean up any existing cluster
    stop_cluster
    
    # Start fresh cluster
    start_cluster
    create_test_schema
    
    # Test 1: Basic operation
    log $BLUE "Test 1: Basic operation with resilient client"
    run_resilient_probe 30
    
    # Test 2: Node failure
    log $BLUE "Test 2: Single node failure"
    (simulate_node_failure 2 30 &)
    run_resilient_probe 60
    
    # Test 3: Rolling restart
    log $BLUE "Test 3: Rolling restart"
    (sleep 10 && rolling_restart &)
    run_resilient_probe 180
    
    # Show final cluster status
    log $GREEN "Final cluster status:"
    $CONTAINER_RUNTIME exec cassandra-node-1 nodetool status
    
    log $GREEN "Full test suite completed!"
}

# Parse command line arguments
if [ $# -gt 0 ]; then
    case "$1" in
        start)
            start_cluster
            create_test_schema
            ;;
        stop)
            stop_cluster
            ;;
        test)
            run_full_test
            ;;
        rolling-restart)
            rolling_restart
            ;;
        *)
            echo "Usage: $0 [start|stop|test|rolling-restart]"
            exit 1
            ;;
    esac
    exit 0
fi

# Interactive menu
while true; do
    show_menu
    read -r choice
    
    case $choice in
        1)
            start_cluster
            create_test_schema
            ;;
        2)
            stop_cluster
            ;;
        3)
            run_resilient_probe
            ;;
        4)
            rolling_restart
            ;;
        5)
            echo -n "Which node to fail (1-$NUM_NODES)? "
            read -r node_num
            echo -n "Duration in seconds? "
            read -r duration
            simulate_node_failure $node_num $duration
            ;;
        6)
            run_comparison_test
            ;;
        7)
            $CONTAINER_RUNTIME exec cassandra-node-1 nodetool status 2>/dev/null || log $RED "Cluster not running"
            ;;
        8)
            echo -n "How many lines? "
            read -r lines
            show_logs $lines
            ;;
        9)
            run_full_test
            ;;
        0)
            log $BLUE "Exiting..."
            exit 0
            ;;
        *)
            log $RED "Invalid option"
            ;;
    esac
done