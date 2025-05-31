using CassandraProbe.Core.Configuration;

namespace CassandraProbe.TestHelpers;

public static class TestConfigurationBuilder
{
    public static ProbeConfiguration CreateDefault()
    {
        return new ProbeConfiguration
        {
            ContactPoints = new List<string> { "localhost" },
            Authentication = new AuthenticationSettings
            {
                Username = "cassandra",
                Password = "cassandra"
            },
            Connection = new ConnectionSettings
            {
                Port = 9042,
                UseSsl = false
            },
            ProbeSelection = new ProbeSelectionSettings
            {
                ProbeNativePort = true,
                ProbeStoragePort = false,
                ProbePing = false,
                ExecuteAllProbes = false
            },
            Query = new QuerySettings
            {
                TestCql = "SELECT key FROM system.local",
                ConsistencyLevel = "LOCAL_ONE"
            },
            Logging = new LoggingSettings
            {
                LogLevel = "Information",
                Quiet = false
            }
        };
    }

    public static ProbeConfiguration CreateWithAllProbes()
    {
        var config = CreateDefault();
        config.ProbeSelection.ExecuteAllProbes = true;
        return config;
    }

    public static ProbeConfiguration CreateWithSsl(string certPath)
    {
        var config = CreateDefault();
        config.Connection.UseSsl = true;
        config.Connection.CertificatePath = certPath;
        return config;
    }

    public static ProbeConfiguration CreateScheduled(int intervalSeconds)
    {
        var config = CreateDefault();
        config.Scheduling = new SchedulingSettings
        {
            IntervalSeconds = intervalSeconds,
            StartImmediately = true
        };
        return config;
    }
}