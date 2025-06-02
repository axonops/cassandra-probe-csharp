#!/bin/bash

# Container Helper Functions
# Shared functions for handling existing containers

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

# Function to check for existing Cassandra containers
check_existing_containers() {
    local runtime=$1
    local containers=()
    
    # Find all Cassandra-related containers
    while IFS= read -r line; do
        if [[ -n "$line" ]]; then
            containers+=("$line")
        fi
    done < <($runtime ps -a --format "table {{.Names}}" | grep -E "(cassandra|probe)" | grep -v "NAMES" || true)
    
    if [ ${#containers[@]} -gt 0 ]; then
        echo -e "${YELLOW}Found existing Cassandra-related containers:${NC}"
        for container in "${containers[@]}"; do
            local status=$($runtime ps -a --format "table {{.Names}} {{.Status}}" | grep "^$container" | awk '{$1=""; print $0}' | xargs)
            echo "  - $container ($status)"
        done
        return 0
    else
        return 1
    fi
}

# Function to prompt for container cleanup
prompt_container_cleanup() {
    local runtime=$1
    
    echo ""
    echo -e "${YELLOW}What would you like to do?${NC}"
    echo "1) Stop and remove these containers (recommended)"
    echo "2) Keep them and exit"
    echo ""
    read -p "Your choice (1 or 2): " choice
    
    case $choice in
        1)
            echo -e "${BLUE}Stopping and removing containers...${NC}"
            while IFS= read -r container; do
                if [[ -n "$container" ]]; then
                    echo -n "  Stopping $container..."
                    $runtime stop "$container" >/dev/null 2>&1 || true
                    echo " done"
                    echo -n "  Removing $container..."
                    $runtime rm -f "$container" >/dev/null 2>&1 || true
                    echo " done"
                fi
            done < <($runtime ps -a --format "{{.Names}}" | grep -E "(cassandra|probe)" || true)
            echo -e "${GREEN}Containers cleaned up successfully!${NC}"
            return 0
            ;;
        2)
            echo -e "${RED}Exiting without changes.${NC}"
            echo "Please manually stop containers before running this script."
            return 1
            ;;
        *)
            echo -e "${RED}Invalid choice. Exiting.${NC}"
            return 1
            ;;
    esac
}

# Function to check if specific ports are in use
check_ports_in_use() {
    local ports=("$@")
    local in_use=()
    
    for port in "${ports[@]}"; do
        if lsof -i :$port >/dev/null 2>&1; then
            in_use+=($port)
        fi
    done
    
    if [ ${#in_use[@]} -gt 0 ]; then
        echo -e "${YELLOW}The following ports are in use: ${in_use[*]}${NC}"
        return 0
    else
        return 1
    fi
}

# Function to cleanup specific container
cleanup_container_if_exists() {
    local runtime=$1
    local container_name=$2
    
    if $runtime ps -a --format "{{.Names}}" | grep -q "^${container_name}$"; then
        echo -e "${YELLOW}Container '$container_name' already exists.${NC}"
        read -p "Stop and remove it? (y/n): " -n 1 -r
        echo
        if [[ $REPLY =~ ^[Yy]$ ]]; then
            $runtime stop "$container_name" >/dev/null 2>&1 || true
            $runtime rm -f "$container_name" >/dev/null 2>&1 || true
            echo -e "${GREEN}Container removed.${NC}"
            return 0
        else
            echo -e "${RED}Cannot proceed with existing container.${NC}"
            return 1
        fi
    fi
    return 0
}