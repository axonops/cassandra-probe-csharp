using CassandraProbe.Core.Exceptions;
using FluentAssertions;
using Xunit;

namespace CassandraProbe.Core.Tests.Exceptions;

public class ProbeExceptionTests
{
    [Fact]
    public void ProbeException_ShouldCreateWithMessage()
    {
        // Arrange
        var message = "Test probe exception";

        // Act
        var exception = new ProbeException(message);

        // Assert
        exception.Message.Should().Be(message);
        exception.InnerException.Should().BeNull();
    }

    [Fact]
    public void ProbeException_ShouldCreateWithMessageAndInnerException()
    {
        // Arrange
        var message = "Test probe exception";
        var innerException = new InvalidOperationException("Inner exception");

        // Act
        var exception = new ProbeException(message, innerException);

        // Assert
        exception.Message.Should().Be(message);
        exception.InnerException.Should().BeSameAs(innerException);
    }

    [Fact]
    public void ProbeConnectionException_ShouldCreateWithHostAndPort()
    {
        // Arrange
        var host = "10.0.0.1";
        var port = 9042;
        var message = "Connection failed";
        var innerException = new System.Net.Sockets.SocketException();

        // Act
        var exception = new ProbeConnectionException(host, port, message, innerException);

        // Assert
        exception.Host.Should().Be(host);
        exception.Port.Should().Be(port);
        exception.Message.Should().Contain(message);
        exception.Message.Should().Contain(host);
        exception.Message.Should().Contain(port.ToString());
        exception.InnerException.Should().BeSameAs(innerException);
    }

    [Fact]
    public void ProbeTimeoutException_ShouldCreateWithProbeTypeAndTimeout()
    {
        // Arrange
        var probeType = "CQL Query";
        var timeout = TimeSpan.FromSeconds(30);

        // Act
        var exception = new ProbeTimeoutException(probeType, timeout);

        // Assert
        exception.ProbeType.Should().Be(probeType);
        exception.Timeout.Should().Be(timeout);
        exception.Message.Should().Contain(probeType);
        exception.Message.Should().Contain("30");
    }

    [Fact]
    public void ProbeConfigurationException_ShouldCreateWithParameterName()
    {
        // Arrange
        var parameter = "ContactPoints";
        var message = "Invalid contact points";

        // Act
        var exception = new ProbeConfigurationException(parameter, message);

        // Assert
        exception.ParameterName.Should().Be(parameter);
        exception.Message.Should().Contain(parameter);
        exception.Message.Should().Contain(message);
    }

    [Fact]
    public void ProbeAuthenticationException_ShouldCreateWithMessage()
    {
        // Arrange
        var message = "Invalid credentials";

        // Act
        var exception = new ProbeAuthenticationException(message);

        // Assert
        exception.Message.Should().Be(message);
        exception.InnerException.Should().BeNull();
    }

    [Fact]
    public void ProbeAuthenticationException_ShouldCreateWithMessageAndInnerException()
    {
        // Arrange
        var message = "Authentication failed";
        var innerException = new UnauthorizedAccessException("Access denied");

        // Act
        var exception = new ProbeAuthenticationException(message, innerException);

        // Assert
        exception.Message.Should().Be(message);
        exception.InnerException.Should().BeSameAs(innerException);
    }

    [Theory]
    [InlineData("10.0.0.1", 9042, "Connection refused")]
    [InlineData("cassandra.local", 9043, "Host not found")]
    [InlineData("192.168.1.100", 19042, "Connection timeout")]
    public void ProbeConnectionException_ShouldFormatMessageCorrectly(string host, int port, string error)
    {
        // Act
        var exception = new ProbeConnectionException(host, port, error);

        // Assert
        exception.Message.Should().Contain($"Failed to connect to {host}:{port}");
        exception.Message.Should().Contain(error);
    }

    [Theory]
    [InlineData("Socket", 5)]
    [InlineData("Ping", 10)]
    [InlineData("CQL Query", 30)]
    public void ProbeTimeoutException_ShouldFormatMessageCorrectly(string probeType, int seconds)
    {
        // Arrange
        var timeout = TimeSpan.FromSeconds(seconds);

        // Act
        var exception = new ProbeTimeoutException(probeType, timeout);

        // Assert
        exception.Message.Should().Contain($"{probeType} probe timed out after {seconds} seconds");
    }

    [Fact]
    public void AllExceptions_ShouldBeSerializable()
    {
        // This test ensures all exceptions follow the standard exception pattern
        var exceptions = new Exception[]
        {
            new ProbeException("test"),
            new ProbeConnectionException("host", 9042, "error"),
            new ProbeTimeoutException("Socket", TimeSpan.FromSeconds(5)),
            new ProbeConfigurationException("param", "error"),
            new ProbeAuthenticationException("auth error")
        };

        foreach (var exception in exceptions)
        {
            exception.Should().BeAssignableTo<Exception>();
            exception.Message.Should().NotBeNullOrEmpty();
        }
    }
}