using System.Text.Json.Serialization;
using CassandraProbe.Core.Models;

namespace CassandraProbe.Logging;

[JsonSerializable(typeof(ProbeResult))]
[JsonSerializable(typeof(ProbeSession))]
[JsonSerializable(typeof(ReconnectionEvent))]
[JsonSerializable(typeof(SessionReport))]
[JsonSerializable(typeof(TopologyReport))]
[JsonSerializable(typeof(SummaryReport))]
[JsonSerializable(typeof(ResultReport))]
[JsonSerializable(typeof(EventReport))]
[JsonSerializable(typeof(List<ReconnectionEvent>))]
[JsonSerializable(typeof(List<ResultReport>))]
[JsonSerializable(typeof(List<EventReport>))]
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(List<string>))]
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true)]
internal partial class ProbeJsonSerializerContext : JsonSerializerContext
{
}

// DTOs for JSON serialization
internal record SessionReport(
    string SessionId,
    DateTime StartTime,
    DateTime? EndTime,
    TimeSpan Duration,
    TopologyReport? Topology,
    SummaryReport Summary,
    List<ResultReport> Results);

internal record TopologyReport(
    string ClusterName,
    int TotalHosts,
    int UpHosts,
    int DownHosts,
    List<string> Datacenters);

internal record SummaryReport(
    int TotalProbes,
    int Successful,
    int Failed,
    double AverageDurationMs);

internal record ResultReport(
    DateTime Timestamp,
    string Host,
    int Port,
    string? Datacenter,
    string? Rack,
    string ProbeType,
    bool Success,
    double DurationMs,
    string? ErrorMessage,
    Dictionary<string, object>? Metadata);

internal record EventReport(
    DateTime Timestamp,
    string Host,
    string EventType,
    string? Message,
    double? DurationMs);