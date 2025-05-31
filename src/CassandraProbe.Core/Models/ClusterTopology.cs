namespace CassandraProbe.Core.Models;

public class ClusterTopology
{
    public string ClusterName { get; set; } = string.Empty;
    public List<HostProbe> Hosts { get; set; } = new();
    public List<string> Datacenters { get; set; } = new();
    public Dictionary<string, List<HostProbe>> DatacenterHosts { get; set; } = new();
    public DateTime DiscoveredAt { get; set; } = DateTime.UtcNow;
    
    public int TotalHosts => Hosts.Count;
    public int UpHosts => Hosts.Count(h => h.Status == HostStatus.Up);
    public int DownHosts => Hosts.Count(h => h.Status == HostStatus.Down);
}