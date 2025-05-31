namespace CassandraProbe.Core.Configuration;

public class ProbeConfiguration
{
    public List<string> ContactPoints { get; set; } = new();
    public AuthenticationSettings Authentication { get; set; } = new();
    public ProbeSelectionSettings ProbeSelection { get; set; } = new();
    public QuerySettings Query { get; set; } = new();
    public LoggingSettings Logging { get; set; } = new();
    public SchedulingSettings Scheduling { get; set; } = new();
    public ConnectionSettings Connection { get; set; } = new();
}

public class ConnectionSettings
{
    public int Port { get; set; } = 9042;
    public bool UseSsl { get; set; } = false;
    public string CertificatePath { get; set; } = string.Empty;
    public string CaCertificatePath { get; set; } = string.Empty;
    public int ConnectionTimeoutSeconds { get; set; } = 30;
    public int RequestTimeoutSeconds { get; set; } = 60;
    public int KeepAliveSeconds { get; set; } = 60;
    public int MaxConnectionsPerHost { get; set; } = 2;
}

public class AuthenticationSettings
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string CqlshrcPath { get; set; } = string.Empty;
}

public class ProbeSelectionSettings
{
    public bool ProbeNativePort { get; set; } = true;
    public bool ProbeStoragePort { get; set; } = false;
    public bool ProbePing { get; set; } = false;
    public bool ExecuteAllProbes { get; set; } = false;
    public int SocketTimeoutMs { get; set; } = 5000;
    public int PingTimeoutMs { get; set; } = 5000;
    public int MaxRetries { get; set; } = 3;
    public int RetryDelayMs { get; set; } = 1000;
}

public class QuerySettings
{
    public string TestCql { get; set; } = "SELECT key FROM system.local";
    public string ConsistencyLevel { get; set; } = "LOCAL_ONE";
    public bool EnableTracing { get; set; } = false;
    public int QueryTimeoutSeconds { get; set; } = 30;
    public int PageSize { get; set; } = 5000;
}

public class LoggingSettings
{
    public string LogDirectory { get; set; } = "logs";
    public int MaxDaysToKeep { get; set; } = 7;
    public int MaxFileSizeMb { get; set; } = 100;
    public string LogFormat { get; set; } = "text";
    public bool Quiet { get; set; } = false;
    public bool Verbose { get; set; } = false;
    public string LogLevel { get; set; } = "Information";
    public bool LogReconnections { get; set; } = true;
    public bool ShowConnectionEvents { get; set; } = true;
    public int BufferSize { get; set; } = 1000;
    public int FlushIntervalSeconds { get; set; } = 5;
}

public class SchedulingSettings
{
    public int? IntervalSeconds { get; set; }
    public string CronExpression { get; set; } = string.Empty;
    public int? DurationMinutes { get; set; }
    public int? MaxRuns { get; set; }
    public bool StartImmediately { get; set; } = true;
    public bool ConcurrentExecutionAllowed { get; set; } = false;
}