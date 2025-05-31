using System.Net;

namespace CassandraProbe.Core.Models;

public class HostProbe
{
    public IPAddress Address { get; set; } = null!;
    public string? HostId { get; set; }
    public string Datacenter { get; set; } = string.Empty;
    public string Rack { get; set; } = string.Empty;
    public string CassandraVersion { get; set; } = string.Empty;
    public int NativePort { get; set; } = 9042;
    public int StoragePort { get; set; } = 7000;
    public HostStatus Status { get; set; }
    public DateTime LastSeen { get; set; }
    
    public override string ToString()
    {
        return $"{Address}:{NativePort} (DC: {Datacenter}, Rack: {Rack}, Status: {Status})";
    }
}

public enum HostStatus
{
    Unknown,
    Up,
    Down
}