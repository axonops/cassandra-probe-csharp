#!/bin/bash

# Test Recovery Scenarios for Improved Resilient Client
# This script demonstrates various failure scenarios and automatic recovery

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

# Source helpers
source "$PROJECT_ROOT/scripts/container-runtime.sh"
source "$PROJECT_ROOT/scripts/container-helper.sh"

# Setup runtime
if ! setup_container_runtime; then
    exit 1
fi

# Configuration
NETWORK_NAME="cassandra-test-net"
DC1_NODE1="cassandra-dc1-node1"
DC1_NODE2="cassandra-dc1-node2"
DC2_NODE1="cassandra-dc2-node1"

# Logging functions
log() { echo -e "${BLUE}[$(date +'%H:%M:%S')] $1${NC}"; }
info() { echo -e "${CYAN}[$(date +'%H:%M:%S')] $1${NC}"; }
success() { echo -e "${GREEN}[$(date +'%H:%M:%S')] ✓ $1${NC}"; }
warn() { echo -e "${YELLOW}[$(date +'%H:%M:%S')] ⚠ $1${NC}"; }
error() { echo -e "${RED}[$(date +'%H:%M:%S')] ✗ $1${NC}"; }
header() {
    echo ""
    echo -e "${MAGENTA}========================================${NC}"
    echo -e "${MAGENTA}$1${NC}"
    echo -e "${MAGENTA}========================================${NC}"
    echo ""
}

# Function to setup multi-DC cluster
setup_cluster() {
    header "Setting up Multi-DC Cassandra Cluster"
    
    # Check for existing containers
    if check_existing_containers "$CONTAINER_RUNTIME"; then
        if ! prompt_container_cleanup "$CONTAINER_RUNTIME"; then
            return 1
        fi
    fi
    
    # Create network
    log "Creating network..."
    $CONTAINER_RUNTIME network create $NETWORK_NAME 2>/dev/null || true
    
    # Start DC1 Node 1 (seed)
    log "Starting DC1 Node 1 (seed)..."
    $CONTAINER_RUNTIME run -d \
        --name $DC1_NODE1 \
        --network $NETWORK_NAME \
        -p 9042:9042 \
        -e CASSANDRA_CLUSTER_NAME=TestCluster \
        -e CASSANDRA_DC=dc1 \
        -e CASSANDRA_RACK=rack1 \
        -e CASSANDRA_ENDPOINT_SNITCH=GossipingPropertyFileSnitch \
        -e MAX_HEAP_SIZE=512M \
        -e HEAP_NEWSIZE=128M \
        cassandra:4.1
    
    # Wait for seed node
    wait_for_cassandra $DC1_NODE1
    
    # Start DC1 Node 2
    log "Starting DC1 Node 2..."
    $CONTAINER_RUNTIME run -d \
        --name $DC1_NODE2 \
        --network $NETWORK_NAME \
        -p 9043:9042 \
        -e CASSANDRA_CLUSTER_NAME=TestCluster \
        -e CASSANDRA_DC=dc1 \
        -e CASSANDRA_RACK=rack2 \
        -e CASSANDRA_SEEDS=$DC1_NODE1 \
        -e CASSANDRA_ENDPOINT_SNITCH=GossipingPropertyFileSnitch \
        -e MAX_HEAP_SIZE=512M \
        -e HEAP_NEWSIZE=128M \
        cassandra:4.1
    
    wait_for_cassandra $DC1_NODE2
    
    # Start DC2 Node 1
    log "Starting DC2 Node 1..."
    $CONTAINER_RUNTIME run -d \
        --name $DC2_NODE1 \
        --network $NETWORK_NAME \
        -p 9044:9042 \
        -e CASSANDRA_CLUSTER_NAME=TestCluster \
        -e CASSANDRA_DC=dc2 \
        -e CASSANDRA_RACK=rack1 \
        -e CASSANDRA_SEEDS=$DC1_NODE1 \
        -e CASSANDRA_ENDPOINT_SNITCH=GossipingPropertyFileSnitch \
        -e MAX_HEAP_SIZE=512M \
        -e HEAP_NEWSIZE=128M \
        cassandra:4.1
    
    wait_for_cassandra $DC2_NODE1
    
    # Create test keyspace
    log "Creating test keyspace..."
    $CONTAINER_RUNTIME exec $DC1_NODE1 cqlsh -e "
        CREATE KEYSPACE IF NOT EXISTS resilient_test 
        WITH replication = {
            'class': 'NetworkTopologyStrategy',
            'dc1': 2,
            'dc2': 1
        };
        
        CREATE TABLE IF NOT EXISTS resilient_test.events (
            id UUID PRIMARY KEY,
            timestamp timestamp,
            datacenter text,
            message text
        );"
    
    # Show cluster status
    log "Cluster status:"
    $CONTAINER_RUNTIME exec $DC1_NODE1 nodetool status
    
    success "Multi-DC cluster ready!"
}

