#!/bin/bash

# Test that resilient client respects CLI configuration

echo "Testing resilient client configuration passing..."

cd "$(dirname "$0")"

# Test with custom consistency level
echo ""
echo "1. Testing with consistency level ONE:"
dotnet run --project src/CassandraProbe.Cli -- \
    --contact-points "localhost:9042" \
    --resilient-client \
    --consistency-level ONE \
    --test-cql "SELECT now() FROM system.local" \
    -d 1 \
    --log-level Debug 2>&1 | grep -E "(consistency|ConsistencyLevel)" || echo "No consistency level output found"

# Test with custom timeouts
echo ""
echo "2. Testing with custom timeouts:"
dotnet run --project src/CassandraProbe.Cli -- \
    --contact-points "localhost:9042" \
    --resilient-client \
    --connection-timeout 10 \
    --request-timeout 15 \
    --test-cql "SELECT now() FROM system.local" \
    -d 1 \
    --log-level Debug 2>&1 | grep -E "(timeout|Timeout)" || echo "No timeout output found"

# Test with authentication
echo ""
echo "3. Testing with authentication:"
dotnet run --project src/CassandraProbe.Cli -- \
    --contact-points "localhost:9042" \
    --resilient-client \
    --username testuser \
    --password testpass \
    --test-cql "SELECT now() FROM system.local" \
    -d 1 \
    --log-level Debug 2>&1 | grep -E "(authentication|Authentication|credentials)" || echo "No auth output found"

echo ""
echo "Test complete. Check output above to verify configuration is being used."