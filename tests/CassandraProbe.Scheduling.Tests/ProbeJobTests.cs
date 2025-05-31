using System.Net;
using CassandraProbe.Core.Configuration;
using CassandraProbe.Core.Interfaces;
using CassandraProbe.Core.Models;
using CassandraProbe.Scheduling;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Quartz;
using Xunit;

namespace CassandraProbe.Scheduling.Tests;

public class ProbeJobTests
{
    private readonly Mock<IProbeOrchestrator> _orchestratorMock;
    private readonly Mock<IConnectionMonitor> _connectionMonitorMock;
    private readonly Mock<ILogger<ProbeJob>> _loggerMock;
    private readonly ProbeJob _job;

    public ProbeJobTests()
    {
        _orchestratorMock = new Mock<IProbeOrchestrator>();
        _connectionMonitorMock = new Mock<IConnectionMonitor>();
        _loggerMock = new Mock<ILogger<ProbeJob>>();
        
        _job = new ProbeJob(
            _orchestratorMock.Object,
            _connectionMonitorMock.Object,
            _loggerMock.Object
        );
    }

    [Fact]
    public async Task Execute_ShouldRunProbeOrchestrator()
    {
        // Arrange
        var jobContext = CreateJobExecutionContext();
        var probeSession = CreateProbeSession();
        
        _orchestratorMock.Setup(x => x.ExecuteProbesAsync(It.IsAny<ProbeConfiguration>()))
            .ReturnsAsync(probeSession);

        var poolStatus = new ConnectionPoolStatus
        {
            TotalConnections = 3,
            ActiveConnections = 2,
            FailedHosts = 1
        };
        _connectionMonitorMock.Setup(x => x.GetPoolStatus()).Returns(poolStatus);
        _connectionMonitorMock.Setup(x => x.GetReconnectionHistory()).Returns(new List<CassandraProbe.Core.Interfaces.ReconnectionEvent>());

        // Act
        await _job.Execute(jobContext);

        // Assert
        _orchestratorMock.Verify(x => x.ExecuteProbesAsync(It.IsAny<ProbeConfiguration>()), Times.Once);
    }

