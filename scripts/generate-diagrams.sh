#!/bin/bash

# Script to generate sequence diagrams from PlantUML files
# This can be run locally or in CI/CD pipelines

set -e

# Colors
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m'

# Script directory
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
DOCS_DIR="$PROJECT_ROOT/docs"
OUTPUT_DIR="$DOCS_DIR/images"

# Function to print colored output
log_info() { echo -e "${GREEN}[INFO]${NC} $1"; }
log_error() { echo -e "${RED}[ERROR]${NC} $1"; }
log_warn() { echo -e "${YELLOW}[WARN]${NC} $1"; }

# Function to check if a command exists
command_exists() {
    command -v "$1" >/dev/null 2>&1
}

# Function to download PlantUML if needed
download_plantuml() {
    local plantuml_jar="$PROJECT_ROOT/tools/plantuml.jar"
    local plantuml_url="https://github.com/plantuml/plantuml/releases/download/v1.2024.0/plantuml-1.2024.0.jar"
    
    if [ ! -f "$plantuml_jar" ]; then
        log_info "Downloading PlantUML..."
        mkdir -p "$PROJECT_ROOT/tools"
        curl -L -o "$plantuml_jar" "$plantuml_url" || {
            log_error "Failed to download PlantUML"
            return 1
        }
        log_info "PlantUML downloaded successfully"
    fi
    
    echo "$plantuml_jar"
}

# Function to generate diagrams using Docker
generate_with_docker() {
    log_info "Using Docker to generate diagrams..."
    
    # Check if Docker is available
    if ! command_exists docker; then
        log_error "Docker is not installed"
        return 1
    fi
    
    # Create output directory
    mkdir -p "$OUTPUT_DIR"
    
    # Run PlantUML in Docker
    docker run --rm \
        -v "$DOCS_DIR:/data" \
        plantuml/plantuml \
        -tpng -tsvg "/data/*.puml" \
        -o "/data/images" || {
        log_error "Failed to generate diagrams with Docker"
        return 1
    }
    
    log_info "Diagrams generated successfully with Docker"
    return 0
}

# Function to generate diagrams using local Java
generate_with_java() {
    log_info "Using local Java to generate diagrams..."
    
    # Check if Java is available
    if ! command_exists java; then
        log_error "Java is not installed"
        return 1
    fi
    
    # Get or download PlantUML jar
    local plantuml_jar=$(download_plantuml)
    if [ $? -ne 0 ]; then
        return 1
    fi
    
    # Create output directory
    mkdir -p "$OUTPUT_DIR"
    
    # Generate PNG and SVG for all PlantUML files
    find "$DOCS_DIR" -name "*.puml" -type f | while read -r puml_file; do
        local basename=$(basename "$puml_file" .puml)
        log_info "Processing $basename..."
        
        # Generate PNG
        java -jar "$plantuml_jar" -tpng "$puml_file" -o "$OUTPUT_DIR" || {
            log_error "Failed to generate PNG for $basename"
            continue
        }
        
        # Generate SVG
        java -jar "$plantuml_jar" -tsvg "$puml_file" -o "$OUTPUT_DIR" || {
            log_error "Failed to generate SVG for $basename"
            continue
        }
        
        log_info "Generated $basename.png and $basename.svg"
    done
    
    log_info "Diagrams generated successfully with Java"
    return 0
}

# Function to generate diagrams using npm/mermaid (as fallback)
generate_with_mermaid() {
    log_info "Converting to Mermaid format as fallback..."
    
    # This would require converting PlantUML to Mermaid syntax
    # For now, we'll skip this as PlantUML is more suitable for complex diagrams
    log_warn "Mermaid conversion not implemented - PlantUML is recommended for complex sequence diagrams"
    return 1
}

# Main execution
main() {
    log_info "Generating sequence diagrams..."
    log_info "Project root: $PROJECT_ROOT"
    
    # Check if there are any PlantUML files
    if ! find "$DOCS_DIR" -name "*.puml" -type f | grep -q .; then
        log_warn "No PlantUML files found in $DOCS_DIR"
        exit 0
    fi
    
    # Try different methods in order of preference
    if generate_with_docker; then
        :  # Success
    elif generate_with_java; then
        :  # Success
    else
        log_error "Failed to generate diagrams"
        log_info "Please install either:"
        log_info "  - Docker: https://docs.docker.com/get-docker/"
        log_info "  - Java: https://www.java.com/en/download/"
        exit 1
    fi
    
    # List generated files
    log_info "Generated files:"
    find "$OUTPUT_DIR" -name "*.png" -o -name "*.svg" | sort | while read -r file; do
        echo "  - ${file#$PROJECT_ROOT/}"
    done
    
    log_info "Done!"
}

# Run main function
main "$@"