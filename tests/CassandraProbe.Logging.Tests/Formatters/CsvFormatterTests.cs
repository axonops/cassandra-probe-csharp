using System.Net;
using CassandraProbe.Core.Models;
using CassandraProbe.Logging.Formatters;
using CassandraProbe.TestHelpers;
using FluentAssertions;
using Xunit;

namespace CassandraProbe.Logging.Tests.Formatters;

public class CsvFormatterTests
{
    [Fact]
    public void FormatSession_ShouldReturnCsvWithHeaders()
    {
        // Arrange
        var session = CreateTestSession();

        // Act
        var csv = CsvFormatter.FormatSession(session);

        // Assert
        csv.Should().NotBeNullOrWhiteSpace();
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines.Should().HaveCountGreaterThan(1);
        
        var headers = lines[0];
        headers.Should().Contain("Timestamp");
        headers.Should().Contain("Host");
        headers.Should().Contain("Port");
        headers.Should().Contain("ProbeType");
        headers.Should().Contain("Success");
        headers.Should().Contain("Duration(ms)");
        headers.Should().Contain("ErrorMessage");
        headers.Should().Contain("Datacenter");
        headers.Should().Contain("Rack");
    }

    [Fact]
    public void FormatSession_ShouldIncludeAllResults()
    {
        // Arrange
        var session = CreateTestSession();

        // Act
        var csv = CsvFormatter.FormatSession(session);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // Assert
        lines.Should().HaveCount(3); // Header + 2 results
    }

    [Fact]
    public void FormatSession_ShouldFormatSuccessfulResult()
    {
        // Arrange
        var session = new ProbeSession();
        var host = TestHostBuilder.CreateHost("10.0.0.1", 9042);
        session.Results.Add(ProbeResult.CreateSuccess(
            host, ProbeType.Socket, TimeSpan.FromMilliseconds(123.45)
        ));

        // Act
        var csv = CsvFormatter.FormatSession(session);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var dataLine = lines[1].Split(',');

        // Assert
        dataLine[1].Should().Be("10.0.0.1"); // Host
        dataLine[2].Should().Be("9042"); // Port
        dataLine[3].Should().Be("Socket"); // ProbeType
        dataLine[4].Should().Be("True"); // Success
        dataLine[5].Should().Be("123.45"); // Duration
        dataLine[6].Should().BeEmpty(); // No error message
    }

    [Fact]
    public void FormatSession_ShouldFormatFailedResult()
    {
        // Arrange
        var session = new ProbeSession();
        var host = TestHostBuilder.CreateHost("10.0.0.2", 9042);
        session.Results.Add(ProbeResult.CreateFailure(
            host, ProbeType.CqlQuery, "Connection refused", TimeSpan.FromMilliseconds(500)
        ));

        // Act
        var csv = CsvFormatter.FormatSession(session);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var dataLine = lines[1].Split(',');

        // Assert
        dataLine[1].Should().Be("10.0.0.2"); // Host
        dataLine[4].Should().Be("False"); // Success
        dataLine[5].Should().Be("500"); // Duration
        dataLine[6].Should().Be("Connection refused"); // Error message
    }

    [Fact]
    public void FormatSession_ShouldHandleCommasInErrorMessage()
    {
        // Arrange
        var session = new ProbeSession();
        var host = TestHostBuilder.CreateHost();
        session.Results.Add(ProbeResult.CreateFailure(
            host, ProbeType.Socket, "Error: Connection failed, timeout occurred", TimeSpan.Zero
        ));

        // Act
        var csv = CsvFormatter.FormatSession(session);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // Assert
        // The error message with comma should be properly handled
        lines[1].Should().Contain("\"Error: Connection failed, timeout occurred\"");
    }

    [Fact]
    public void FormatSession_ShouldHandleQuotesInData()
    {
        // Arrange
        var session = new ProbeSession();
        var host = TestHostBuilder.CreateHost();
        host.Datacenter = "dc\"1\"";
        session.Results.Add(ProbeResult.CreateSuccess(host, ProbeType.Socket, TimeSpan.Zero));

        // Act
        var csv = CsvFormatter.FormatSession(session);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // Assert
        lines[1].Should().Contain("\"dc\"\"1\"\"\""); // Escaped quotes
    }

