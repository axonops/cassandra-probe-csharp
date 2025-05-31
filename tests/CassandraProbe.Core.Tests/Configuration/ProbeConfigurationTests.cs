using CassandraProbe.Core.Configuration;
using FluentAssertions;
using Xunit;

namespace CassandraProbe.Core.Tests.Configuration;

public class ProbeConfigurationTests
{
    [Fact]
    public void ProbeConfiguration_ShouldInitializeWithDefaults()
    {
        // Arrange & Act
        var config = new ProbeConfiguration();

        // Assert
        config.ContactPoints.Should().NotBeNull().And.BeEmpty();
        config.Authentication.Should().NotBeNull();
        config.Connection.Should().NotBeNull();
        config.ProbeSelection.Should().NotBeNull();
        config.Query.Should().NotBeNull();
        config.Logging.Should().NotBeNull();
        config.Scheduling.Should().NotBeNull();
    }

    [Fact]
    public void AuthenticationSettings_ShouldInitializeWithDefaults()
    {
        // Arrange & Act
        var auth = new AuthenticationSettings();

        // Assert
        auth.Username.Should().BeEmpty();
        auth.Password.Should().BeEmpty();
        auth.CqlshrcPath.Should().BeEmpty();
    }

    [Fact]
    public void ConnectionSettings_ShouldInitializeWithDefaults()
    {
        // Arrange & Act
        var conn = new ConnectionSettings();

        // Assert
        conn.Port.Should().Be(9042);
        conn.UseSsl.Should().BeFalse();
        conn.CertificatePath.Should().BeEmpty();
        conn.CaCertificatePath.Should().BeEmpty();
        conn.ConnectionTimeoutSeconds.Should().Be(30);
        conn.RequestTimeoutSeconds.Should().Be(60);
        conn.KeepAliveSeconds.Should().Be(60);
        conn.MaxConnectionsPerHost.Should().Be(2);
    }

    [Fact]
    public void ProbeSelectionSettings_ShouldInitializeWithDefaults()
    {
        // Arrange & Act
        var probe = new ProbeSelectionSettings();

        // Assert
        probe.ProbeNativePort.Should().BeTrue();
        probe.ProbeStoragePort.Should().BeFalse();
        probe.ProbePing.Should().BeFalse();
        probe.ExecuteAllProbes.Should().BeFalse();
        probe.SocketTimeoutMs.Should().Be(5000);
        probe.PingTimeoutMs.Should().Be(5000);
        probe.MaxRetries.Should().Be(3);
        probe.RetryDelayMs.Should().Be(1000);
    }

    [Fact]
    public void QuerySettings_ShouldInitializeWithDefaults()
    {
        // Arrange & Act
        var query = new QuerySettings();

        // Assert
        query.TestCql.Should().Be("SELECT key FROM system.local");
        query.ConsistencyLevel.Should().Be("LOCAL_ONE");
        query.EnableTracing.Should().BeFalse();
        query.QueryTimeoutSeconds.Should().Be(30);
        query.PageSize.Should().Be(5000);
    }

    [Fact]
    public void LoggingSettings_ShouldInitializeWithDefaults()
    {
        // Arrange & Act
        var logging = new LoggingSettings();

        // Assert
        logging.LogDirectory.Should().Be("logs");
        logging.MaxDaysToKeep.Should().Be(7);
        logging.MaxFileSizeMb.Should().Be(100);
        logging.LogFormat.Should().Be("text");
        logging.Quiet.Should().BeFalse();
        logging.Verbose.Should().BeFalse();
        logging.LogLevel.Should().Be("Information");
        logging.LogReconnections.Should().BeTrue();
        logging.ShowConnectionEvents.Should().BeTrue();
        logging.BufferSize.Should().Be(1000);
        logging.FlushIntervalSeconds.Should().Be(5);
    }

    [Fact]
    public void SchedulingSettings_ShouldInitializeWithDefaults()
    {
        // Arrange & Act
        var scheduling = new SchedulingSettings();

        // Assert
        scheduling.IntervalSeconds.Should().BeNull();
        scheduling.CronExpression.Should().BeEmpty();
        scheduling.DurationMinutes.Should().BeNull();
        scheduling.MaxRuns.Should().BeNull();
        scheduling.StartImmediately.Should().BeTrue();
        scheduling.ConcurrentExecutionAllowed.Should().BeFalse();
    }

    [Fact]
    public void ProbeConfiguration_ShouldAcceptCustomValues()
    {
        // Arrange & Act
        var config = new ProbeConfiguration
        {
            ContactPoints = new List<string> { "10.0.0.1", "10.0.0.2" },
            Authentication = new AuthenticationSettings
            {
                Username = "cassandra",
                Password = "cassandra",
                CqlshrcPath = "/home/user/.cassandra/cqlshrc"
            },
            Connection = new ConnectionSettings
            {
                Port = 9043,
                UseSsl = true,
                CertificatePath = "/path/to/cert.pem",
                ConnectionTimeoutSeconds = 60
            },
            ProbeSelection = new ProbeSelectionSettings
            {
                ExecuteAllProbes = true,
                SocketTimeoutMs = 10000
            },
            Query = new QuerySettings
            {
                TestCql = "SELECT * FROM custom_table",
                ConsistencyLevel = "QUORUM",
                EnableTracing = true
            }
        };

        // Assert
        config.ContactPoints.Should().HaveCount(2);
        config.ContactPoints.Should().Contain("10.0.0.1");
        config.Authentication.Username.Should().Be("cassandra");
        config.Connection.Port.Should().Be(9043);
        config.Connection.UseSsl.Should().BeTrue();
        config.ProbeSelection.ExecuteAllProbes.Should().BeTrue();
        config.Query.TestCql.Should().Be("SELECT * FROM custom_table");
        config.Query.ConsistencyLevel.Should().Be("QUORUM");
    }

    [Theory]
    [InlineData("ALL", true, 10000)]
    [InlineData("QUORUM", false, 5000)]
    [InlineData("ONE", true, 3000)]
    public void QuerySettings_ShouldHandleVariousConfigurations(string consistency, bool tracing, int timeout)
    {
        // Arrange & Act
        var query = new QuerySettings
        {
            ConsistencyLevel = consistency,
            EnableTracing = tracing,
            QueryTimeoutSeconds = timeout / 1000
        };

        // Assert
        query.ConsistencyLevel.Should().Be(consistency);
        query.EnableTracing.Should().Be(tracing);
        query.QueryTimeoutSeconds.Should().Be(timeout / 1000);
    }
}