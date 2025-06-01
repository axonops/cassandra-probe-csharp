#!/bin/bash

# Main demo runner script for Cassandra Probe Resilient Client
# Supports both Docker and Podman

set -e

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
MAGENTA='\033[0;35m'
NC='\033[0m'

# Detect container runtime
detect_container_runtime() {
    if command -v docker &> /dev/null; then
        echo "docker"
    elif command -v podman &> /dev/null; then
        echo "podman"
    else
        echo "none"
    fi
}

CONTAINER_RUNTIME=$(detect_container_runtime)

# Display header
echo -e "${MAGENTA}======================================${NC}"
echo -e "${MAGENTA}Cassandra Probe Resilient Client Demo${NC}"
echo -e "${MAGENTA}======================================${NC}"
echo ""

# Check runtime
if [ "$CONTAINER_RUNTIME" = "none" ]; then
    echo -e "${RED}Error: Neither Docker nor Podman is installed!${NC}"
    echo "Please install one of them to run the demos."
    echo ""
    echo "For Docker: https://docs.docker.com/get-docker/"
    echo "For Podman: https://podman.io/getting-started/installation"
    exit 1
fi

echo -e "${GREEN}Container runtime detected: ${CONTAINER_RUNTIME}${NC}"
echo ""

# Show available demos
echo "Available demos:"
echo "1) Simple resilient client test (requires existing Cassandra)"
echo "2) Full resilient client demo with 3-node cluster"
echo "3) Automated test suite with failure scenarios"
echo ""

# Check if scripts are executable
if [ ! -x "scripts/test-resilient-client-simple.sh" ]; then
    chmod +x scripts/*.sh
fi

# Menu
while true; do
    echo -n "Select demo (1-3) or 'q' to quit: "
    read -r choice
    
    case $choice in
        1)
            echo -e "${BLUE}Running simple resilient client test...${NC}"
            ./scripts/test-resilient-client-simple.sh
            ;;
        2)
            echo -e "${BLUE}Running full resilient client demo...${NC}"
            ./scripts/demo-resilient-client.sh
            ;;
        3)
            echo -e "${BLUE}Running automated test suite...${NC}"
            ./scripts/test-resilient-client.sh
            ;;
        q|Q)
            echo -e "${GREEN}Exiting...${NC}"
            exit 0
            ;;
        *)
            echo -e "${RED}Invalid choice. Please select 1-3 or 'q' to quit.${NC}"
            ;;
    esac
    
    echo ""
    echo "Press Enter to return to menu..."
    read -r
    clear
done