using CassandraProbe.Core.Configuration;
using CassandraProbe.Core.Interfaces;
using CassandraProbe.Core.Models;
using Microsoft.Extensions.Logging;

namespace CassandraProbe.Services;

public class ProbeOrchestrator : IProbeOrchestrator
{
    private readonly IClusterDiscovery _clusterDiscovery;
    private readonly IEnumerable<IProbeAction> _probeActions;
    private readonly ILogger<ProbeOrchestrator> _logger;
    private readonly ISessionManager _sessionManager;

    public event EventHandler<ProbeCompletedEventArgs>? ProbeCompleted;

    public ProbeOrchestrator(
        IClusterDiscovery clusterDiscovery,
        IEnumerable<IProbeAction> probeActions,
        ILogger<ProbeOrchestrator> logger,
        ISessionManager sessionManager)
    {
        _clusterDiscovery = clusterDiscovery;
        _probeActions = probeActions;
        _logger = logger;
        _sessionManager = sessionManager;
    }

    public async Task<ProbeSession> ExecuteProbesAsync(ProbeConfiguration config)
    {
        var session = new ProbeSession();
        _logger.LogInformation("Starting probe session {SessionId}", session.Id);

        try
        {
            // Ensure connection is established (reuses existing session)
            await _sessionManager.GetSessionAsync();

            // Discover cluster topology
            session.Topology = await _clusterDiscovery.DiscoverAsync(config);

            // Get hosts to probe
            var hosts = await _clusterDiscovery.GetHostsAsync();

            // Select probes to execute
            var selectedProbes = SelectProbes(config);
            
            if (!selectedProbes.Any())
            {
                _logger.LogWarning("No probes selected for execution");
                session.EndTime = DateTime.UtcNow;
                return session;
            }

            _logger.LogInformation("Executing {ProbeCount} probe types on {HostCount} hosts",
                selectedProbes.Count(), hosts.Count());

            // Create probe context
            var context = new ProbeContext
            {
                Configuration = config,
                Logger = _logger,
                CancellationToken = CancellationToken.None
            };

            // Execute probes in parallel for each host
            var tasks = new List<Task<ProbeResult>>();
            
            foreach (var host in hosts)
            {
                foreach (var probe in selectedProbes)
                {
                    tasks.Add(ExecuteProbeAsync(probe, host, context));
                }
            }

            // Wait for all probes to complete
            var results = await Task.WhenAll(tasks);
            session.Results.AddRange(results);

            session.EndTime = DateTime.UtcNow;
            
            // Log summary
            var successCount = results.Count(r => r.Success);
            var failureCount = results.Length - successCount;
            
            _logger.LogInformation(
                "Probe session {SessionId} completed in {Duration:F2}s. Success: {Success}, Failures: {Failures}",
                session.Id, session.Duration.TotalSeconds, successCount, failureCount);

            return session;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during probe session {SessionId}", session.Id);
            session.EndTime = DateTime.UtcNow;
            throw;
        }
    }

    private async Task<ProbeResult> ExecuteProbeAsync(IProbeAction probe, HostProbe host, ProbeContext context)
    {
        try
        {
            _logger.LogDebug("Executing {ProbeName} on {Host}", probe.Name, host.Address);
            
            var result = await probe.ExecuteAsync(host, context);
            
            // Fire event
            ProbeCompleted?.Invoke(this, new ProbeCompletedEventArgs { Result = result });
            
            if (result.Success)
            {
                _logger.LogDebug("{ProbeName} succeeded on {Host} in {Duration:F2}ms",
                    probe.Name, host.Address, result.Duration.TotalMilliseconds);
            }
            else
            {
                _logger.LogWarning("{ProbeName} failed on {Host}: {Error}",
                    probe.Name, host.Address, result.ErrorMessage);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error executing {ProbeName} on {Host}", 
                probe.Name, host.Address);
            
            return ProbeResult.CreateFailure(host, probe.Type, 
                $"Unexpected error: {ex.Message}", TimeSpan.Zero);
        }
    }

    private IEnumerable<IProbeAction> SelectProbes(ProbeConfiguration config)
    {
        var selectedProbes = new List<IProbeAction>();

        if (config.ProbeSelection.ExecuteAllProbes)
        {
            selectedProbes.AddRange(_probeActions);
        }
        else
        {
            if (config.ProbeSelection.ProbeNativePort)
            {
                var nativeProbe = _probeActions.FirstOrDefault(p => p.Type == ProbeType.NativePort);
                if (nativeProbe != null) selectedProbes.Add(nativeProbe);
            }

            if (config.ProbeSelection.ProbeStoragePort)
            {
                var storageProbe = _probeActions.FirstOrDefault(p => p.Type == ProbeType.StoragePort);
                if (storageProbe != null) selectedProbes.Add(storageProbe);
            }

            if (config.ProbeSelection.ProbePing)
            {
                var pingProbe = _probeActions.FirstOrDefault(p => p.Type == ProbeType.Ping);
                if (pingProbe != null) selectedProbes.Add(pingProbe);
            }

            if (!string.IsNullOrEmpty(config.Query.TestCql))
            {
                var queryProbe = _probeActions.FirstOrDefault(p => p.Type == ProbeType.CqlQuery);
                if (queryProbe != null) selectedProbes.Add(queryProbe);
            }
        }

        // If no specific probes selected but we have contact points, default to socket probe
        if (!selectedProbes.Any() && config.ProbeSelection.ProbeNativePort)
        {
            var socketProbe = _probeActions.FirstOrDefault(p => p.Type == ProbeType.Socket);
            if (socketProbe != null) selectedProbes.Add(socketProbe);
        }

        return selectedProbes;
    }
}