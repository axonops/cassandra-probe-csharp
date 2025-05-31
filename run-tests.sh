#!/bin/bash

# Cassandra Probe C# - Test Runner Script
# This script runs all unit tests and generates a coverage report

echo "=== Cassandra Probe C# Test Runner ==="
echo

# Check if dotnet is installed
if ! command -v dotnet &> /dev/null; then
    echo "Error: .NET SDK is not installed. Please install .NET 6.0 or later."
    exit 1
fi

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Function to run tests for a project
run_tests() {
    local project_name=$1
    local project_path="tests/${project_name}/${project_name}.csproj"
    
    if [ -f "$project_path" ]; then
        echo -e "${YELLOW}Running tests for ${project_name}...${NC}"
        dotnet test "$project_path" \
            --configuration Release \
            --no-build \
            --verbosity minimal \
            --logger "console;verbosity=minimal" \
            --collect:"XPlat Code Coverage" \
            --results-directory "./TestResults/${project_name}"
        
        if [ $? -eq 0 ]; then
            echo -e "${GREEN}✓ ${project_name} tests passed${NC}"
        else
            echo -e "${RED}✗ ${project_name} tests failed${NC}"
            return 1
        fi
    else
        echo -e "${YELLOW}Warning: Project ${project_name} not found at ${project_path}${NC}"
    fi
    echo
}

# Build all projects first
echo "Building solution..."
dotnet build --configuration Release

if [ $? -ne 0 ]; then
    echo -e "${RED}Build failed. Please fix build errors before running tests.${NC}"
    exit 1
fi

echo
echo "Running unit tests..."
echo

# Run tests for each test project
test_projects=(
    "CassandraProbe.Core.Tests"
    "CassandraProbe.Services.Tests"
    "CassandraProbe.Actions.Tests"
    "CassandraProbe.Scheduling.Tests"
    "CassandraProbe.Cli.Tests"
)

failed_tests=0

for project in "${test_projects[@]}"; do
    run_tests "$project"
    if [ $? -ne 0 ]; then
        ((failed_tests++))
    fi
done

# Summary
echo "=== Test Summary ==="
if [ $failed_tests -eq 0 ]; then
    echo -e "${GREEN}All tests passed!${NC}"
else
    echo -e "${RED}${failed_tests} test project(s) failed.${NC}"
fi

# Generate coverage report if coverlet is available
if command -v reportgenerator &> /dev/null; then
    echo
    echo "Generating coverage report..."
    reportgenerator \
        -reports:"TestResults/**/coverage.cobertura.xml" \
        -targetdir:"TestResults/CoverageReport" \
        -reporttypes:Html
    
    echo -e "${GREEN}Coverage report generated at: TestResults/CoverageReport/index.html${NC}"
else
    echo
    echo -e "${YELLOW}Note: Install reportgenerator to generate HTML coverage reports:${NC}"
    echo "  dotnet tool install -g dotnet-reportgenerator-globaltool"
fi

# Calculate overall coverage (simplified)
echo
echo "=== Coverage Summary ==="
if [ -f "TestResults/CassandraProbe.Core.Tests/*/coverage.cobertura.xml" ]; then
    # This is a simplified coverage calculation
    # In a real scenario, you'd parse the XML files properly
    echo "Coverage calculation requires parsing XML files."
    echo "Run 'dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover' for detailed coverage."
else
    echo "No coverage data found. Ensure coverlet.collector is installed."
fi

exit $failed_tests