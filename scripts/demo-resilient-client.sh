#!/bin/bash

# Resilient Client Demonstration Script
# This script shows how the resilient client handles various failure scenarios
# compared to the standard client behavior

set -e

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
MAGENTA='\033[0;35m'
CYAN='\033[0;36m'
NC='\033[0m'

# Configuration
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
LOG_DIR="$PROJECT_ROOT/logs/resilient-client-demo"
COMPOSE_FILE="$PROJECT_ROOT/docker-compose.test.yml"

# Logging functions
log() {
    local color=$1
    local message=$2
    echo -e "${color}[$(date +'%H:%M:%S')] ${message}${NC}"
}

header() {
    echo ""
    echo -e "${MAGENTA}========================================${NC}"
    echo -e "${MAGENTA}$1${NC}"
    echo -e "${MAGENTA}========================================${NC}"
    echo ""
}

# Setup function
setup() {
    header "Setting up test environment"
    
    # Create log directory
    mkdir -p "$LOG_DIR"
    
    # Build the application
    log $BLUE "Building application..."
    cd "$PROJECT_ROOT"
    dotnet build -c Release > "$LOG_DIR/build.log" 2>&1
    
    # Start Cassandra cluster
    log $BLUE "Starting 3-node Cassandra cluster..."
    docker-compose -f "$COMPOSE_FILE" up -d
    
    # Wait for cluster to be ready
    log $YELLOW "Waiting for cluster to be ready (this may take 2-3 minutes)..."
    local max_attempts=90
    local attempt=1
    
    while [ $attempt -le $max_attempts ]; do
        if docker exec cassandra-node-1 cqlsh -e "SELECT now() FROM system.local" >/dev/null 2>&1; then
            break
        fi
        echo -n "."
        sleep 2
        ((attempt++))
    done
    echo ""
    
    if [ $attempt -gt $max_attempts ]; then
        log $RED "Cluster failed to start!"
        exit 1
    fi
    
    # Wait for all nodes to join
    log $YELLOW "Waiting for all nodes to join cluster..."
    while true; do
        local up_nodes=$(docker exec cassandra-node-1 nodetool status | grep "^UN" | wc -l)
        if [ "$up_nodes" -eq "3" ]; then
            break
        fi
        sleep 2
    done
    
    log $GREEN "Cluster is ready!"
    docker exec cassandra-node-1 nodetool status
    
    # Create test schema
    log $BLUE "Creating test keyspace and table..."
    docker exec cassandra-node-1 cqlsh -e "
        CREATE KEYSPACE IF NOT EXISTS probe_test 
        WITH replication = {'class': 'SimpleStrategy', 'replication_factor': 3};
        
        CREATE TABLE IF NOT EXISTS probe_test.events (
            id UUID,
            timestamp timestamp,
            event_type text,
            message text,
            PRIMARY KEY (id, timestamp)
        );
    "
}

# Cleanup function
cleanup() {
    header "Cleaning up"
    
    log $BLUE "Stopping Cassandra cluster..."
    docker-compose -f "$COMPOSE_FILE" down -v
    
    log $GREEN "Cleanup completed!"
}

# Function to run probe in background
run_probe_background() {
    local client_type=$1
    local log_file=$2
    local extra_args=$3
    
    cd "$PROJECT_ROOT"
    
    if [ "$client_type" == "resilient" ]; then
        dotnet run --project src/CassandraProbe.Cli -- \
            --contact-points "localhost:19042,localhost:9043,localhost:9044" \
            --resilient-client \
            --test-cql "INSERT INTO probe_test.events (id, timestamp, event_type, message) VALUES (uuid(), toTimestamp(now()), 'probe', 'resilient-client-test')" \
            -i 1 \
            --log-level Debug \
            --connection-events \
            $extra_args \
            > "$log_file" 2>&1 &
    else
        dotnet run --project src/CassandraProbe.Cli -- \
            --contact-points "localhost:19042,localhost:9043,localhost:9044" \
            --test-cql "INSERT INTO probe_test.events (id, timestamp, event_type, message) VALUES (uuid(), toTimestamp(now()), 'probe', 'standard-client-test')" \
            -i 1 \
            --log-level Information \
            --connection-events \
            $extra_args \
            > "$log_file" 2>&1 &
    fi
    
    echo $!
}

