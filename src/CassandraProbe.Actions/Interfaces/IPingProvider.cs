using System.Net;
using System.Net.NetworkInformation;

namespace CassandraProbe.Actions.Interfaces;

public interface IPingProvider
{
    Task<PingReply> SendPingAsync(IPAddress address, int timeout);
}