using System.Diagnostics;
using System.Text.RegularExpressions;
using Cassandra;
using CassandraProbe.Core.Exceptions;
using CassandraProbe.Core.Interfaces;
using CassandraProbe.Core.Models;
using Microsoft.Extensions.Logging;

namespace CassandraProbe.Actions;

public class CqlQueryProbe : IProbeAction
{
    private readonly ISessionManager _sessionManager;
    private readonly ILogger<CqlQueryProbe> _logger;
    private static readonly Regex QueryTypeRegex = new(@"^\s*(SELECT|INSERT|UPDATE)\s+", 
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public CqlQueryProbe(ISessionManager sessionManager, ILogger<CqlQueryProbe> logger)
    {
        _sessionManager = sessionManager;
        _logger = logger;
    }

    public string Name => "CQL Query Probe";
    public ProbeType Type => ProbeType.CqlQuery;

    public async Task<ProbeResult> ExecuteAsync(HostProbe host, ProbeContext context)
    {
        var query = context.Configuration.Query.TestCql;
        if (string.IsNullOrWhiteSpace(query))
        {
            return ProbeResult.CreateFailure(host, Type, "No test query specified", TimeSpan.Zero);
        }

        // Validate query type
        if (!IsValidQueryType(query))
        {
            _logger.LogDebug("Query validation failed for: {Query}", query);
            return ProbeResult.CreateFailure(host, Type, 
                $"Invalid query type. Only SELECT, INSERT, and UPDATE queries are allowed. Query: '{query}'", TimeSpan.Zero);
        }
        
        _logger.LogDebug("Executing CQL query probe for {Host}: {Query}", host.Address, query);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var session = await _sessionManager.GetSessionAsync();
            
            // Create statement with configured consistency level
            var statement = new SimpleStatement(query);
            statement.SetConsistencyLevel(ParseConsistencyLevel(context.Configuration.Query.ConsistencyLevel));
            
            // Set query timeout
            var queryTimeout = context.Configuration.Query.QueryTimeoutSeconds * 1000;
            statement.SetReadTimeoutMillis(queryTimeout);
            
            // Enable tracing if requested
            if (context.Configuration.Query.EnableTracing)
            {
                statement.EnableTracing();
            }

            // Execute query
            var result = await session.ExecuteAsync(statement);
            
            stopwatch.Stop();
            
            var probeResult = ProbeResult.CreateSuccess(host, Type, stopwatch.Elapsed);
            
            // Add metadata
            probeResult.Metadata["RowCount"] = result.Count();
            probeResult.Metadata["ConsistencyLevel"] = context.Configuration.Query.ConsistencyLevel;
            
            // Add tracing information if available
            if (context.Configuration.Query.EnableTracing && result.Info.QueryTrace != null)
            {
                var trace = result.Info.QueryTrace;
                probeResult.Metadata["TraceId"] = trace.TraceId;
                probeResult.Metadata["CoordinatorAddress"] = trace.Coordinator?.ToString() ?? "Unknown";
                probeResult.Metadata["TraceDurationMicros"] = trace.DurationMicros;
                
                // Log trace events
                if (trace.Events != null && trace.Events.Any())
                {
                    _logger.LogDebug("Query trace for {Host}:", host.Address);
                    foreach (var evt in trace.Events.OrderBy(e => e.Timestamp))
                    {
                        _logger.LogDebug("  [{Timestamp}] {Description} on {Source} (thread {Thread})",
                            evt.Timestamp, evt.Description, evt.Source, evt.ThreadName);
                    }
                }
            }
            
            return probeResult;
        }
        catch (NoHostAvailableException ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "No hosts available for query execution");
            return ProbeResult.CreateFailure(host, Type, 
                "No hosts available for query execution", stopwatch.Elapsed);
        }
        catch (Cassandra.AuthenticationException ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Authentication failed during query execution");
            return ProbeResult.CreateFailure(host, Type, 
                $"Authentication failed: {ex.Message}", stopwatch.Elapsed);
        }
        catch (UnauthorizedException ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Authorization failed during query execution");
            return ProbeResult.CreateFailure(host, Type, 
                $"Authorization failed: {ex.Message}", stopwatch.Elapsed);
        }
        catch (SyntaxError ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Query syntax error");
            return ProbeResult.CreateFailure(host, Type, 
                $"Query syntax error: {ex.Message}", stopwatch.Elapsed);
        }
        catch (OperationTimedOutException ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Query timeout");
            return ProbeResult.CreateFailure(host, Type, 
                $"Query timeout: {ex.Message}", stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Unexpected error during query execution");
            return ProbeResult.CreateFailure(host, Type, 
                $"Query error: {ex.Message}", stopwatch.Elapsed);
        }
    }

    private bool IsValidQueryType(string query)
    {
        return QueryTypeRegex.IsMatch(query);
    }

    private ConsistencyLevel ParseConsistencyLevel(string level)
    {
        return level.ToUpperInvariant() switch
        {
            "ANY" => ConsistencyLevel.Any,
            "ONE" => ConsistencyLevel.One,
            "TWO" => ConsistencyLevel.Two,
            "THREE" => ConsistencyLevel.Three,
            "QUORUM" => ConsistencyLevel.Quorum,
            "ALL" => ConsistencyLevel.All,
            "LOCAL_QUORUM" => ConsistencyLevel.LocalQuorum,
            "EACH_QUORUM" => ConsistencyLevel.EachQuorum,
            "LOCAL_ONE" => ConsistencyLevel.LocalOne,
            _ => ConsistencyLevel.One
        };
    }
}