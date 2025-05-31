using System.Diagnostics;
using System.Net.Sockets;
using CassandraProbe.Core.Interfaces;
using CassandraProbe.Core.Models;
using Microsoft.Extensions.Logging;

namespace CassandraProbe.Actions.PortSpecificProbes;

public class StoragePortProbe : IProbeAction
{
    private readonly ILogger<StoragePortProbe> _logger;

    public StoragePortProbe(ILogger<StoragePortProbe> logger)
    {
        _logger = logger;
    }

    public string Name => "Storage Port Probe";
    public ProbeType Type => ProbeType.StoragePort;

    public async Task<ProbeResult> ExecuteAsync(HostProbe host, ProbeContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        // Use the timeout from context first, fallback to configuration
        var timeout = context.SocketTimeout != TimeSpan.Zero 
            ? context.SocketTimeout 
            : TimeSpan.FromMilliseconds(context.Configuration.ProbeSelection.SocketTimeoutMs);

        _logger.LogDebug("Storage port probe starting for {Host}:{Port} with timeout {Timeout}ms", 
            host.Address, host.StoragePort, timeout.TotalMilliseconds);

        try
        {
            using var tcpClient = new TcpClient();
            using var cts = new CancellationTokenSource(timeout);
            
            await tcpClient.ConnectAsync(host.Address, host.StoragePort, cts.Token);
            
            if (!tcpClient.Connected)
            {
                throw new SocketException((int)SocketError.NotConnected);
            }

            // Storage port (gossip) uses a different protocol
            // We can only verify TCP connectivity, not the gossip protocol itself
            _logger.LogDebug("Storage port probe connected to {Host}:{Port}", 
                host.Address, host.StoragePort);

            stopwatch.Stop();
            var result = ProbeResult.CreateSuccess(host, Type, stopwatch.Elapsed);
            result.Metadata["Port"] = host.StoragePort;
            result.Metadata["Purpose"] = "Inter-node communication (Gossip)";
            
            // Detect secure storage port (typically 7001)
            if (host.StoragePort == 7001)
            {
                result.Metadata["PortType"] = "SecureStorage";
            }
            
            return result;
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            var result = ProbeResult.CreateFailure(host, Type, "Operation timed out", stopwatch.Elapsed);
            AddMetadata(result, host.StoragePort);
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogDebug(ex, "Storage port probe failed for {Host}:{Port}", 
                host.Address, host.StoragePort);
            
            var result = ProbeResult.CreateFailure(host, Type, 
                $"Storage port connection failed: {ex.Message}", stopwatch.Elapsed);
            AddMetadata(result, host.StoragePort);
            return result;
        }
    }

    private static void AddMetadata(ProbeResult result, int port)
    {
        result.Metadata["Port"] = port;
        result.Metadata["Purpose"] = "Inter-node communication (Gossip)";
        
        // Detect secure storage port (typically 7001)
        if (port == 7001)
        {
            result.Metadata["PortType"] = "SecureStorage";
        }
    }
}