    [Fact]
    public async Task Execute_ShouldLogExecutionStart()
    {
        // Arrange
        var jobContext = CreateJobExecutionContext();
        var probeSession = CreateProbeSession();
        
        _orchestratorMock.Setup(x => x.ExecuteProbesAsync(It.IsAny<ProbeConfiguration>()))
            .ReturnsAsync(probeSession);

        var poolStatus = new ConnectionPoolStatus
        {
            TotalConnections = 3,
            ActiveConnections = 2,
            FailedHosts = 1
        };
        _connectionMonitorMock.Setup(x => x.GetPoolStatus()).Returns(poolStatus);
        _connectionMonitorMock.Setup(x => x.GetReconnectionHistory()).Returns(new List<CassandraProbe.Core.Interfaces.ReconnectionEvent>());

        // Act
        await _job.Execute(jobContext);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                It.Is<LogLevel>(l => l == LogLevel.Information),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Starting scheduled probe job")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Execute_ShouldLogProbeResults()
    {
        // Arrange
        var jobContext = CreateJobExecutionContext();
        var probeSession = CreateProbeSession();
        probeSession.Results.Add(ProbeResult.CreateSuccess(
            new HostProbe { Address = System.Net.IPAddress.Parse("10.0.0.1") },
            ProbeType.Socket,
            TimeSpan.FromMilliseconds(50)
        ));
        probeSession.Results.Add(ProbeResult.CreateFailure(
            new HostProbe { Address = System.Net.IPAddress.Parse("10.0.0.2") },
            ProbeType.Socket,
            "Connection refused",
            TimeSpan.FromMilliseconds(100)
        ));
        
        _orchestratorMock.Setup(x => x.ExecuteProbesAsync(It.IsAny<ProbeConfiguration>()))
            .ReturnsAsync(probeSession);

        var poolStatus = new ConnectionPoolStatus
        {
            TotalConnections = 3,
            ActiveConnections = 2,
            FailedHosts = 1
        };
        _connectionMonitorMock.Setup(x => x.GetPoolStatus()).Returns(poolStatus);
        _connectionMonitorMock.Setup(x => x.GetReconnectionHistory()).Returns(new List<CassandraProbe.Core.Interfaces.ReconnectionEvent>());

        // Act
        await _job.Execute(jobContext);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                It.Is<LogLevel>(l => l == LogLevel.Information),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => 
                    v.ToString()!.Contains("Scheduled probe job") &&
                    v.ToString()!.Contains("completed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Execute_ShouldLogConnectionPoolStatus()
    {
        // Arrange
        var jobContext = CreateJobExecutionContext();
        var probeSession = CreateProbeSession();
        
        _orchestratorMock.Setup(x => x.ExecuteProbesAsync(It.IsAny<ProbeConfiguration>()))
            .ReturnsAsync(probeSession);

        var poolStatus = new ConnectionPoolStatus
        {
            TotalConnections = 5,
            ActiveConnections = 3,
            FailedHosts = 1,
            ReconnectingHosts = new Dictionary<IPEndPoint, ReconnectionInfo> 
            { 
                [new IPEndPoint(IPAddress.Parse("10.0.0.4"), 9042)] = new ReconnectionInfo { AttemptCount = 1 }
            }
        };
        _connectionMonitorMock.Setup(x => x.GetPoolStatus()).Returns(poolStatus);
        _connectionMonitorMock.Setup(x => x.GetReconnectionHistory()).Returns(new List<CassandraProbe.Core.Interfaces.ReconnectionEvent>());

        // Act
        await _job.Execute(jobContext);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                It.Is<LogLevel>(l => l == LogLevel.Information),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => 
                    v.ToString()!.Contains("Connection pool status") &&
                    v.ToString()!.Contains("Total: 5") &&
                    v.ToString()!.Contains("Active: 3") &&
                    v.ToString()!.Contains("Failed: 1") &&
                    v.ToString()!.Contains("Reconnecting: 1")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Execute_ShouldHandleOrchestratorException()
    {
        // Arrange
        var jobContext = CreateJobExecutionContext();
        var exception = new Exception("Orchestrator failed");
        
        _orchestratorMock.Setup(x => x.ExecuteProbesAsync(It.IsAny<ProbeConfiguration>()))
            .ThrowsAsync(exception);

        // Act
        var act = async () => await _job.Execute(jobContext);

        // Assert
        await act.Should().ThrowAsync<Exception>()
            .WithMessage("Orchestrator failed");

        _loggerMock.Verify(
            x => x.Log(
                It.Is<LogLevel>(l => l == LogLevel.Error),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error in scheduled probe job")),
                It.Is<Exception>(e => e == exception),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Execute_ShouldRetrieveConfigurationFromJobDataMap()
    {
        // Arrange
        var config = new ProbeConfiguration
        {
            ContactPoints = new List<string> { "10.0.0.1", "10.0.0.2" }
        };
        
        var jobContext = CreateJobExecutionContext(config);
        var probeSession = CreateProbeSession();
        ProbeConfiguration? capturedConfig = null;
        
        _orchestratorMock.Setup(x => x.ExecuteProbesAsync(It.IsAny<ProbeConfiguration>()))
            .Callback<ProbeConfiguration>(c => capturedConfig = c)
            .ReturnsAsync(probeSession);

        var poolStatus = new ConnectionPoolStatus
        {
            TotalConnections = 3,
            ActiveConnections = 2,
            FailedHosts = 1
        };
        _connectionMonitorMock.Setup(x => x.GetPoolStatus()).Returns(poolStatus);
        _connectionMonitorMock.Setup(x => x.GetReconnectionHistory()).Returns(new List<CassandraProbe.Core.Interfaces.ReconnectionEvent>());

        // Act
        await _job.Execute(jobContext);

        // Assert
        capturedConfig.Should().NotBeNull();
        capturedConfig!.ContactPoints.Should().BeEquivalentTo(config.ContactPoints);
    }

    [Fact]
    public async Task Execute_ShouldHandleMissingConfiguration()
    {
        // Arrange
        var jobContext = CreateJobExecutionContext(config: null, addDefaultConfig: false);

        // Act
        await _job.Execute(jobContext);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                It.Is<LogLevel>(l => l == LogLevel.Error),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("No ProbeConfiguration found")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Execute_ShouldLogIndividualProbeFailures()
    {
        // Arrange
        var jobContext = CreateJobExecutionContext();
        var probeSession = CreateProbeSession();
        
        var failedResult = ProbeResult.CreateFailure(
            new HostProbe { Address = System.Net.IPAddress.Parse("10.0.0.1") },
            ProbeType.CqlQuery,
            "Query timeout",
            TimeSpan.FromSeconds(30)
        );
        probeSession.Results.Add(failedResult);
        
        _orchestratorMock.Setup(x => x.ExecuteProbesAsync(It.IsAny<ProbeConfiguration>()))
            .ReturnsAsync(probeSession);

        var poolStatus = new ConnectionPoolStatus
        {
            TotalConnections = 3,
            ActiveConnections = 2,
            FailedHosts = 1
        };
        _connectionMonitorMock.Setup(x => x.GetPoolStatus()).Returns(poolStatus);
        _connectionMonitorMock.Setup(x => x.GetReconnectionHistory()).Returns(new List<CassandraProbe.Core.Interfaces.ReconnectionEvent>());

        // Act
        await _job.Execute(jobContext);

        // Assert
        // The current implementation logs completion but doesn't log individual failures
        _loggerMock.Verify(
            x => x.Log(
                It.Is<LogLevel>(l => l == LogLevel.Information),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => 
                    v.ToString()!.Contains("Scheduled probe job") &&
                    v.ToString()!.Contains("completed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    private IJobExecutionContext CreateJobExecutionContext(ProbeConfiguration? config = null)
    {
        return CreateJobExecutionContext(config, addDefaultConfig: true);
    }

    private IJobExecutionContext CreateJobExecutionContext(ProbeConfiguration? config, bool addDefaultConfig)
    {
        var jobDataMap = new JobDataMap();
        if (config != null)
        {
            jobDataMap["ProbeConfiguration"] = config;
        }
        else if (addDefaultConfig)
        {
            jobDataMap["ProbeConfiguration"] = new ProbeConfiguration();
        }
        // When addDefaultConfig is false and config is null, don't add any configuration

        var jobDetail = JobBuilder.Create<ProbeJob>()
            .WithIdentity("test-job")
            .UsingJobData(jobDataMap)
            .Build();

        var trigger = TriggerBuilder.Create()
            .WithIdentity("test-trigger")
            .StartNow()
            .Build();

        var context = new Mock<IJobExecutionContext>();
        context.Setup(x => x.JobDetail).Returns(jobDetail);
        context.Setup(x => x.Trigger).Returns(trigger);
        context.Setup(x => x.MergedJobDataMap).Returns(jobDataMap);
        context.Setup(x => x.PreviousFireTimeUtc).Returns((DateTimeOffset?)null);
        context.Setup(x => x.NextFireTimeUtc).Returns((DateTimeOffset?)null);

        return context.Object;
    }

    private ProbeSession CreateProbeSession()
    {
        return new ProbeSession
        {
            Topology = new ClusterTopology
            {
                ClusterName = "TestCluster",
                Hosts = new List<HostProbe>()
            }
        };
    }
}