# Demo 1: Single Node Failure
demo_single_node_failure() {
    header "Demo 1: Single Node Failure"
    
    log $BLUE "This demo shows how clients react when a single node fails"
    echo ""
    
    # Start both clients
    log $CYAN "Starting standard client probe..."
    local standard_pid=$(run_probe_background "standard" "$LOG_DIR/demo1-standard.log" "-d 2")
    
    log $CYAN "Starting resilient client probe..."
    local resilient_pid=$(run_probe_background "resilient" "$LOG_DIR/demo1-resilient.log" "-d 2")
    
    # Let them run normally
    log $GREEN "Both clients running normally for 20 seconds..."
    sleep 20
    
    # Stop node 2
    log $RED "STOPPING cassandra-node-2..."
    docker-compose -f "$COMPOSE_FILE" stop cassandra-node-2
    
    log $YELLOW "Node 2 is DOWN - observing client behavior for 30 seconds..."
    sleep 30
    
    # Restart node 2
    log $GREEN "STARTING cassandra-node-2..."
    docker-compose -f "$COMPOSE_FILE" start cassandra-node-2
    
    log $YELLOW "Node 2 is RECOVERING - observing recovery for 30 seconds..."
    sleep 30
    
    # Stop probes
    kill $standard_pid $resilient_pid 2>/dev/null || true
    
    # Analyze results
    log $BLUE "Analyzing results..."
    echo ""
    
    echo -e "${CYAN}Standard Client:${NC}"
    grep -E "(ERROR|WARN|Failed|Timeout|Exception)" "$LOG_DIR/demo1-standard.log" | tail -10 || echo "No errors found"
    
    echo ""
    echo -e "${CYAN}Resilient Client:${NC}"
    grep -E "(RESILIENT CLIENT|Host.*DOWN|Host.*UP|Detection|Recovery)" "$LOG_DIR/demo1-resilient.log" | tail -10 || echo "No recovery events found"
    
    echo ""
    log $GREEN "Demo 1 completed! Check logs for detailed behavior."
}

# Demo 2: Rolling Restart
demo_rolling_restart() {
    header "Demo 2: Rolling Restart"
    
    log $BLUE "This demo shows how clients handle a rolling restart (maintenance scenario)"
    echo ""
    
    # Start both clients with longer duration
    log $CYAN "Starting standard client probe..."
    local standard_pid=$(run_probe_background "standard" "$LOG_DIR/demo2-standard.log" "-d 5")
    
    log $CYAN "Starting resilient client probe..."
    local resilient_pid=$(run_probe_background "resilient" "$LOG_DIR/demo2-resilient.log" "-d 5")
    
    # Let them run normally
    log $GREEN "Both clients running normally for 15 seconds..."
    sleep 15
    
    # Rolling restart
    for node in "cassandra-node-1" "cassandra-node-2" "cassandra-node-3"; do
        log $YELLOW "Restarting $node..."
        docker-compose -f "$COMPOSE_FILE" restart $node
        
        log $YELLOW "Waiting for $node to rejoin (40 seconds)..."
        sleep 40
        
        # Show current cluster state
        docker exec cassandra-node-1 nodetool status 2>/dev/null || docker exec cassandra-node-2 nodetool status 2>/dev/null || true
    done
    
    log $GREEN "Rolling restart completed, observing recovery for 30 seconds..."
    sleep 30
    
    # Stop probes
    kill $standard_pid $resilient_pid 2>/dev/null || true
    
    # Analyze results
    log $BLUE "Analyzing results..."
    echo ""
    
    # Count successes and failures
    local standard_success=$(grep -c "successful" "$LOG_DIR/demo2-standard.log" || echo "0")
    local standard_fail=$(grep -c -E "(Failed|ERROR|Exception)" "$LOG_DIR/demo2-standard.log" || echo "0")
    
    local resilient_success=$(grep -c "succeeded" "$LOG_DIR/demo2-resilient.log" || echo "0")
    local resilient_fail=$(grep -c "failed after all retry" "$LOG_DIR/demo2-resilient.log" || echo "0")
    
    echo -e "${CYAN}Standard Client:${NC}"
    echo "  Successful queries: $standard_success"
    echo "  Failed queries: $standard_fail"
    
    echo ""
    echo -e "${CYAN}Resilient Client:${NC}"
    echo "  Successful queries: $resilient_success"
    echo "  Failed queries: $resilient_fail"
    echo "  State transitions detected: $(grep -c "state changes detected" "$LOG_DIR/demo2-resilient.log" || echo "0")"
    
    echo ""
    log $GREEN "Demo 2 completed! The resilient client should show better recovery."
}

