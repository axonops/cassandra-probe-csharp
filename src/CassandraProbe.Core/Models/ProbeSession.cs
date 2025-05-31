namespace CassandraProbe.Core.Models;

public class ProbeSession
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public DateTime StartTime { get; init; } = DateTime.UtcNow;
    public DateTime? EndTime { get; set; }
    public List<ProbeResult> Results { get; set; } = new();
    public ClusterTopology? Topology { get; set; }
    public TimeSpan Duration => EndTime.HasValue ? EndTime.Value - StartTime : TimeSpan.Zero;
}