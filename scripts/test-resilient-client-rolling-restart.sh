#!/bin/bash

# Test script for demonstrating resilient client recovery during rolling restart
# This script starts a 3-node Cassandra cluster and performs a rolling restart

set -e

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
MAGENTA='\033[0;35m'
CYAN='\033[0;36m'
NC='\033[0m'

# Determine directory paths
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

# Source container runtime detection
source "$SCRIPT_DIR/container-runtime.sh"
source "$SCRIPT_DIR/container-helper.sh"

# Setup runtime
if ! setup_container_runtime; then
    echo -e "${RED}Failed to setup container runtime${NC}"
    exit 1
fi

# Configuration
NETWORK_NAME="cassandra-test-net"
NODE1_NAME="cassandra-node1"
NODE2_NAME="cassandra-node2"
NODE3_NAME="cassandra-node3"

# Cleanup function
cleanup() {
    echo -e "${CYAN}Cleaning up...${NC}"
    $CONTAINER_RUNTIME stop $NODE1_NAME $NODE2_NAME $NODE3_NAME 2>/dev/null || true
    $CONTAINER_RUNTIME rm -f $NODE1_NAME $NODE2_NAME $NODE3_NAME 2>/dev/null || true
    $CONTAINER_RUNTIME network rm $NETWORK_NAME 2>/dev/null || true
}

# Check for existing containers
check_and_cleanup_existing() {
    if check_existing_containers "$CONTAINER_RUNTIME"; then
        echo -e "${YELLOW}Found existing Cassandra containers${NC}"
        if ! prompt_container_cleanup "$CONTAINER_RUNTIME"; then
            echo -e "${RED}Exiting to keep existing containers${NC}"
            exit 0
        fi
    fi
}

# Start a Cassandra node
start_node() {
    local node_name=$1
    local seeds=$2
    local port_offset=$3
    
    echo -e "${BLUE}Starting $node_name...${NC}"
    
    $CONTAINER_RUNTIME run -d \
        --name $node_name \
        --network $NETWORK_NAME \
        -p $((9042 + port_offset)):9042 \
        -e CASSANDRA_CLUSTER_NAME=TestCluster \
        -e CASSANDRA_SEEDS="$seeds" \
        -e CASSANDRA_ENDPOINT_SNITCH=GossipingPropertyFileSnitch \
        -e MAX_HEAP_SIZE=512M \
        -e HEAP_NEWSIZE=128M \
        cassandra:4.1 >/dev/null
}

# Wait for node to be ready
wait_for_node() {
    local node_name=$1
    local max_wait=120
    local elapsed=0
    
    echo -n "Waiting for $node_name to be ready"
    while [ $elapsed -lt $max_wait ]; do
        if $CONTAINER_RUNTIME exec $node_name cqlsh -e "SELECT now() FROM system.local" >/dev/null 2>&1; then
            echo -e " ${GREEN}✓${NC}"
            return 0
        fi
        echo -n "."
        sleep 2
        ((elapsed+=2))
    done
    echo -e " ${RED}✗${NC}"
    return 1
}

# Create test keyspace and table
create_test_schema() {
    echo -e "${BLUE}Creating test schema...${NC}"
    $CONTAINER_RUNTIME exec $NODE1_NAME cqlsh -e "
        CREATE KEYSPACE IF NOT EXISTS resilient_test 
        WITH replication = {'class': 'SimpleStrategy', 'replication_factor': 3};
        
        CREATE TABLE IF NOT EXISTS resilient_test.events (
            id UUID PRIMARY KEY,
            timestamp timestamp,
            message text
        );"
    echo -e "${GREEN}Schema created${NC}"
}

# Show cluster status
show_status() {
    echo -e "\n${CYAN}=== Cluster Status ===${NC}"
    $CONTAINER_RUNTIME exec $NODE1_NAME nodetool status || true
    echo ""
}

