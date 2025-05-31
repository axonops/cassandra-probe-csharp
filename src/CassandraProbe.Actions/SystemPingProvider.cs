using System.Net;
using System.Net.NetworkInformation;
using CassandraProbe.Actions.Interfaces;

namespace CassandraProbe.Actions;

public class SystemPingProvider : IPingProvider
{
    public async Task<PingReply> SendPingAsync(IPAddress address, int timeout)
    {
        using var ping = new Ping();
        return await ping.SendPingAsync(address, timeout);
    }
}