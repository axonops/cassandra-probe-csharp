#!/bin/bash

# Minimal test script for resilient client
# Assumes you have Cassandra running somewhere

set -e

# Colors
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m'

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

echo "Resilient Client Test"
echo "===================="
echo ""

# Get contact point from user or use default
CONTACT_POINT="${1:-localhost:9042}"
echo -e "${YELLOW}Testing with Cassandra at: $CONTACT_POINT${NC}"
echo ""

# Build
echo "Building project..."
cd "$PROJECT_ROOT"
if ! dotnet build -c Release >/dev/null 2>&1; then
    echo -e "${RED}Build failed!${NC}"
    exit 1
fi

# Test connection
echo "Testing connection..."
if ! timeout 5 dotnet run --project src/CassandraProbe.Cli -- \
    --contact-points "$CONTACT_POINT" \
    --test-cql "SELECT now() FROM system.local" \
    -d 1 \
    --log-level Error >/dev/null 2>&1; then
    echo -e "${RED}Cannot connect to Cassandra at $CONTACT_POINT${NC}"
    echo ""
    echo "Please ensure Cassandra is running and accessible."
    echo "Usage: $0 [contact-point]"
    echo "Example: $0 192.168.1.100:9042"
    exit 1
fi

echo -e "${GREEN}Connection successful!${NC}"
echo ""

# Run comparison
echo "1. Standard Client (5 seconds):"
echo "------------------------------"
dotnet run --project src/CassandraProbe.Cli -- \
    --contact-points "$CONTACT_POINT" \
    --test-cql "SELECT release_version FROM system.local" \
    -i 2 \
    -d 5 \
    --log-level Information

echo ""
echo "2. Resilient Client Demo (5 seconds):"
echo "------------------------------------"
dotnet run --project src/CassandraProbe.Cli -- \
    --contact-points "$CONTACT_POINT" \
    --datacenter datacenter1 \
    --resilient-demo \
    --test-cql "SELECT release_version FROM system.local" \
    -i 2 \
    -d 5 \
    --log-level Information

echo ""
echo -e "${GREEN}Test complete!${NC}"
echo ""
echo "The resilient client should show:"
echo "• Host monitoring messages"
echo "• Connection pool refresh"
echo "• Enhanced logging"