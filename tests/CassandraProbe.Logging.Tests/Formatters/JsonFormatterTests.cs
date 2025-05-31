using System.Net;
using CassandraProbe.Core.Models;
using CassandraProbe.Logging.Formatters;
using CassandraProbe.TestHelpers;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CassandraProbe.Logging.Tests.Formatters;

public class JsonFormatterTests
{
    [Fact]
    public void FormatSession_ShouldReturnValidJson()
    {
        // Arrange
        var session = CreateTestSession();

        // Act
        var json = JsonFormatter.FormatSession(session);

        // Assert
        json.Should().NotBeNullOrWhiteSpace();
        var parsed = JObject.Parse(json);
        parsed.Should().NotBeNull();
    }

    [Fact]
    public void FormatSession_ShouldIncludeAllSessionProperties()
    {
        // Arrange
        var session = CreateTestSession();

        // Act
        var json = JsonFormatter.FormatSession(session);
        var parsed = JObject.Parse(json);

        // Assert
        parsed["sessionId"]!.ToString().Should().Be(session.Id);
        parsed["startTime"]!.ToString().Should().NotBeNullOrEmpty();
        parsed["endTime"]!.ToString().Should().NotBeNullOrEmpty();
        parsed["duration"]!.ToString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void FormatSession_ShouldIncludeTopology()
    {
        // Arrange
        var session = CreateTestSession();

        // Act
        var json = JsonFormatter.FormatSession(session);
        var parsed = JObject.Parse(json);

        // Assert
        var topology = parsed["topology"];
        topology.Should().NotBeNull();
        topology!["clusterName"]!.ToString().Should().Be("TestCluster");
        topology["totalHosts"]!.Value<int>().Should().Be(2);
        topology["upHosts"]!.Value<int>().Should().Be(1);
        topology["downHosts"]!.Value<int>().Should().Be(1);
    }

    [Fact]
    public void FormatSession_ShouldIncludeResults()
    {
        // Arrange
        var session = CreateTestSession();

        // Act
        var json = JsonFormatter.FormatSession(session);
        var parsed = JObject.Parse(json);

        // Assert
        var results = parsed["results"] as JArray;
        results.Should().NotBeNull();
        results!.Count.Should().Be(2);

        var firstResult = results[0];
        firstResult["success"]!.Value<bool>().Should().BeTrue();
        firstResult["probeType"]!.ToString().Should().Be("Socket");
        firstResult["host"]!.ToString().Should().Contain("10.0.0.1");
    }

    [Fact]
    public void FormatSession_ShouldHandleFailedResults()
    {
        // Arrange
        var session = CreateTestSession();
        var failedHost = TestHostBuilder.CreateHost("10.0.0.3");
        session.Results.Add(ProbeResult.CreateFailure(
            failedHost, 
            ProbeType.CqlQuery, 
            "Query timeout", 
            TimeSpan.FromSeconds(30)
        ));

        // Act
        var json = JsonFormatter.FormatSession(session);
        var parsed = JObject.Parse(json);

        // Assert
        var results = parsed["results"] as JArray;
        var failedResult = results!.FirstOrDefault(r => r["success"]!.Value<bool>() == false);
        failedResult.Should().NotBeNull();
        failedResult!["errorMessage"]!.ToString().Should().Be("Query timeout");
    }

    [Fact]
    public void FormatSession_ShouldIncludeResultMetadata()
    {
        // Arrange
        var session = new ProbeSession();
        var host = TestHostBuilder.CreateHost();
        var result = ProbeResult.CreateSuccess(host, ProbeType.CqlQuery, TimeSpan.FromMilliseconds(100));
        result.Metadata["RowCount"] = 42;
        result.Metadata["QueryType"] = "SELECT";
        session.Results.Add(result);

        // Act
        var json = JsonFormatter.FormatSession(session);
        var parsed = JObject.Parse(json);

        // Assert
        var results = parsed["results"] as JArray;
        var metadata = results![0]["metadata"];
        metadata.Should().NotBeNull();
        metadata!["RowCount"]!.Value<int>().Should().Be(42);
        metadata["QueryType"]!.ToString().Should().Be("SELECT");
    }

    [Fact]
    public void FormatSession_ShouldHandleEmptySession()
    {
        // Arrange
        var session = new ProbeSession();

        // Act
        var json = JsonFormatter.FormatSession(session);
        var parsed = JObject.Parse(json);

        // Assert
        parsed["results"]!.Should().BeOfType<JArray>();
        (parsed["results"] as JArray)!.Count.Should().Be(0);
        parsed["topology"]!.Type.Should().Be(JTokenType.Null);
    }

    [Fact]
    public void FormatSession_ShouldFormatDurationCorrectly()
    {
        // Arrange
        var session = new ProbeSession();
        session.EndTime = session.StartTime.AddMinutes(5).AddSeconds(30).AddMilliseconds(250);

        // Act
        var json = JsonFormatter.FormatSession(session);
        var parsed = JObject.Parse(json);

        // Assert
        var duration = parsed["duration"]!.ToString();
        duration.Should().Contain("5");
        duration.Should().Contain("30");
    }

    [Fact]
    public void FormatSession_ShouldHandleNullEndTime()
    {
        // Arrange
        var session = new ProbeSession
        {
            EndTime = null
        };

        // Act
        var json = JsonFormatter.FormatSession(session);
        var parsed = JObject.Parse(json);

        // Assert
        parsed["endTime"]!.Type.Should().Be(JTokenType.Null);
        parsed["duration"]!.ToString().Should().Be("00:00:00");
    }

    [Fact]
    public void FormatSession_ShouldEscapeSpecialCharacters()
    {
        // Arrange
        var session = new ProbeSession();
        session.Topology = new ClusterTopology
        {
            ClusterName = "Test\"Cluster'With<Special>Chars&"
        };

        // Act
        var json = JsonFormatter.FormatSession(session);
        var parsed = JObject.Parse(json);

        // Assert
        // JSON parsing should succeed without errors
        parsed["topology"]!["clusterName"]!.ToString()
            .Should().Be("Test\"Cluster'With<Special>Chars&");
    }

    [Fact]
    public void FormatSession_ShouldOrderResultsByHostAndProbeType()
    {
        // Arrange
        var session = new ProbeSession();
        var host1 = TestHostBuilder.CreateHost("10.0.0.1");
        var host2 = TestHostBuilder.CreateHost("10.0.0.2");
        
        // Add in random order
        session.Results.Add(ProbeResult.CreateSuccess(host2, ProbeType.Ping, TimeSpan.FromMilliseconds(10)));
        session.Results.Add(ProbeResult.CreateSuccess(host1, ProbeType.Socket, TimeSpan.FromMilliseconds(10)));
        session.Results.Add(ProbeResult.CreateSuccess(host2, ProbeType.Socket, TimeSpan.FromMilliseconds(10)));
        session.Results.Add(ProbeResult.CreateSuccess(host1, ProbeType.Ping, TimeSpan.FromMilliseconds(10)));

        // Act
        var json = JsonFormatter.FormatSession(session);
        var parsed = JObject.Parse(json);
        var results = parsed["results"] as JArray;

        // Assert
        results!.Count.Should().Be(4);
        // Results should be ordered by host address first, then by probe type
        results[0]["host"]!.ToString().Should().Contain("10.0.0.1");
        results[0]["probeType"]!.ToString().Should().Be("Socket");
        results[1]["host"]!.ToString().Should().Contain("10.0.0.1");
        results[1]["probeType"]!.ToString().Should().Be("Ping");
    }

    private ProbeSession CreateTestSession()
    {
        var session = new ProbeSession
        {
            EndTime = DateTime.UtcNow.AddSeconds(10),
            Topology = TestHostBuilder.CreateTopology("TestCluster", 2)
        };

        var host1 = session.Topology.Hosts[0];
        var host2 = session.Topology.Hosts[1];

        session.Results.Add(ProbeResult.CreateSuccess(
            host1, ProbeType.Socket, TimeSpan.FromMilliseconds(50)
        ));
        session.Results.Add(ProbeResult.CreateSuccess(
            host2, ProbeType.Socket, TimeSpan.FromMilliseconds(75)
        ));

        return session;
    }
}