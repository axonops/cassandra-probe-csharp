using System.Diagnostics;
using System.Net.Sockets;
using CassandraProbe.Core.Interfaces;
using CassandraProbe.Core.Models;
using Microsoft.Extensions.Logging;
using Polly;

namespace CassandraProbe.Actions;

public class SocketProbe : IProbeAction
{
    private readonly ILogger<SocketProbe> _logger;

    public SocketProbe(ILogger<SocketProbe> logger)
    {
        _logger = logger;
    }

    public string Name => "Socket Probe";
    public ProbeType Type => ProbeType.Socket;

    public async Task<ProbeResult> ExecuteAsync(HostProbe host, ProbeContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        // Use the timeout from context first, fallback to configuration
        var timeout = context.SocketTimeout != TimeSpan.Zero 
            ? context.SocketTimeout 
            : TimeSpan.FromMilliseconds(context.Configuration.ProbeSelection.SocketTimeoutMs);

        _logger.LogDebug("Socket probe starting for {Host}:{Port} with timeout {Timeout}ms", 
            host.Address, host.NativePort, timeout.TotalMilliseconds);

        try
        {
            // Define retry policy
            var retryPolicy = Policy
                .Handle<SocketException>()
                .WaitAndRetryAsync(
                    2,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (exception, timeSpan, retryCount, _) =>
                    {
                        _logger.LogDebug("Socket probe retry {RetryCount} for {Host}:{Port} after {Delay}s",
                            retryCount, host.Address, host.NativePort, timeSpan.TotalSeconds);
                    });

            await retryPolicy.ExecuteAsync(async () =>
            {
                using var tcpClient = new TcpClient();
                using var cts = new CancellationTokenSource(timeout);
                
                await tcpClient.ConnectAsync(host.Address, host.NativePort, cts.Token);
                
                if (!tcpClient.Connected)
                {
                    throw new SocketException((int)SocketError.NotConnected);
                }
            });

            stopwatch.Stop();
            return ProbeResult.CreateSuccess(host, Type, stopwatch.Elapsed);
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            return ProbeResult.CreateFailure(host, Type, "Operation timed out", stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogDebug(ex, "Socket probe failed for {Host}:{Port}", 
                host.Address, host.NativePort);
            
            return ProbeResult.CreateFailure(host, Type, 
                $"Socket connection failed: {ex.Message}", stopwatch.Elapsed);
        }
    }
}