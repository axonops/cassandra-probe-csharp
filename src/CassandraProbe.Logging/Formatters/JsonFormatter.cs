using System.Text.Json;
using CassandraProbe.Core.Models;

namespace CassandraProbe.Logging.Formatters;

public class JsonFormatter
{
    private static readonly ProbeJsonSerializerContext JsonContext = ProbeJsonSerializerContext.Default;

    public static string FormatResult(ProbeResult result)
    {
        return JsonSerializer.Serialize(result, JsonContext.ProbeResult);
    }

    public static string FormatSession(ProbeSession session)
    {
        var report = new SessionReport(
            SessionId: session.Id,
            StartTime: session.StartTime,
            EndTime: session.EndTime,
            Duration: session.Duration,
            Topology: session.Topology != null ? new TopologyReport(
                ClusterName: session.Topology.ClusterName,
                TotalHosts: session.Topology.TotalHosts,
                UpHosts: session.Topology.UpHosts,
                DownHosts: session.Topology.DownHosts,
                Datacenters: session.Topology.DatacenterHosts.Keys.ToList()
            ) : null,
            Summary: new SummaryReport(
                TotalProbes: session.Results.Count,
                Successful: session.Results.Count(r => r.Success),
                Failed: session.Results.Count(r => !r.Success),
                AverageDurationMs: session.Results.Any() 
                    ? session.Results.Average(r => r.Duration.TotalMilliseconds) 
                    : 0
            ),
            Results: session.Results.OrderBy(r => r.Host.Address.ToString()).ThenBy(r => r.ProbeType).Select(r => new ResultReport(
                Timestamp: r.Timestamp,
                Host: r.Host.Address.ToString(),
                Port: r.Host.NativePort,
                Datacenter: r.Host.Datacenter,
                Rack: r.Host.Rack,
                ProbeType: r.ProbeType.ToString(),
                Success: r.Success,
                DurationMs: r.Duration.TotalMilliseconds,
                ErrorMessage: r.ErrorMessage,
                Metadata: r.Metadata
            )).ToList()
        );

        return JsonSerializer.Serialize(report, JsonContext.SessionReport);
    }

    public static string FormatConnectionEvents(IEnumerable<ReconnectionEvent> events)
    {
        var eventData = events.Select(e => new EventReport(
            Timestamp: e.Timestamp,
            Host: e.Host.ToString(),
            EventType: e.EventType.ToString(),
            Message: e.Message,
            DurationMs: e.Duration?.TotalMilliseconds
        )).ToList();

        return JsonSerializer.Serialize(eventData, JsonContext.ListEventReport);
    }
}