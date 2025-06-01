#!/bin/bash

# Container Runtime Detection Helper
# This script provides functions to detect and use Docker or Podman

# Detect container runtime (Docker or Podman)
detect_container_runtime() {
    if command -v docker &> /dev/null; then
        if docker version &> /dev/null; then
            echo "docker"
            return
        fi
    fi
    
    if command -v podman &> /dev/null; then
        if podman version &> /dev/null; then
            echo "podman"
            return
        fi
    fi
    
    echo "error"
}

# Set container runtime and compose command
setup_container_runtime() {
    CONTAINER_RUNTIME=$(detect_container_runtime)
    
    if [ "$CONTAINER_RUNTIME" = "error" ]; then
        echo "Error: Neither Docker nor Podman is available or running" >&2
        echo "Please install Docker Desktop or Podman and ensure it's running" >&2
        return 1
    fi
    
    # Set compose command based on runtime
    if [ "$CONTAINER_RUNTIME" = "podman" ]; then
        COMPOSE_CMD="podman-compose"
        # Check if podman-compose is installed
        if ! command -v podman-compose &> /dev/null; then
            echo "Warning: podman-compose is not installed" >&2
            echo "You can install it with: pip install podman-compose" >&2
            echo "Or use: brew install podman-compose (on macOS)" >&2
        fi
    else
        COMPOSE_CMD="docker-compose"
        # Check if docker-compose is installed
        if ! command -v docker-compose &> /dev/null; then
            # Try docker compose (newer integrated version)
            if docker compose version &> /dev/null; then
                COMPOSE_CMD="docker compose"
            else
                echo "Warning: docker-compose is not installed" >&2
                echo "You can install it or use Docker Desktop which includes it" >&2
            fi
        fi
    fi
    
    # Export for use in scripts
    export CONTAINER_RUNTIME
    export COMPOSE_CMD
    
    # Print detected runtime
    echo "Container runtime: $CONTAINER_RUNTIME" >&2
    if [ -n "$COMPOSE_CMD" ]; then
        echo "Compose command: $COMPOSE_CMD" >&2
    fi
    
    return 0
}

# Helper function to run container commands with proper error handling
run_container_cmd() {
    local cmd="$1"
    shift
    
    case "$cmd" in
        "runtime")
            $CONTAINER_RUNTIME "$@"
            ;;
        "compose")
            $COMPOSE_CMD "$@"
            ;;
        *)
            echo "Error: Unknown command type: $cmd" >&2
            return 1
            ;;
    esac
}

# Check if script is being sourced or run directly
if [[ "${BASH_SOURCE[0]}" == "${0}" ]]; then
    # Script is being run directly, just detect and report
    setup_container_runtime
fi