# Function to wait for Cassandra
wait_for_cassandra() {
    local container=$1
    local max_wait=120
    local elapsed=0
    
    info "Waiting for $container to be ready..."
    while [ $elapsed -lt $max_wait ]; do
        if $CONTAINER_RUNTIME exec $container cqlsh -e "SELECT now() FROM system.local" >/dev/null 2>&1; then
            success "$container is ready!"
            return 0
        fi
        echo -n "."
        sleep 2
        ((elapsed+=2))
    done
    echo ""
    error "$container failed to start"
    return 1
}

# Function to run resilient client
run_resilient_client() {
    local scenario=$1
    local duration=$2
    local extra_args=$3
    
    cd "$PROJECT_ROOT"
    
    # Run in background
    dotnet run --project src/CassandraProbe.Cli -- \
        --contact-points "localhost:9042,localhost:9043,localhost:9044" \
        --resilient-client \
        --datacenter dc1 \
        --test-cql "INSERT INTO resilient_test.events (id, timestamp, datacenter, message) VALUES (uuid(), toTimestamp(now()), 'dc1', '$scenario')" \
        -i 2 \
        -d $duration \
        --log-level Information \
        --connection-events \
        $extra_args &
    
    echo $!
}

# Scenario 1: Single Node Failure
scenario_single_node() {
    header "Scenario 1: Single Node Failure & Recovery"
    
    info "Starting resilient client..."
    local pid=$(run_resilient_client "single-node-failure" 60)
    
    sleep 10
    warn "Stopping DC1 Node 2..."
    $CONTAINER_RUNTIME stop $DC1_NODE2
    
    sleep 20
    info "Client should continue working with degraded performance"
    
    success "Restarting DC1 Node 2..."
    $CONTAINER_RUNTIME start $DC1_NODE2
    wait_for_cassandra $DC1_NODE2
    
    sleep 20
    info "Client should recover automatically"
    
    # Wait for completion
    wait $pid || true
    
    success "Scenario 1 complete!"
}

# Scenario 2: Full DC Failure
scenario_dc_failure() {
    header "Scenario 2: Full Datacenter Failure"
    
    info "Starting resilient client..."
    local pid=$(run_resilient_client "dc-failure" 90)
    
    sleep 10
    warn "Stopping entire DC1..."
    $CONTAINER_RUNTIME stop $DC1_NODE1 $DC1_NODE2
    
    sleep 30
    error "DC1 is DOWN - client should fail over to DC2"
    
    success "Restarting DC1..."
    $CONTAINER_RUNTIME start $DC1_NODE1
    wait_for_cassandra $DC1_NODE1
    $CONTAINER_RUNTIME start $DC1_NODE2
    wait_for_cassandra $DC1_NODE2
    
    sleep 30
    info "Client should recover and prefer DC1 again"
    
    wait $pid || true
    success "Scenario 2 complete!"
}

