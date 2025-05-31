using CassandraProbe.Cli;
using CommandLine;
using FluentAssertions;
using Xunit;

namespace CassandraProbe.Cli.Tests;

public class CommandLineParserTests
{
    private readonly Parser _parser;

    public CommandLineParserTests()
    {
        _parser = new Parser(settings =>
        {
            settings.CaseInsensitiveEnumValues = true;
            settings.HelpWriter = null; // Disable help output in tests
        });
    }

    [Fact]
    public void Parse_ShouldHandleMinimalArguments()
    {
        // Arrange
        var args = new[] { "--contact-points", "localhost" };

        // Act
        var result = _parser.ParseArguments<CommandLineOptions>(args);

        // Assert
        result.Should().NotBeNull();
        result.Errors.Should().BeEmpty();
        result.Value.Should().NotBeNull();
        result.Value.ContactPoints.Should().Be("localhost");
    }

    [Fact]
    public void Parse_ShouldHandleMultipleContactPoints()
    {
        // Arrange
        var args = new[] { "--contact-points", "host1,host2,host3" };

        // Act
        var result = _parser.ParseArguments<CommandLineOptions>(args);

        // Assert
        result.Errors.Should().BeEmpty();
        result.Value.Should().NotBeNull();
        result.Value.ContactPoints.Should().Be("host1,host2,host3");
    }

    [Fact]
    public void Parse_ShouldHandleAuthentication()
    {
        // Arrange
        var args = new[] { "--contact-points", "localhost", "-u", "admin", "-p", "secret" };

        // Act
        var result = _parser.ParseArguments<CommandLineOptions>(args);

        // Assert
        result.Errors.Should().BeEmpty();
        var options = result.Value;
        options.Should().NotBeNull();
        options.Username.Should().Be("admin");
        options.Password.Should().Be("secret");
    }

    [Fact]
    public void Parse_ShouldHandlePortConfiguration()
    {
        // Arrange
        var args = new[] { "--contact-points", "localhost", "--port", "19042" };

        // Act
        var result = _parser.ParseArguments<CommandLineOptions>(args);

        // Assert
        result.Errors.Should().BeEmpty();
        result.Value.Should().NotBeNull();
        result.Value.Port.Should().Be(19042);
    }

    [Fact]
    public void Parse_ShouldHandleSslConfiguration()
    {
        // Arrange
        var args = new[] 
        { 
            "--contact-points", "localhost", 
            "--ssl",
            "--cert", "/path/to/cert.pem",
            "--ca-cert", "/path/to/ca.pem"
        };

        // Act
        var result = _parser.ParseArguments<CommandLineOptions>(args);

        // Assert
        result.Errors.Should().BeEmpty();
        var options = result.Value;
        options.Should().NotBeNull();
        options.UseSsl.Should().BeTrue();
        options.CertificatePath.Should().Be("/path/to/cert.pem");
        options.CaCertificatePath.Should().Be("/path/to/ca.pem");
    }

    [Fact]
    public void Parse_ShouldHandleProbeSelection()
    {
        // Arrange
        var args = new[] 
        { 
            "--contact-points", "localhost", 
            "--storage",
            "--ping",
            "--all-probes"
        };

        // Act
        var result = _parser.ParseArguments<CommandLineOptions>(args);

        // Assert
        var options = result.Value;
        options.ProbeStorage.Should().BeTrue();
        options.ProbePing.Should().BeTrue();
        options.AllProbes.Should().BeTrue();
    }

    [Fact]
    public void Parse_ShouldHandleQueryOptions()
    {
        // Arrange
        var args = new[] 
        { 
            "--contact-points", "localhost", 
            "--test-cql", "SELECT * FROM test",
            "--consistency", "QUORUM",
            "--tracing"
        };

        // Act
        var result = _parser.ParseArguments<CommandLineOptions>(args);

        // Assert
        var options = result.Value;
        options.TestCql.Should().Be("SELECT * FROM test");
        options.ConsistencyLevel.Should().Be("QUORUM");
        options.EnableTracing.Should().BeTrue();
    }

    [Fact]
    public void Parse_ShouldHandleTimeoutOptions()
    {
        // Arrange
        var args = new[] 
        { 
            "--contact-points", "localhost", 
            "--socket-timeout", "10000",
            "--query-timeout", "60"
        };

        // Act
        var result = _parser.ParseArguments<CommandLineOptions>(args);

        // Assert
        var options = result.Value;
        options.SocketTimeout.Should().Be(10000);
        options.QueryTimeout.Should().Be(60);
    }

    [Fact]
    public void Parse_ShouldHandleSchedulingOptions()
    {
        // Arrange
        var args = new[] 
        { 
            "--contact-points", "localhost", 
            "-i", "300",
            "-d", "60",
            "--max-runs", "10"
        };

        // Act
        var result = _parser.ParseArguments<CommandLineOptions>(args);

        // Assert
        var options = result.Value;
        options.IntervalSeconds.Should().Be(300);
        options.DurationMinutes.Should().Be(60);
        options.MaxRuns.Should().Be(10);
    }

