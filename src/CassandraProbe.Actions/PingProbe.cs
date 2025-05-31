using System.Diagnostics;
using System.Net.NetworkInformation;
using CassandraProbe.Actions.Interfaces;
using CassandraProbe.Core.Interfaces;
using CassandraProbe.Core.Models;
using Microsoft.Extensions.Logging;

namespace CassandraProbe.Actions;

public class PingProbe : IProbeAction
{
    private readonly ILogger<PingProbe> _logger;
    private readonly IPingProvider _pingProvider;

    public PingProbe(ILogger<PingProbe> logger, IPingProvider? pingProvider = null)
    {
        _logger = logger;
        _pingProvider = pingProvider ?? new SystemPingProvider();
    }

    public string Name => "Ping Probe";
    public ProbeType Type => ProbeType.Ping;

    public async Task<ProbeResult> ExecuteAsync(HostProbe host, ProbeContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var timeoutMs = (int)context.SocketTimeout.TotalMilliseconds;

        _logger.LogDebug("Ping probe attempting to ping {Host} with timeout {TimeoutMs}ms", host.Address, timeoutMs);

        try
        {
            var reply = await _pingProvider.SendPingAsync(host.Address, timeoutMs);
            
            stopwatch.Stop();

            if (reply.Status == IPStatus.Success)
            {
                var result = ProbeResult.CreateSuccess(host, Type, stopwatch.Elapsed);
                result.Metadata["RoundTripTime"] = reply.RoundtripTime;
                return result;
            }
            else
            {
                return ProbeResult.CreateFailure(host, Type, 
                    $"Ping failed with status: {reply.Status}", stopwatch.Elapsed);
            }
        }
        catch (PingException ex)
        {
            stopwatch.Stop();
            _logger.LogDebug(ex, "Ping probe failed for {Host}", host.Address);
            
            // Try TCP ping as fallback
            return await TcpPingFallback(host, context, stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogDebug(ex, "Ping probe error for {Host}", host.Address);
            
            return ProbeResult.CreateFailure(host, Type, 
                $"Ping error: {ex.Message}", stopwatch.Elapsed);
        }
    }

    private async Task<ProbeResult> TcpPingFallback(HostProbe host, ProbeContext context, TimeSpan icmpDuration)
    {
        _logger.LogDebug("Falling back to TCP ping for {Host}", host.Address);
        
        var stopwatch = Stopwatch.StartNew();
        var timeoutMs = (int)context.SocketTimeout.TotalMilliseconds;

        try
        {
            using var tcpClient = new System.Net.Sockets.TcpClient();
            using var cts = new CancellationTokenSource(timeoutMs);
            
            await tcpClient.ConnectAsync(host.Address, host.NativePort, cts.Token);
            
            stopwatch.Stop();
            
            if (tcpClient.Connected)
            {
                var result = ProbeResult.CreateSuccess(host, Type, icmpDuration + stopwatch.Elapsed);
                result.Metadata["FallbackMethod"] = "TCP";
                result.Metadata["TcpConnectTime"] = stopwatch.ElapsedMilliseconds;
                return result;
            }
            else
            {
                return ProbeResult.CreateFailure(host, Type, 
                    "TCP ping failed: Not connected", icmpDuration + stopwatch.Elapsed);
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return ProbeResult.CreateFailure(host, Type, 
                $"TCP ping failed: {ex.Message}", icmpDuration + stopwatch.Elapsed);
        }
    }
}