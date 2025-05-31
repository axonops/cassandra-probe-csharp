using System.Text.Json;
using System.Text.Json.Serialization;
using CassandraProbe.Core.Models;

namespace CassandraProbe.Logging.Formatters;

public class JsonFormatter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string FormatResult(ProbeResult result)
    {
        return JsonSerializer.Serialize(result, Options);
    }

    public static string FormatSession(ProbeSession session)
    {
        var report = new
        {
            sessionId = session.Id,
            startTime = session.StartTime,
            endTime = session.EndTime,
            duration = session.Duration,
            topology = session.Topology != null ? new
            {
                clusterName = session.Topology.ClusterName,
                totalHosts = session.Topology.TotalHosts,
                upHosts = session.Topology.UpHosts,
                downHosts = session.Topology.DownHosts,
                datacenters = session.Topology.DatacenterHosts.Keys.ToList()
            } : null,
            summary = new
            {
                totalProbes = session.Results.Count,
                successful = session.Results.Count(r => r.Success),
                failed = session.Results.Count(r => !r.Success),
                averageDurationMs = session.Results.Any() 
                    ? session.Results.Average(r => r.Duration.TotalMilliseconds) 
                    : 0
            },
            results = session.Results.OrderBy(r => r.Host.Address.ToString()).ThenBy(r => r.ProbeType).Select(r => new
            {
                timestamp = r.Timestamp,
                host = r.Host.Address.ToString(),
                port = r.Host.NativePort,
                datacenter = r.Host.Datacenter,
                rack = r.Host.Rack,
                probeType = r.ProbeType.ToString(),
                success = r.Success,
                durationMs = r.Duration.TotalMilliseconds,
                errorMessage = r.ErrorMessage,
                metadata = r.Metadata
            })
        };

        return JsonSerializer.Serialize(report, Options);
    }

    public static string FormatConnectionEvents(IEnumerable<ReconnectionEvent> events)
    {
        var eventData = events.Select(e => new
        {
            timestamp = e.Timestamp,
            host = e.Host.ToString(),
            eventType = e.EventType.ToString(),
            message = e.Message,
            durationMs = e.Duration?.TotalMilliseconds
        });

        return JsonSerializer.Serialize(eventData, Options);
    }
}