using CassandraProbe.Core.Configuration;
using CassandraProbe.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Quartz;

namespace CassandraProbe.Scheduling;

[DisallowConcurrentExecution]
public class ProbeJob : IJob
{
    private readonly IProbeOrchestrator _orchestrator;
    private readonly IConnectionMonitor _connectionMonitor;
    private readonly ILogger<ProbeJob> _logger;

    public ProbeJob(
        IProbeOrchestrator orchestrator,
        IConnectionMonitor connectionMonitor,
        ILogger<ProbeJob> logger)
    {
        _orchestrator = orchestrator;
        _connectionMonitor = connectionMonitor;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var jobId = context.JobDetail.Key.Name;
        var startTime = DateTime.UtcNow;
        
        _logger.LogInformation("Starting scheduled probe job {JobId}", jobId);

        try
        {
            // Get configuration from job data map
            var config = context.JobDetail.JobDataMap.Get("ProbeConfiguration") as ProbeConfiguration;
            
            if (config == null)
            {
                _logger.LogError("No ProbeConfiguration found in job data map");
                return;
            }

            // Execute probes (uses existing session)
            var session = await _orchestrator.ExecuteProbesAsync(config);
            
            // Log connection pool status
            var poolStatus = _connectionMonitor.GetPoolStatus();
            _logger.LogInformation(
                "Connection pool status - Total: {Total}, Active: {Active}, Failed: {Failed}, Reconnecting: {Reconnecting}",
                poolStatus.TotalConnections,
                poolStatus.ActiveConnections,
                poolStatus.FailedHosts,
                poolStatus.ReconnectingHosts.Count);

            // Log reconnection events since last run
            var reconnectionEvents = _connectionMonitor.GetReconnectionHistory()
                .Where(e => e.Timestamp >= (context.PreviousFireTimeUtc?.DateTime ?? startTime.AddHours(-1)))
                .ToList();

            if (reconnectionEvents.Any())
            {
                _logger.LogInformation("Reconnection events since last run:");
                foreach (var evt in reconnectionEvents)
                {
                    _logger.LogInformation("  [{Timestamp}] {Host} - {EventType}: {Message}",
                        evt.Timestamp, evt.Host, evt.EventType, evt.Message);
                }
            }

            var duration = DateTime.UtcNow - startTime;
            _logger.LogInformation(
                "Scheduled probe job {JobId} completed in {Duration:F2}s. Next run: {NextFireTime}",
                jobId, duration.TotalSeconds, (context.NextFireTimeUtc?.DateTime) ?? DateTime.MaxValue);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in scheduled probe job {JobId}", jobId);
            throw; // Let Quartz handle the error
        }
    }
}