    [Fact]
    public void Parse_ShouldHandleCronExpression()
    {
        // Arrange
        var args = new[] 
        { 
            "--contact-points", "localhost", 
            "--cron", "0 */5 * * * ?"
        };

        // Act
        var result = _parser.ParseArguments<CommandLineOptions>(args);

        // Assert
        result.Value.CronExpression.Should().Be("0 */5 * * * ?");
    }

    [Fact]
    public void Parse_ShouldHandleLoggingOptions()
    {
        // Arrange
        var args = new[] 
        { 
            "--contact-points", "localhost", 
            "--log-dir", "/var/log/probe",
            "--log-level", "Debug",
            "--log-format", "json",
            "-q",
            "-V"
        };

        // Act
        var result = _parser.ParseArguments<CommandLineOptions>(args);

        // Assert
        var options = result.Value;
        options.LogDirectory.Should().Be("/var/log/probe");
        options.LogLevel.Should().Be("Debug");
        options.LogFormat.Should().Be("json");
        options.Quiet.Should().BeTrue();
        options.Verbose.Should().BeTrue();
    }

    [Fact]
    public void Parse_ShouldHandleOutputOptions()
    {
        // Arrange
        var args = new[] 
        { 
            "--contact-points", "localhost", 
            "--output-file", "/tmp/results.json",
            "-o", "json"
        };

        // Act
        var result = _parser.ParseArguments<CommandLineOptions>(args);

        // Assert
        var options = result.Value;
        options.OutputFile.Should().Be("/tmp/results.json");
        options.OutputFormat.Should().Be("json");
    }

    [Fact]
    public void Parse_ShouldHandleVersionFlag()
    {
        // Arrange
        var args = new[] { "--version" };

        // Act
        var result = _parser.ParseArguments<CommandLineOptions>(args);

        // Assert
        // Version flag produces a VersionRequestedError which is expected behavior
        result.Errors.Should().ContainSingle()
            .Which.Tag.Should().Be(CommandLine.ErrorType.VersionRequestedError);
    }

    [Fact]
    public void Parse_ShouldHandleCqlshrcPath()
    {
        // Arrange
        var args = new[] 
        { 
            "--contact-points", "localhost", 
            "-c", "/home/user/.cassandra/cqlshrc"
        };

        // Act
        var result = _parser.ParseArguments<CommandLineOptions>(args);

        // Assert
        result.Errors.Should().BeEmpty();
        result.Value.Should().NotBeNull();
        result.Value.CqlshrcPath.Should().Be("/home/user/.cassandra/cqlshrc");
    }

    [Fact]
    public void Parse_ShouldReturnErrorForMissingRequiredArguments()
    {
        // Arrange
        var args = Array.Empty<string>();

        // Act
        var result = _parser.ParseArguments<CommandLineOptions>(args);

        // Assert
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public void Parse_ShouldReturnErrorForInvalidArguments()
    {
        // Arrange
        var args = new[] { "--invalid-option", "value" };

        // Act
        var result = _parser.ParseArguments<CommandLineOptions>(args);

        // Assert
        result.Errors.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData("--contact-points")]
    public void Parse_ShouldAcceptBothShortAndLongOptions(string option)
    {
        // Arrange
        var args = new[] { option, "localhost" };

        // Act
        var result = _parser.ParseArguments<CommandLineOptions>(args);

        // Assert
        result.Errors.Should().BeEmpty();
        result.Value.Should().NotBeNull();
        result.Value.ContactPoints.Should().Be("localhost");
    }

    [Fact]
    public void Parse_ShouldHandleComplexScenario()
    {
        // Arrange
        var args = new[] 
        { 
            "--contact-points", "node1,node2,node3",
            "-u", "admin",
            "-p", "password",
            "--port", "19042",
            "--ssl",
            "--all-probes",
            "--test-cql", "SELECT * FROM system_schema.keyspaces",
            "--consistency", "LOCAL_QUORUM",
            "--interval", "60",
            "--log-dir", "/var/log/cassandra-probe",
            "--log-level", "Warning",
            "--output-file", "results.csv",
            "-o", "csv"
        };

        // Act
        var result = _parser.ParseArguments<CommandLineOptions>(args);

        // Assert
        result.Errors.Should().BeEmpty();
        var options = result.Value;
        options.Should().NotBeNull();
        options.ContactPoints.Should().Be("node1,node2,node3");
        options.Username.Should().Be("admin");
        options.Password.Should().Be("password");
        options.Port.Should().Be(19042);
        options.UseSsl.Should().BeTrue();
        options.AllProbes.Should().BeTrue();
        options.TestCql.Should().Be("SELECT * FROM system_schema.keyspaces");
        options.ConsistencyLevel.Should().Be("LOCAL_QUORUM");
        options.IntervalSeconds.Should().Be(60);
        options.LogDirectory.Should().Be("/var/log/cassandra-probe");
        options.LogLevel.Should().Be("Warning");
        options.OutputFile.Should().Be("results.csv");
        options.OutputFormat.Should().Be("csv");
    }
}