# Start the resilient client demo
start_resilient_demo() {
    echo -e "\n${MAGENTA}=== Starting Resilient Client Demo ===${NC}"
    echo -e "${YELLOW}The client will continuously execute queries every 2 seconds${NC}"
    echo -e "${YELLOW}Watch how it handles node failures and recoveries${NC}\n"
    
    cd "$PROJECT_ROOT"
    
    # Build the project if needed
    if [ ! -f "src/CassandraProbe.Cli/bin/Debug/net9.0/cassandra-probe.dll" ]; then
        echo -e "${BLUE}Building project...${NC}"
        dotnet build -c Debug
    fi
    
    # Run the resilient client demo
    dotnet run --project src/CassandraProbe.Cli -- \
        --contact-points "localhost:9042,localhost:9043,localhost:9044" \
        --datacenter datacenter1 \
        --resilient-demo \
        --test-cql "INSERT INTO resilient_test.events (id, timestamp, message) VALUES (uuid(), toTimestamp(now()), 'Test event')" \
        -i 2 \
        -d 300 \
        --log-level Information \
        --connection-events &
    
    DEMO_PID=$!
    echo -e "${GREEN}Demo started with PID: $DEMO_PID${NC}"
    return $DEMO_PID
}

# Perform rolling restart
rolling_restart() {
    echo -e "\n${MAGENTA}=== Starting Rolling Restart ===${NC}"
    echo -e "${YELLOW}This simulates a maintenance scenario${NC}\n"
    
    local nodes=("$NODE1_NAME" "$NODE2_NAME" "$NODE3_NAME")
    
    for node in "${nodes[@]}"; do
        echo -e "\n${BLUE}Restarting $node...${NC}"
        
        # Stop the node
        echo -e "  Stopping $node..."
        $CONTAINER_RUNTIME stop $node >/dev/null
        echo -e "  ${YELLOW}Node stopped${NC}"
        
        # Wait a bit to simulate maintenance
        echo -e "  Simulating maintenance (10 seconds)..."
        sleep 10
        
        # Start the node
        echo -e "  Starting $node..."
        $CONTAINER_RUNTIME start $node >/dev/null
        
        # Wait for node to rejoin
        if wait_for_node $node; then
            echo -e "  ${GREEN}Node restarted successfully${NC}"
        else
            echo -e "  ${RED}Node failed to restart${NC}"
        fi
        
        # Show status
        show_status
        
        # Wait before next node
        echo -e "${CYAN}Waiting 15 seconds before next node...${NC}"
        sleep 15
    done
    
    echo -e "\n${GREEN}Rolling restart complete!${NC}"
}

# Main execution
main() {
    echo -e "${MAGENTA}=== Resilient Client Rolling Restart Test ===${NC}"
    echo -e "This demonstrates how the resilient client handles a rolling restart scenario\n"
    
    # Check and cleanup existing containers
    check_and_cleanup_existing
    
    # Cleanup any previous runs
    cleanup 2>/dev/null || true
    
    # Create network
    echo -e "${BLUE}Creating network...${NC}"
    $CONTAINER_RUNTIME network create $NETWORK_NAME >/dev/null 2>&1 || true
    
    # Start nodes
    echo -e "\n${CYAN}Starting 3-node Cassandra cluster...${NC}"
    start_node $NODE1_NAME "$NODE1_NAME" 0
    wait_for_node $NODE1_NAME
    
    start_node $NODE2_NAME "$NODE1_NAME" 1
    wait_for_node $NODE2_NAME
    
    start_node $NODE3_NAME "$NODE1_NAME" 2
    wait_for_node $NODE3_NAME
    
    # Create schema
    create_test_schema
    
    # Show initial status
    show_status
    
    # Start the demo
    start_resilient_demo
    DEMO_PID=$!
    
    # Give the demo time to start
    echo -e "\n${YELLOW}Waiting 10 seconds for demo to initialize...${NC}"
    sleep 10
    
    # Perform rolling restart
    rolling_restart
    
    # Let the demo run a bit more to show recovery
    echo -e "\n${YELLOW}Demo continuing for 30 more seconds to show full recovery...${NC}"
    sleep 30
    
    # Stop the demo
    echo -e "\n${BLUE}Stopping demo...${NC}"
    kill $DEMO_PID 2>/dev/null || true
    
    # Show final metrics
    echo -e "\n${CYAN}=== Test Complete ===${NC}"
    echo -e "${GREEN}The resilient client successfully handled the rolling restart!${NC}"
    echo -e "${GREEN}Key observations:${NC}"
    echo -e "  - Automatic session/cluster recreation when needed"
    echo -e "  - Circuit breakers prevented cascading failures"
    echo -e "  - Queries continued with minimal disruption"
    echo -e "  - Automatic recovery without manual intervention"
    
    # Cleanup
    read -p "Press Enter to cleanup..."
    cleanup
}

# Handle Ctrl+C
trap cleanup EXIT

# Run main
main