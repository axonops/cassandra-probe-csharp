using System.Diagnostics;
using System.Net.Sockets;
using CassandraProbe.Core.Interfaces;
using CassandraProbe.Core.Models;
using Microsoft.Extensions.Logging;

namespace CassandraProbe.Actions.PortSpecificProbes;

public class NativePortProbe : IProbeAction
{
    private readonly ILogger<NativePortProbe> _logger;

    public NativePortProbe(ILogger<NativePortProbe> logger)
    {
        _logger = logger;
    }

    public string Name => "Native Port Probe";
    public ProbeType Type => ProbeType.NativePort;

    public async Task<ProbeResult> ExecuteAsync(HostProbe host, ProbeContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        // Use the timeout from context first, fallback to configuration
        var timeout = context.SocketTimeout != TimeSpan.Zero 
            ? context.SocketTimeout 
            : TimeSpan.FromMilliseconds(context.Configuration.ProbeSelection.SocketTimeoutMs);

        _logger.LogDebug("Native port probe starting for {Host}:{Port} with timeout {Timeout}ms", 
            host.Address, host.NativePort, timeout.TotalMilliseconds);

        try
        {
            using var tcpClient = new TcpClient();
            using var cts = new CancellationTokenSource(timeout);
            
            await tcpClient.ConnectAsync(host.Address, host.NativePort, cts.Token);
            
            if (!tcpClient.Connected)
            {
                throw new SocketException((int)SocketError.NotConnected);
            }

            // Try to send a minimal CQL protocol handshake to verify it's actually Cassandra
            var stream = tcpClient.GetStream();
            
            // CQL protocol version negotiation frame
            // This is a minimal OPTIONS request
            var optionsFrame = new byte[] 
            {
                0x04, // Version (protocol v4)
                0x00, // Flags
                0x00, 0x01, // Stream ID
                0x05, // Opcode (OPTIONS)
                0x00, 0x00, 0x00, 0x00 // Length (0)
            };

            await stream.WriteAsync(optionsFrame, 0, optionsFrame.Length, cts.Token);
            await stream.FlushAsync(cts.Token);

            // Read response header (9 bytes)
            var responseHeader = new byte[9];
            var bytesRead = await stream.ReadAsync(responseHeader, 0, 9, cts.Token);
            
            if (bytesRead >= 9 && responseHeader[4] == 0x06) // SUPPORTED opcode in response
            {
                _logger.LogDebug("Native port probe confirmed CQL protocol on {Host}:{Port}", 
                    host.Address, host.NativePort);
            }

            stopwatch.Stop();
            var result = ProbeResult.CreateSuccess(host, Type, stopwatch.Elapsed);
            result.Metadata["Port"] = host.NativePort;
            return result;
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            return ProbeResult.CreateFailure(host, Type, "Operation timed out", stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogDebug(ex, "Native port probe failed for {Host}:{Port}", 
                host.Address, host.NativePort);
            
            return ProbeResult.CreateFailure(host, Type, 
                $"Native port connection failed: {ex.Message}", stopwatch.Elapsed);
        }
    }
}