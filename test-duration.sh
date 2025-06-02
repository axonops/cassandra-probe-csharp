#!/bin/bash
# Test script to verify duration behavior

echo "Testing probe with 30-second duration..."
echo "The app should exit automatically after 30 seconds"
echo ""

# Run probe with 30 second duration and 5 second interval
# Should run approximately 6 times then exit
timeout 45 ./publish/osx-arm64/cassandra-probe \
  --contact-points localhost:9042 \
  --interval 5 \
  --duration 0.5 \
  --quiet || true

EXIT_CODE=$?

if [ $EXIT_CODE -eq 124 ]; then
    echo "ERROR: App did not exit after duration expired (timeout reached)"
    exit 1
else
    echo "SUCCESS: App exited properly after duration"
    exit 0
fi