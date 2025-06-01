# Resilient Client Demonstration

The resilient client has been successfully implemented with the following features:

## Key Features

1. **Automatic Host Monitoring**
   - Monitors host states every 5 seconds
   - Detects when hosts go down or come back up
   - Tracks state transitions for visibility

2. **Connection Pool Management**
   - Refreshes connection pools every 60 seconds
   - Ensures connections don't become stale
   - Handles connection failures gracefully

3. **Retry Logic**
   - Retries failed queries up to 3 times
   - Uses exponential backoff (1s, 2s, 4s)
   - Only retries for retryable exceptions

4. **Speculative Execution**
   - Enabled for idempotent queries
   - Improves latency during partial cluster failures
   - Sends queries to multiple nodes simultaneously

5. **Enhanced Logging**
   - Logs all host state changes
   - Reports connection refresh events
   - Provides detailed metrics

## Usage

To use the resilient client, add the `--resilient-client` flag:

```bash
dotnet run --project src/CassandraProbe.Cli -- \
    --contact-points "localhost:9042" \
    --resilient-client \
    --test-cql "SELECT now() FROM system.local" \
    -i 5 \
    -d 30 \
    --log-level Information
```

## Implementation Details

The resilient client is implemented in:
- `/src/CassandraProbe.Services/Resilience/ResilientCassandraClient.cs`
- `/src/CassandraProbe.Services/Resilience/ResilienceDemo.cs`

Key components:
- Host state monitoring timer
- Connection refresh timer
- Retry policy with exponential backoff
- Metrics tracking for observability

## Metrics

The client tracks:
- Total queries executed
- Failed queries
- Success rate
- Host state transitions
- Per-host metrics (up/down status, consecutive failures)

## Production Usage

To use this in your application, copy the `ResilientCassandraClient` class and adapt it to your needs. Key considerations:

1. Adjust monitoring intervals based on your requirements
2. Configure retry attempts and timeouts appropriately
3. Consider implementing circuit breaker for additional protection
4. Monitor the metrics to understand cluster health

## Testing

Unit tests are provided in:
- `/tests/CassandraProbe.Services.Tests/Resilience/ResilientCassandraClientTests.cs`

Integration tests (requires Docker) in:
- `/tests/CassandraProbe.IntegrationTests/ResilientClientIntegrationTests.cs`

## Docker Demo Scripts

Two demonstration scripts are provided:

1. `scripts/demo-resilient-client.sh` - Interactive demo with 3-node cluster
2. `scripts/test-resilient-client.sh` - Automated test scenarios

These scripts demonstrate:
- Single node failure handling
- Rolling restart recovery
- Complete cluster outage recovery