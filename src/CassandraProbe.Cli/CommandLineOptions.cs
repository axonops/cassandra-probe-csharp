using CommandLine;

namespace CassandraProbe.Cli;

public class CommandLineOptions
{
    // Connection Options
    [Option("contact-points", Required = true, HelpText = "Comma-separated list of contact points")]
    public string ContactPoints { get; set; } = string.Empty;

    [Option('P', "port", Default = 9042, HelpText = "Native protocol port")]
    public int Port { get; set; }

    [Option("datacenter", HelpText = "Local datacenter name")]
    public string? Datacenter { get; set; }

    [Option('y', "yaml", HelpText = "Path to cassandra.yaml for configuration")]
    public string? YamlPath { get; set; }

    // Authentication Options
    [Option('u', "username", HelpText = "Cassandra username")]
    public string? Username { get; set; }

    [Option('p', "password", HelpText = "Cassandra password")]
    public string? Password { get; set; }

    [Option('c', "cqlshrc", HelpText = "Path to CQLSHRC file")]
    public string? CqlshrcPath { get; set; }

    [Option("ssl", HelpText = "Enable SSL/TLS")]
    public bool UseSsl { get; set; }

    [Option("cert", HelpText = "Path to client certificate")]
    public string? CertificatePath { get; set; }

    [Option("ca-cert", HelpText = "Path to CA certificate")]
    public string? CaCertificatePath { get; set; }

    // Probe Selection Options
    [Option("native", Default = true, HelpText = "Probe native protocol port")]
    public bool ProbeNative { get; set; }

    [Option("storage", HelpText = "Probe storage/gossip port")]
    public bool ProbeStorage { get; set; }

    [Option("ping", HelpText = "Execute ping/reachability probe")]
    public bool ProbePing { get; set; }

    [Option('a', "all-probes", HelpText = "Execute all probe types")]
    public bool AllProbes { get; set; }

    [Option("socket-timeout", Default = 10000, HelpText = "Socket connection timeout (ms)")]
    public int SocketTimeout { get; set; }

    // Query Options
    [Option("test-cql", HelpText = "Test CQL query to execute")]
    public string? TestCql { get; set; }

    [Option("consistency", Default = "ONE", HelpText = "Consistency level for query")]
    public string ConsistencyLevel { get; set; } = "ONE";

    [Option("tracing", HelpText = "Enable query tracing")]
    public bool EnableTracing { get; set; }

    [Option("query-timeout", Default = 30, HelpText = "Query execution timeout (seconds)")]
    public int QueryTimeout { get; set; }

    // Scheduling Options
    [Option('i', "interval", HelpText = "Seconds between probe executions")]
    public int? IntervalSeconds { get; set; }

    [Option("cron", HelpText = "Cron expression for scheduling")]
    public string? CronExpression { get; set; }

    [Option('d', "duration", HelpText = "Total duration to run (minutes)")]
    public int? DurationMinutes { get; set; }

    [Option("max-runs", HelpText = "Maximum number of probe runs")]
    public int? MaxRuns { get; set; }

    // Logging Options
    [Option("log-dir", Default = "./logs", HelpText = "Directory for log files")]
    public string LogDirectory { get; set; } = "./logs";

    [Option("log-max-days", Default = 7, HelpText = "Maximum days to keep logs")]
    public int LogMaxDays { get; set; }

    [Option("log-max-file-mb", Default = 100, HelpText = "Max log file size before rotation")]
    public int LogMaxFileMb { get; set; }

    [Option("log-format", Default = "text", HelpText = "Log format (text, json)")]
    public string LogFormat { get; set; } = "text";

    [Option('q', "quiet", HelpText = "Suppress console output")]
    public bool Quiet { get; set; }

    [Option('V', "verbose", HelpText = "Enable verbose output")]
    public bool Verbose { get; set; }

    [Option("log-level", Default = "Information", HelpText = "Log level (Debug, Information, Warning, Error)")]
    public string LogLevel { get; set; } = "Information";

    [Option("log-reconnections", Default = true, HelpText = "Log all reconnection events")]
    public bool LogReconnections { get; set; }

    [Option("connection-events", Default = true, HelpText = "Show connection events in console")]
    public bool ShowConnectionEvents { get; set; }

    // Output Options
    [Option('o', "output", Default = "console", HelpText = "Output format (console, json, csv)")]
    public string OutputFormat { get; set; } = "console";

    [Option("output-file", HelpText = "File path for output")]
    public string? OutputFile { get; set; }

    [Option('m', "metrics", HelpText = "Enable metrics collection")]
    public bool EnableMetrics { get; set; }

    [Option("metrics-export", HelpText = "Metrics export format")]
    public string? MetricsExportFormat { get; set; }

    // Resilience Options
    [Option("resilient-client", HelpText = "Use resilient client with enhanced connection recovery")]
    public bool UseResilientClient { get; set; }
    
    [Option("resilient-demo", HelpText = "Run resilient client demonstration showing failure scenarios")]
    public bool RunResilientDemo { get; set; }

    // Other Options
    [Option("config", HelpText = "Path to configuration file")]
    public string? ConfigFile { get; set; }

    [Option("help", HelpText = "Show help information")]
    public bool Help { get; set; }

    [Option("version", HelpText = "Show version information")]
    public bool Version { get; set; }
}