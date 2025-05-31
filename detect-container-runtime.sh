#!/bin/bash
# Container runtime detection script for Cassandra Probe
# Detects and returns available container runtime (docker or podman)

# Function to check if a command exists
command_exists() {
    command -v "$1" &> /dev/null
}

# Function to check if Docker is running
docker_is_running() {
    docker info &> /dev/null
}

# Function to check if Podman is running
podman_is_running() {
    podman info &> /dev/null
}

# Detect container runtime
detect_runtime() {
    # Check for Docker first
    if command_exists docker; then
        if docker_is_running; then
            echo "docker"
            return 0
        fi
    fi
    
    # Check for Podman
    if command_exists podman; then
        if podman_is_running; then
            echo "podman"
            return 0
        fi
    fi
    
    # No runtime found
    return 1
}

# If script is run directly (not sourced)
if [[ "${BASH_SOURCE[0]}" == "${0}" ]]; then
    RUNTIME=$(detect_runtime)
    if [ $? -eq 0 ]; then
        echo "Detected container runtime: $RUNTIME"
        exit 0
    else
        echo "No container runtime found. Please install Docker or Podman." >&2
        exit 1
    fi
fi