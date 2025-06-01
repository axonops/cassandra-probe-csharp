#!/bin/bash

# Simple Resilient Client Test
# This script demonstrates the resilient client without Docker

set -e

# Colors
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

echo -e "${BLUE}Building the application...${NC}"
cd "$PROJECT_ROOT"
dotnet build -c Release

echo -e "${YELLOW}Testing resilient client against existing Cassandra cluster...${NC}"
echo ""

# Run with resilient client
echo -e "${GREEN}Starting resilient client demo...${NC}"
dotnet run --project src/CassandraProbe.Cli -- \
    --contact-points "localhost:9042" \
    --resilient-client \
    --test-cql "SELECT now() FROM system.local" \
    -i 5 \
    -d 30 \
    --log-level Information \
    --connection-events \
    --verbose

echo ""
echo -e "${GREEN}Resilient client demo completed!${NC}"
echo ""
echo "The resilient client provides:"
echo "- Automatic reconnection on connection failures"
echo "- Host state monitoring every 5 seconds"
echo "- Connection pool refresh every 60 seconds"
echo "- Retry logic with exponential backoff"
echo "- Speculative execution for idempotent queries"