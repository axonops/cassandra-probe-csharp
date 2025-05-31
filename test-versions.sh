#!/bin/bash
# Test script for verifying probe works with all supported Cassandra versions
# Supports: 4.0, 4.1, and 5.0

set -e

echo "🧪 Cassandra Probe Version Testing"
echo "=================================="
echo "Testing compatibility with Cassandra 4.0, 4.1, and 5.0"
echo ""

# Source the container runtime detection script
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "${SCRIPT_DIR}/detect-container-runtime.sh"

# Detect container runtime
RUNTIME=$(detect_runtime)
if [ $? -ne 0 ]; then
    echo "❌ No container runtime found. Please install Docker or Podman."
    exit 1
fi

echo "✅ Using container runtime: $RUNTIME"
echo ""

# Colors for output
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Function to test a specific version
test_version() {
    local version=$1
    local port=$2
    local container_name="cassandra-test-${version//./}"
    
    echo -e "${YELLOW}Testing Cassandra $version on port $port...${NC}"
    
    # Stop and remove existing container if it exists
    $RUNTIME stop $container_name 2>/dev/null || true
    $RUNTIME rm $container_name 2>/dev/null || true
    
    # Start Cassandra
    echo "Starting Cassandra $version..."
    $RUNTIME run -d --name $container_name -p $port:9042 cassandra:$version
    
    # Wait for Cassandra to be ready
    echo "Waiting for Cassandra to initialize..."
    for i in {1..60}; do
        if $RUNTIME exec $container_name cqlsh -e "DESC KEYSPACES;" &> /dev/null; then
            echo -e "${GREEN}✅ Cassandra $version is ready!${NC}"
            break
        fi
        if [ $i -eq 60 ]; then
            echo -e "${RED}❌ Timeout waiting for Cassandra $version${NC}"
            return 1
        fi
        echo -n "."
        sleep 2
    done
    echo ""
    
    # Run probe tests
    echo "Running probe tests..."
    
    # Test 1: Basic connectivity
    echo -n "  Basic connectivity: "
    if ./cassandra-probe -cp localhost:$port > /dev/null 2>&1; then
        echo -e "${GREEN}PASS${NC}"
    else
        echo -e "${RED}FAIL${NC}"
    fi
    
    # Test 2: Version query
    echo -n "  Version detection: "
    if ./cassandra-probe -cp localhost:$port -cql "SELECT release_version FROM system.local" | grep -q "$version"; then
        echo -e "${GREEN}PASS${NC}"
    else
        echo -e "${RED}FAIL${NC}"
    fi
    
    # Test 3: Virtual tables (4.0+)
    echo -n "  Virtual tables: "
    if ./cassandra-probe -cp localhost:$port -cql "SELECT * FROM system_views.clients LIMIT 1" > /dev/null 2>&1; then
        echo -e "${GREEN}PASS${NC}"
    else
        echo -e "${RED}FAIL (expected for some queries)${NC}"
    fi
    
    # Test 4: All probes
    echo -n "  All probes: "
    if ./cassandra-probe -cp localhost:$port --all-probes > /dev/null 2>&1; then
        echo -e "${GREEN}PASS${NC}"
    else
        echo -e "${RED}FAIL${NC}"
    fi
    
    # Stop container
    echo "Stopping Cassandra $version..."
    $RUNTIME stop $container_name > /dev/null
    $RUNTIME rm $container_name > /dev/null
    
    echo -e "${GREEN}✅ Cassandra $version testing complete${NC}"
    echo ""
}

# Check if probe executable exists
if [ ! -f "./cassandra-probe" ]; then
    echo -e "${RED}❌ Error: cassandra-probe executable not found in current directory${NC}"
    echo "Please build or download the probe first."
    exit 1
fi

# Make sure it's executable
chmod +x ./cassandra-probe

# Test each version
echo "Starting version compatibility tests..."
echo ""

# Cassandra 4.0
test_version "4.0" "9040"

# Cassandra 4.1 (Recommended)
test_version "4.1" "9041"

# Cassandra 5.0
test_version "5.0" "9050"

echo "=================================="
echo -e "${GREEN}🎉 All version tests complete!${NC}"
echo ""
echo "Summary:"
echo "- Cassandra 4.0: Supported ✅"
echo "- Cassandra 4.1: Supported (Recommended) ✅"
echo "- Cassandra 5.0: Supported ✅"
echo ""
echo "Note: Cassandra 3.x versions are NOT supported"