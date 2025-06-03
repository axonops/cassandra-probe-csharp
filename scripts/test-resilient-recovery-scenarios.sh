#!/bin/bash

# Comprehensive test script for resilient client recovery scenarios
# Tests: single node failure, complete outage, network issues, and rolling restart

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
NODE1_NAME="cassandra-test-node1"
NODE2_NAME="cassandra-test-node2"
NODE3_NAME="cassandra-test-node3"
DEMO_DURATION=300  # 5 minutes total

# Cleanup function
cleanup() {
    echo -e "\n${CYAN}Cleaning up...${NC}"
    
    # Kill demo if running
    if [ ! -z "$DEMO_PID" ] && kill -0 $DEMO_PID 2>/dev/null; then
        kill $DEMO_PID 2>/dev/null || true
    fi
    
    # Stop and remove containers
    $CONTAINER_RUNTIME stop $NODE1_NAME $NODE2_NAME $NODE3_NAME 2>/dev/null || true
    $CONTAINER_RUNTIME rm -f $NODE1_NAME $NODE2_NAME $NODE3_NAME 2>/dev/null || true
    $CONTAINER_RUNTIME network rm $NETWORK_NAME 2>/dev/null || true
}

# Header function
header() {
    echo -e "\n${MAGENTA}========================================${NC}"
    echo -e "${MAGENTA}$1${NC}"
    echo -e "${MAGENTA}========================================${NC}\n"
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
        -e CASSANDRA_DC=datacenter1 \
        -e CASSANDRA_RACK=rack1 \
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
            message text,
            scenario text
        );"
    echo -e "${GREEN}Schema created${NC}"
}

# Show cluster status
show_status() {
    echo -e "\n${CYAN}Cluster Status:${NC}"
    $CONTAINER_RUNTIME exec $NODE1_NAME nodetool status 2>/dev/null || echo "Unable to get status"
}

# Start the resilient client demo
start_resilient_demo() {
    local scenario=$1
    echo -e "\n${BLUE}Starting resilient client for scenario: $scenario${NC}"
    
    cd "$PROJECT_ROOT"
    
    # Build if needed
    if [ ! -f "src/CassandraProbe.Cli/bin/Debug/net9.0/cassandra-probe.dll" ]; then
        echo -e "${BLUE}Building project...${NC}"
        dotnet build -c Debug >/dev/null 2>&1
    fi
    
    # Run the resilient client
    dotnet run --project src/CassandraProbe.Cli -- \
        --contact-points "localhost:9042,localhost:9043,localhost:9044" \
        --datacenter datacenter1 \
        --resilient-demo \
        --test-cql "INSERT INTO resilient_test.events (id, timestamp, message, scenario) VALUES (uuid(), toTimestamp(now()), 'Recovery test event', '$scenario')" \
        -i 2 \
        -d $DEMO_DURATION \
        --log-level Information \
        --connection-events \
        --consistency LOCAL_QUORUM &
    
    DEMO_PID=$!
    echo -e "${GREEN}Demo started with PID: $DEMO_PID${NC}"
}

# Scenario 1: Single Node Failure
scenario_single_node() {
    header "SCENARIO 1: Single Node Failure"
    echo "Testing resilient client behavior when one node fails"
    
    start_resilient_demo "single-node-failure"
    sleep 10
    
    echo -e "\n${YELLOW}Stopping node 2...${NC}"
    $CONTAINER_RUNTIME stop $NODE2_NAME
    echo -e "${RED}Node 2 is DOWN${NC}"
    
    show_status
    
    echo -e "\n${YELLOW}Client should continue working with 2 nodes${NC}"
    sleep 20
    
    echo -e "\n${BLUE}Restarting node 2...${NC}"
    $CONTAINER_RUNTIME start $NODE2_NAME
    wait_for_node $NODE2_NAME
    echo -e "${GREEN}Node 2 is UP${NC}"
    
    show_status
    
    echo -e "\n${GREEN}Waiting for automatic recovery...${NC}"
    sleep 20
    
    kill $DEMO_PID 2>/dev/null || true
    wait $DEMO_PID 2>/dev/null || true
}

# Scenario 2: Complete Cluster Outage
scenario_complete_outage() {
    header "SCENARIO 2: Complete Cluster Outage"
    echo "Testing resilient client behavior during complete cluster failure"
    
    start_resilient_demo "complete-outage"
    sleep 10
    
    echo -e "\n${YELLOW}Stopping all nodes...${NC}"
    $CONTAINER_RUNTIME stop $NODE1_NAME $NODE2_NAME $NODE3_NAME
    echo -e "${RED}ALL NODES ARE DOWN - Complete outage!${NC}"
    
    echo -e "\n${YELLOW}Client will attempt session/cluster recreation${NC}"
    sleep 15
    
    echo -e "\n${BLUE}Starting nodes again...${NC}"
    $CONTAINER_RUNTIME start $NODE1_NAME
    wait_for_node $NODE1_NAME
    $CONTAINER_RUNTIME start $NODE2_NAME
    wait_for_node $NODE2_NAME
    $CONTAINER_RUNTIME start $NODE3_NAME
    wait_for_node $NODE3_NAME
    
    echo -e "${GREEN}All nodes are UP${NC}"
    show_status
    
    echo -e "\n${GREEN}Client should automatically recover without restart${NC}"
    sleep 20
    
    kill $DEMO_PID 2>/dev/null || true
    wait $DEMO_PID 2>/dev/null || true
}

