#!/bin/bash

# Cleanup script to remove all Cassandra-related containers
# Useful for starting fresh

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

# Detect container runtime
if command -v docker >/dev/null 2>&1; then
    RUNTIME="docker"
elif command -v podman >/dev/null 2>&1; then
    RUNTIME="podman"
else
    echo -e "${RED}Error: Neither Docker nor Podman found${NC}"
    exit 1
fi

echo -e "${BLUE}Cassandra Container Cleanup${NC}"
echo -e "${BLUE}==========================${NC}"
echo ""

# Find all Cassandra-related containers
containers=()
while IFS= read -r line; do
    if [[ -n "$line" ]]; then
        containers+=("$line")
    fi
done < <($RUNTIME ps -a --format "table {{.Names}} {{.Status}}" | grep -E "(cassandra|probe)" | grep -v "NAMES" || true)

if [ ${#containers[@]} -eq 0 ]; then
    echo -e "${GREEN}No Cassandra-related containers found.${NC}"
    exit 0
fi

# Show what we found
echo -e "${YELLOW}Found the following containers:${NC}"
printf '%s\n' "${containers[@]}"
echo ""

# Confirm action
read -p "Remove all these containers? (y/n): " -n 1 -r
echo
if [[ ! $REPLY =~ ^[Yy]$ ]]; then
    echo -e "${YELLOW}Cleanup cancelled.${NC}"
    exit 0
fi

# Clean up containers
echo ""
echo -e "${BLUE}Cleaning up containers...${NC}"
while IFS= read -r container; do
    name=$(echo "$container" | awk '{print $1}')
    if [[ -n "$name" ]]; then
        echo -n "  Stopping $name..."
        $RUNTIME stop "$name" >/dev/null 2>&1 || true
        echo " done"
        echo -n "  Removing $name..."
        $RUNTIME rm -f "$name" >/dev/null 2>&1 || true
        echo " done"
    fi
done < <($RUNTIME ps -a --format "{{.Names}}" | grep -E "(cassandra|probe)" || true)

# Clean up networks
echo ""
echo -e "${BLUE}Cleaning up networks...${NC}"
$RUNTIME network prune -f >/dev/null 2>&1 || true

echo ""
echo -e "${GREEN}Cleanup complete!${NC}"
echo ""
echo "You can now run any demo script without conflicts."