# Scenario 3: Network Partition
scenario_network_partition() {
    header "Scenario 3: Network Partition (Container Pause)"
    
    info "Starting resilient client..."
    local pid=$(run_resilient_client "network-partition" 60)
    
    sleep 10
    warn "Simulating network partition (pausing containers)..."
    $CONTAINER_RUNTIME pause $DC1_NODE1 $DC1_NODE2
    
    sleep 20
    error "Network partition active - client in emergency mode"
    
    success "Healing network partition..."
    $CONTAINER_RUNTIME unpause $DC1_NODE1 $DC1_NODE2
    
    sleep 20
    info "Client should recover automatically"
    
    wait $pid || true
    success "Scenario 3 complete!"
}

# Scenario 4: Rolling Restart
scenario_rolling_restart() {
    header "Scenario 4: Rolling Restart (Maintenance)"
    
    info "Starting resilient client..."
    local pid=$(run_resilient_client "rolling-restart" 120)
    
    sleep 10
    
    # Rolling restart
    for node in $DC1_NODE1 $DC1_NODE2 $DC2_NODE1; do
        warn "Restarting $node..."
        $CONTAINER_RUNTIME restart $node
        wait_for_cassandra $node
        info "$node restarted successfully"
        sleep 10
    done
    
    info "Rolling restart complete - client should have maintained availability"
    
    wait $pid || true
    success "Scenario 4 complete!"
}

# Function to show results
show_results() {
    header "Recovery Test Results"
    
    log "Querying test results..."
    $CONTAINER_RUNTIME exec $DC1_NODE1 cqlsh -e "
        SELECT datacenter, message, count(*) as count 
        FROM resilient_test.events 
        GROUP BY datacenter, message
        ALLOW FILTERING;"
    
    log "Total events by datacenter:"
    $CONTAINER_RUNTIME exec $DC1_NODE1 cqlsh -e "
        SELECT datacenter, count(*) as total 
        FROM resilient_test.events 
        GROUP BY datacenter
        ALLOW FILTERING;"
}

# Cleanup function
cleanup() {
    header "Cleanup"
    
    log "Stopping containers..."
    $CONTAINER_RUNTIME stop $DC1_NODE1 $DC1_NODE2 $DC2_NODE1 2>/dev/null || true
    $CONTAINER_RUNTIME rm -f $DC1_NODE1 $DC1_NODE2 $DC2_NODE1 2>/dev/null || true
    $CONTAINER_RUNTIME network rm $NETWORK_NAME 2>/dev/null || true
    
    success "Cleanup complete!"
}

# Main menu
main_menu() {
    header "Resilient Client Recovery Test Suite"
    
    echo "This suite tests automatic recovery without application restart."
    echo ""
    echo "Options:"
    echo "1) Setup multi-DC cluster"
    echo "2) Run Scenario 1: Single Node Failure"
    echo "3) Run Scenario 2: Datacenter Failure"
    echo "4) Run Scenario 3: Network Partition"
    echo "5) Run Scenario 4: Rolling Restart"
    echo "6) Run All Scenarios"
    echo "7) Show Results"
    echo "8) Cleanup"
    echo "0) Exit"
    echo ""
    
    read -p "Select option: " choice
    
    case $choice in
        1) setup_cluster ;;
        2) scenario_single_node ;;
        3) scenario_dc_failure ;;
        4) scenario_network_partition ;;
        5) scenario_rolling_restart ;;
        6)
            setup_cluster
            scenario_single_node
            scenario_dc_failure
            scenario_network_partition
            scenario_rolling_restart
            show_results
            ;;
        7) show_results ;;
        8) cleanup ;;
        0) exit 0 ;;
        *) error "Invalid option" ;;
    esac
}

# Handle arguments
case "${1:-}" in
    setup) setup_cluster ;;
    scenario1) scenario_single_node ;;
    scenario2) scenario_dc_failure ;;
    scenario3) scenario_network_partition ;;
    scenario4) scenario_rolling_restart ;;
    all)
        setup_cluster
        scenario_single_node
        scenario_dc_failure
        scenario_network_partition
        scenario_rolling_restart
        show_results
        ;;
    cleanup) cleanup ;;
    *)
        # Interactive mode
        while true; do
            main_menu
            echo ""
            read -p "Press Enter to continue..."
        done
        ;;
esac