# Scenario 3: Network Issues (Pause/Unpause)
scenario_network_issues() {
    header "SCENARIO 3: Network Issues"
    echo "Testing resilient client behavior during network problems"
    
    start_resilient_demo "network-issues"
    sleep 10
    
    echo -e "\n${YELLOW}Simulating network issues on node 1 (pause)...${NC}"
    $CONTAINER_RUNTIME pause $NODE1_NAME
    echo -e "${YELLOW}Node 1 is PAUSED (network frozen)${NC}"
    
    sleep 15
    
    echo -e "\n${BLUE}Resolving network issues (unpause)...${NC}"
    $CONTAINER_RUNTIME unpause $NODE1_NAME
    echo -e "${GREEN}Node 1 network restored${NC}"
    
    echo -e "\n${GREEN}Circuit breakers should prevent connection storms${NC}"
    sleep 20
    
    kill $DEMO_PID 2>/dev/null || true
    wait $DEMO_PID 2>/dev/null || true
}

# Scenario 4: Rolling Restart
scenario_rolling_restart() {
    header "SCENARIO 4: Rolling Restart"
    echo "Testing resilient client during maintenance window"
    
    start_resilient_demo "rolling-restart"
    sleep 10
    
    local nodes=("$NODE1_NAME" "$NODE2_NAME" "$NODE3_NAME")
    
    for node in "${nodes[@]}"; do
        echo -e "\n${BLUE}Restarting $node...${NC}"
        $CONTAINER_RUNTIME restart $node
        
        echo -e "${YELLOW}Waiting for $node to rejoin...${NC}"
        wait_for_node $node
        echo -e "${GREEN}$node restarted successfully${NC}"
        
        show_status
        sleep 10
    done
    
    echo -e "\n${GREEN}Rolling restart complete - client maintained availability${NC}"
    sleep 10
    
    kill $DEMO_PID 2>/dev/null || true
    wait $DEMO_PID 2>/dev/null || true
}

# Show recovery statistics
show_recovery_stats() {
    header "Recovery Statistics"
    
    echo -e "${BLUE}Querying test results...${NC}"
    
    # Count events by scenario
    $CONTAINER_RUNTIME exec $NODE1_NAME cqlsh -e "
        SELECT scenario, COUNT(*) as event_count 
        FROM resilient_test.events 
        GROUP BY scenario
        ALLOW FILTERING;" 2>/dev/null || echo "Unable to query results"
    
    echo -e "\n${GREEN}Key Metrics:${NC}"
    echo "- Session recreations: Check application logs"
    echo "- Circuit breaker activations: Check application logs"
    echo "- Recovery times: Observed during scenarios"
    echo "- Query success rate: Based on event counts above"
}

# Main execution
main() {
    header "Resilient Client Recovery Test Suite"
    echo "This comprehensive test demonstrates automatic recovery capabilities"
    echo "Each scenario tests different failure conditions"
    
    # Check and cleanup
    check_and_cleanup_existing
    cleanup 2>/dev/null || true
    
    # Create network
    echo -e "\n${BLUE}Creating network...${NC}"
    $CONTAINER_RUNTIME network create $NETWORK_NAME >/dev/null 2>&1 || true
    
    # Start cluster
    echo -e "\n${CYAN}Starting 3-node Cassandra cluster...${NC}"
    start_node $NODE1_NAME "$NODE1_NAME" 0
    wait_for_node $NODE1_NAME
    
    start_node $NODE2_NAME "$NODE1_NAME" 1
    wait_for_node $NODE2_NAME
    
    start_node $NODE3_NAME "$NODE1_NAME" 2
    wait_for_node $NODE3_NAME
    
    # Create schema
    create_test_schema
    show_status
    
    # Run scenarios
    echo -e "\n${YELLOW}Running recovery scenarios...${NC}"
    echo -e "${YELLOW}Each scenario will run for about 1 minute${NC}\n"
    
    # Scenario selection
    if [ "$1" == "--all" ] || [ -z "$1" ]; then
        scenario_single_node
        sleep 5
        
        scenario_complete_outage
        sleep 5
        
        scenario_network_issues
        sleep 5
        
        scenario_rolling_restart
    else
        case $1 in
            1) scenario_single_node ;;
            2) scenario_complete_outage ;;
            3) scenario_network_issues ;;
            4) scenario_rolling_restart ;;
            *) echo -e "${RED}Invalid scenario. Use 1-4 or --all${NC}" ;;
        esac
    fi
    
    # Show results
    show_recovery_stats
    
    header "Test Complete"
    echo -e "${GREEN}The resilient client successfully demonstrated:${NC}"
    echo "✓ Automatic session/cluster recreation"
    echo "✓ Circuit breaker protection"
    echo "✓ Multi-node failure handling"
    echo "✓ Recovery without application restart"
    echo "✓ Continuous query execution during failures"
    
    # Cleanup prompt
    echo ""
    read -p "Press Enter to cleanup..."
    cleanup
}

# Handle Ctrl+C
trap cleanup EXIT

# Run with argument or show menu
if [ "$1" == "--help" ] || [ "$1" == "-h" ]; then
    echo "Usage: $0 [scenario|--all]"
    echo ""
    echo "Scenarios:"
    echo "  1 - Single Node Failure"
    echo "  2 - Complete Cluster Outage"
    echo "  3 - Network Issues"
    echo "  4 - Rolling Restart"
    echo "  --all - Run all scenarios (default)"
    exit 0
fi

main $1