# Demo 3: Complete Cluster Outage
demo_cluster_outage() {
    header "Demo 3: Complete Cluster Outage"
    
    log $BLUE "This demo shows how clients handle complete cluster failure"
    echo ""
    
    # Start both clients
    log $CYAN "Starting probes..."
    local standard_pid=$(run_probe_background "standard" "$LOG_DIR/demo3-standard.log" "-d 3")
    local resilient_pid=$(run_probe_background "resilient" "$LOG_DIR/demo3-resilient.log" "-d 3")
    
    sleep 15
    
    # Stop entire cluster
    log $RED "STOPPING ENTIRE CLUSTER..."
    docker-compose -f "$COMPOSE_FILE" stop
    
    log $YELLOW "Cluster is DOWN - observing behavior for 30 seconds..."
    sleep 30
    
    # Restart cluster
    log $GREEN "STARTING CLUSTER..."
    docker-compose -f "$COMPOSE_FILE" start
    
    log $YELLOW "Cluster is RECOVERING - waiting for recovery..."
    
    # Wait for at least one node
    local attempt=1
    while [ $attempt -le 60 ]; do
        if docker exec cassandra-node-1 cqlsh -e "SELECT now() FROM system.local" >/dev/null 2>&1; then
            log $GREEN "Cluster is responding!"
            break
        fi
        sleep 2
        ((attempt++))
    done
    
    sleep 30
    
    # Stop probes
    kill $standard_pid $resilient_pid 2>/dev/null || true
    
    # Analyze
    log $BLUE "Analyzing recovery behavior..."
    echo ""
    
    echo -e "${CYAN}Standard Client - Errors during outage:${NC}"
    grep -A2 -B2 "STOPPING ENTIRE CLUSTER" "$LOG_DIR/demo3-standard.log" | grep -E "(ERROR|Failed|Exception)" | head -5
    
    echo ""
    echo -e "${CYAN}Resilient Client - Recovery detection:${NC}"
    grep -E "(Circuit|Recovery|Reconnect|Host.*UP)" "$LOG_DIR/demo3-resilient.log" | tail -10
    
    echo ""
    log $GREEN "Demo 3 completed!"
}

# Show real-time comparison
show_realtime_comparison() {
    header "Real-time Comparison: Standard vs Resilient"
    
    log $BLUE "Running both clients side-by-side. Watch the behavior difference!"
    echo ""
    
    # Create a script to show both outputs
    cat > "$LOG_DIR/compare.sh" << 'EOF'
#!/bin/bash
tail -f logs/resilient-client-demo/realtime-*.log | awk '
/realtime-standard.log/ {mode="STANDARD"}
/realtime-resilient.log/ {mode="RESILIENT"}
mode=="STANDARD" && /ERROR|Failed|Timeout/ {print "\033[0;31m[STANDARD] " $0 "\033[0m"; next}
mode=="RESILIENT" && /RESILIENT CLIENT|succeeded|Recovery/ {print "\033[0;32m[RESILIENT] " $0 "\033[0m"; next}
'
EOF
    chmod +x "$LOG_DIR/compare.sh"
    
    # Start probes
    run_probe_background "standard" "$LOG_DIR/realtime-standard.log" "" >/dev/null
    run_probe_background "resilient" "$LOG_DIR/realtime-resilient.log" "" >/dev/null
    
    log $YELLOW "Press Ctrl+C to stop the comparison"
    echo ""
    
    # Run comparison
    cd "$PROJECT_ROOT"
    "$LOG_DIR/compare.sh"
}

# Main menu
main_menu() {
    while true; do
        header "Cassandra Resilient Client Demonstration"
        
        echo "1) Setup test environment"
        echo "2) Demo 1: Single node failure"
        echo "3) Demo 2: Rolling restart (maintenance)"
        echo "4) Demo 3: Complete cluster outage"
        echo "5) Show real-time comparison"
        echo "6) View cluster status"
        echo "7) Cleanup environment"
        echo "8) Run all demos"
        echo "0) Exit"
        echo ""
        echo -n "Select option: "
        read -r choice
        
        case $choice in
            1) setup ;;
            2) demo_single_node_failure ;;
            3) demo_rolling_restart ;;
            4) demo_cluster_outage ;;
            5) show_realtime_comparison ;;
            6) docker exec cassandra-node-1 nodetool status ;;
            7) cleanup ;;
            8) 
                setup
                demo_single_node_failure
                sleep 5
                demo_rolling_restart
                sleep 5
                demo_cluster_outage
                ;;
            0) 
                log $BLUE "Exiting..."
                exit 0
                ;;
            *) log $RED "Invalid option" ;;
        esac
        
        echo ""
        echo "Press Enter to continue..."
        read -r
    done
}

# Handle script arguments
if [ $# -gt 0 ]; then
    case "$1" in
        setup) setup ;;
        demo1) demo_single_node_failure ;;
        demo2) demo_rolling_restart ;;
        demo3) demo_cluster_outage ;;
        all)
            setup
            demo_single_node_failure
            demo_rolling_restart  
            demo_cluster_outage
            cleanup
            ;;
        cleanup) cleanup ;;
        *) 
            echo "Usage: $0 [setup|demo1|demo2|demo3|all|cleanup]"
            exit 1
            ;;
    esac
else
    main_menu
fi