    [Fact]
    public void FormatSession_ShouldFormatTimestamp()
    {
        // Arrange
        var session = new ProbeSession();
        var host = TestHostBuilder.CreateHost();
        var result = ProbeResult.CreateSuccess(host, ProbeType.Socket, TimeSpan.Zero);
        session.Results.Add(result);

        // Act
        var csv = CsvFormatter.FormatSession(session);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var dataLine = lines[1].Split(',');

        // Assert
        var timestamp = dataLine[0];
        timestamp.Should().MatchRegex(@"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}");
    }

    [Fact]
    public void FormatSession_ShouldHandleEmptySession()
    {
        // Arrange
        var session = new ProbeSession();

        // Act
        var csv = CsvFormatter.FormatSession(session);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // Assert
        lines.Should().HaveCount(1); // Only headers
    }

    [Fact]
    public void FormatSession_ShouldOrderResultsByTimestamp()
    {
        // Arrange
        var session = new ProbeSession();
        var host = TestHostBuilder.CreateHost();
        
        // Add results with different timestamps
        var result1 = ProbeResult.CreateSuccess(host, ProbeType.Socket, TimeSpan.Zero);
        Thread.Sleep(10);
        var result2 = ProbeResult.CreateSuccess(host, ProbeType.Ping, TimeSpan.Zero);
        
        // Add in reverse order
        session.Results.Add(result2);
        session.Results.Add(result1);

        // Act
        var csv = CsvFormatter.FormatSession(session);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // Assert
        lines[1].Should().Contain("Socket"); // Earlier result first
        lines[2].Should().Contain("Ping");
    }

    [Fact]
    public void FormatSession_ShouldHandleNullDatacenterAndRack()
    {
        // Arrange
        var session = new ProbeSession();
        var host = new HostProbe
        {
            Address = IPAddress.Parse("10.0.0.1"),
            NativePort = 9042,
            Datacenter = null!,
            Rack = null!
        };
        session.Results.Add(ProbeResult.CreateSuccess(host, ProbeType.Socket, TimeSpan.Zero));

        // Act
        var csv = CsvFormatter.FormatSession(session);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var dataLine = lines[1].Split(',');

        // Assert
        dataLine[7].Should().BeEmpty(); // Datacenter
        dataLine[8].Should().BeEmpty(); // Rack
    }

    [Fact]
    public void FormatSession_ShouldHandleVariousProbeTypes()
    {
        // Arrange
        var session = new ProbeSession();
        var host = TestHostBuilder.CreateHost();
        
        foreach (var probeType in Enum.GetValues<ProbeType>())
        {
            session.Results.Add(ProbeResult.CreateSuccess(host, probeType, TimeSpan.Zero));
        }

        // Act
        var csv = CsvFormatter.FormatSession(session);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // Assert
        lines.Should().HaveCount(6); // Header + 5 probe types
        csv.Should().Contain("Socket");
        csv.Should().Contain("Ping");
        csv.Should().Contain("CqlQuery");
        csv.Should().Contain("NativePort");
        csv.Should().Contain("StoragePort");
    }

    [Fact]
    public void FormatSession_ShouldHandleSpecialCharactersInAllFields()
    {
        // Arrange
        var session = new ProbeSession();
        var host = new HostProbe
        {
            Address = IPAddress.Parse("10.0.0.1"),
            NativePort = 9042,
            Datacenter = "dc,1\n\"test\"",
            Rack = "rack\r\n1"
        };
        session.Results.Add(ProbeResult.CreateFailure(
            host, 
            ProbeType.Socket, 
            "Error:\n\"Connection, failed\"", 
            TimeSpan.Zero
        ));

        // Act
        var csv = CsvFormatter.FormatSession(session);

        // Assert
        csv.Should().NotContain("\r\n", "within data fields");
        csv.Should().Contain("\"dc,1");
        csv.Should().Contain("\"rack");
        csv.Should().Contain("\"Error:");
    }

    private ProbeSession CreateTestSession()
    {
        var session = new ProbeSession();
        var host1 = TestHostBuilder.CreateHost("10.0.0.1");
        var host2 = TestHostBuilder.CreateHost("10.0.0.2");

        session.Results.Add(ProbeResult.CreateSuccess(
            host1, ProbeType.Socket, TimeSpan.FromMilliseconds(50)
        ));
        session.Results.Add(ProbeResult.CreateFailure(
            host2, ProbeType.CqlQuery, "Query failed", TimeSpan.FromMilliseconds(1000)
        ));

        return session;
    }
}