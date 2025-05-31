using CassandraProbe.Core.Configuration;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace CassandraProbe.Logging;

public static class ProbeLogger
{
    private const string OutputTemplate = 
        "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] {Message:lj} <{SourceContext}>{NewLine}{Exception}";
    
    private const string ConnectionEventTemplate = 
        "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] CONNECTION: {Message:lj}{NewLine}{Exception}";

    public static ILogger CreateLogger(LoggingSettings settings)
    {
        var loggerConfig = new LoggerConfiguration()
            .MinimumLevel.Is(ParseLogLevel(settings.LogLevel))
            .Enrich.FromLogContext()
            .Enrich.WithThreadId()
            .Enrich.WithMachineName();

        // Console output unless quiet mode
        if (!settings.Quiet)
        {
            if (settings.ShowConnectionEvents)
            {
                // Special formatting for connection events
                loggerConfig.WriteTo.Logger(lc => lc
                    .Filter.ByIncludingOnly(evt => evt.Properties.ContainsKey("IsConnectionEvent"))
                    .WriteTo.Console(outputTemplate: ConnectionEventTemplate));
            }

            // Regular console output
            loggerConfig.WriteTo.Console(
                outputTemplate: OutputTemplate,
                restrictedToMinimumLevel: settings.Verbose ? LogEventLevel.Debug : LogEventLevel.Information);
        }

        // File output
        if (!string.IsNullOrEmpty(settings.LogDirectory))
        {
            Directory.CreateDirectory(settings.LogDirectory);
            
            var logPath = Path.Combine(settings.LogDirectory, "cassandra-probe-.log");
            var fileSizeLimitBytes = settings.MaxFileSizeMb * 1024 * 1024;

            if (settings.LogFormat.ToLower() == "json")
            {
                // JSON formatted logs
                loggerConfig.WriteTo.File(
                    formatter: new CompactJsonFormatter(),
                    path: logPath.Replace(".log", ".json"),
                    rollingInterval: RollingInterval.Day,
                    fileSizeLimitBytes: fileSizeLimitBytes,
                    retainedFileCountLimit: settings.MaxDaysToKeep,
                    shared: true);
            }
            else
            {
                // Text formatted logs
                loggerConfig.WriteTo.File(
                    path: logPath,
                    outputTemplate: OutputTemplate,
                    rollingInterval: RollingInterval.Day,
                    fileSizeLimitBytes: fileSizeLimitBytes,
                    retainedFileCountLimit: settings.MaxDaysToKeep,
                    shared: true);
            }

            // Separate file for connection events if enabled
            if (settings.LogReconnections)
            {
                var connectionLogPath = Path.Combine(settings.LogDirectory, "connection-events-.log");
                loggerConfig.WriteTo.Logger(lc => lc
                    .Filter.ByIncludingOnly(evt => 
                        evt.Properties.ContainsKey("IsConnectionEvent") || 
                        evt.MessageTemplate.Text.Contains("Host") ||
                        evt.MessageTemplate.Text.Contains("reconnect", StringComparison.OrdinalIgnoreCase))
                    .WriteTo.File(
                        path: connectionLogPath,
                        outputTemplate: ConnectionEventTemplate,
                        rollingInterval: RollingInterval.Day,
                        fileSizeLimitBytes: fileSizeLimitBytes,
                        retainedFileCountLimit: settings.MaxDaysToKeep,
                        shared: true));
            }
        }

        return loggerConfig.CreateLogger();
    }

    public static ILogger CreateConnectionLogger(ILogger baseLogger)
    {
        return baseLogger.ForContext("IsConnectionEvent", true);
    }

    private static LogEventLevel ParseLogLevel(string level)
    {
        return level.ToLowerInvariant() switch
        {
            "debug" => LogEventLevel.Debug,
            "information" or "info" => LogEventLevel.Information,
            "warning" or "warn" => LogEventLevel.Warning,
            "error" => LogEventLevel.Error,
            "fatal" => LogEventLevel.Fatal,
            _ => LogEventLevel.Information
        };
    }
}