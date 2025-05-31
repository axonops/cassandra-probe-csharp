namespace CassandraProbe.Core.Exceptions;

public class ProbeException : Exception
{
    public ProbeException(string message) : base(message)
    {
    }

    public ProbeException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

public class ConnectionException : ProbeException
{
    public ConnectionException(string message) : base(message)
    {
    }

    public ConnectionException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

public class ProbeConnectionException : ProbeException
{
    public string Host { get; }
    public int Port { get; }

    public ProbeConnectionException(string host, int port, string message, Exception? innerException = null)
        : base($"Failed to connect to {host}:{port}: {message}", innerException)
    {
        Host = host;
        Port = port;
    }
}

public class AuthenticationException : ProbeException
{
    public AuthenticationException(string message) : base(message)
    {
    }

    public AuthenticationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

public class QueryExecutionException : ProbeException
{
    public QueryExecutionException(string message) : base(message)
    {
    }

    public QueryExecutionException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

public class ProbeTimeoutException : ProbeException
{
    public string ProbeType { get; }
    public TimeSpan Timeout { get; }

    public ProbeTimeoutException(string probeType, TimeSpan timeout) 
        : base($"{probeType} probe timed out after {timeout.TotalSeconds} seconds")
    {
        ProbeType = probeType;
        Timeout = timeout;
    }

    public ProbeTimeoutException(string message) : base(message)
    {
        ProbeType = string.Empty;
        Timeout = TimeSpan.Zero;
    }

    public ProbeTimeoutException(string message, Exception innerException) : base(message, innerException)
    {
        ProbeType = string.Empty;
        Timeout = TimeSpan.Zero;
    }
}

public class ConfigurationException : ProbeException
{
    public ConfigurationException(string message) : base(message)
    {
    }

    public ConfigurationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

public class ProbeConfigurationException : ProbeException
{
    public string ParameterName { get; }

    public ProbeConfigurationException(string parameterName, string message)
        : base($"Configuration error for {parameterName}: {message}")
    {
        ParameterName = parameterName;
    }
}

public class ProbeAuthenticationException : ProbeException
{
    public ProbeAuthenticationException(string message) : base(message)
    {
    }

    public ProbeAuthenticationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}