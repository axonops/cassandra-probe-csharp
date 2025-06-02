#!/bin/bash

# Stable Demo Script for Resilient Client
# This uses docker-compose for better reliability

set -e

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
MAGENTA='\033[0;35m'
NC='\033[0m'

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
COMPOSE_FILE="$PROJECT_ROOT/docker-compose.demo.yml"

# Detect container runtime
if command -v docker >/dev/null 2>&1; then
    CONTAINER_RUNTIME="docker"
    COMPOSE_CMD="docker compose"
    # Try docker-compose if docker compose doesn't work
    if ! docker compose version >/dev/null 2>&1; then
        if command -v docker-compose >/dev/null 2>&1; then
            COMPOSE_CMD="docker-compose"
        else
            echo -e "${RED}Error: Neither 'docker compose' nor 'docker-compose' found${NC}"
            exit 1
        fi
    fi
elif command -v podman >/dev/null 2>&1; then
    CONTAINER_RUNTIME="podman"
    COMPOSE_CMD="podman-compose"
    if ! command -v podman-compose >/dev/null 2>&1; then
        echo -e "${RED}Error: podman-compose not found. Install with: pip install podman-compose${NC}"
        exit 1
    fi
else
    echo -e "${RED}Error: Neither Docker nor Podman found${NC}"
    exit 1
fi

echo -e "${GREEN}Using: $CONTAINER_RUNTIME with $COMPOSE_CMD${NC}"

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
    cd "$PROJECT_ROOT"
    $COMPOSE_CMD -f "$COMPOSE_FILE" down -v >/dev/null 2>&1 || true
    success "Cleanup complete"
}

# Start Cassandra
start_cassandra() {
    log "Starting Cassandra..."
    
    cd "$PROJECT_ROOT"
    
    # Stop any existing instance
    $COMPOSE_CMD -f "$COMPOSE_FILE" down -v >/dev/null 2>&1 || true
    
    # Start fresh
    if ! $COMPOSE_CMD -f "$COMPOSE_FILE" up -d; then
        error "Failed to start Cassandra"
        return 1
    fi
    
    # Wait for health check
    log "Waiting for Cassandra to be healthy (may take 60-90 seconds)..."
    local attempts=0
    local max_attempts=45  # 45 * 2 = 90 seconds
    
    while [ $attempts -lt $max_attempts ]; do
        if $COMPOSE_CMD -f "$COMPOSE_FILE" ps | grep -q "healthy"; then
            success "Cassandra is healthy!"
            break
        fi
        
        # Check if container is still running
        if ! $COMPOSE_CMD -f "$COMPOSE_FILE" ps | grep -q "cassandra-demo"; then
            error "Cassandra container stopped unexpectedly"
            $COMPOSE_CMD -f "$COMPOSE_FILE" logs --tail 50
            return 1
        fi
        
        echo -n "."
        sleep 2
        ((attempts++))
    done
    echo ""
    
    if [ $attempts -eq $max_attempts ]; then
        error "Cassandra failed to become healthy"
        $COMPOSE_CMD -f "$COMPOSE_FILE" logs --tail 50
        return 1
    fi
    
    # Create test schema
    log "Creating test schema..."
    $CONTAINER_RUNTIME exec cassandra-demo cqlsh -e "
        CREATE KEYSPACE IF NOT EXISTS demo 
        WITH replication = {'class': 'SimpleStrategy', 'replication_factor': 1};
        
        CREATE TABLE IF NOT EXISTS demo.test (
            id UUID PRIMARY KEY,
            timestamp timestamp,
            client text,
            message text
        );" || {
        error "Failed to create schema"
        return 1
    }
    
    success "Cassandra is ready on localhost:19042"
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
        --contact-points "localhost:19042" \
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
    $CONTAINER_RUNTIME exec cassandra-demo cqlsh -e "
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
    
    # Offer to keep running
    read -p "Keep Cassandra running for manual testing? (y/n) " -n 1 -r
    echo ""
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        cleanup
    else
        echo ""
        echo "Cassandra is running on localhost:19042"
        echo "To connect: $CONTAINER_RUNTIME exec -it cassandra-demo cqlsh"
        echo "To stop: $COMPOSE_CMD -f $COMPOSE_FILE down"
    fi
}

# Handle cleanup on exit
trap cleanup EXIT INT TERM

# Run main
main "$@"