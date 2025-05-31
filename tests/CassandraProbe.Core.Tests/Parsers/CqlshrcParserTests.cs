using CassandraProbe.Core.Parsers;
using FluentAssertions;
using Xunit;
using System.IO;

namespace CassandraProbe.Core.Tests.Parsers;

public class CqlshrcParserTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly List<string> _tempFiles = new();

    public CqlshrcParserTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"cqlshrc_tests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        foreach (var file in _tempFiles)
        {
            if (File.Exists(file))
                File.Delete(file);
        }
        
        if (Directory.Exists(_tempDirectory))
            Directory.Delete(_tempDirectory, true);
    }

    private string CreateTempFile(string content)
    {
        var filePath = Path.Combine(_tempDirectory, $"cqlshrc_{Guid.NewGuid()}");
        File.WriteAllText(filePath, content);
        _tempFiles.Add(filePath);
        return filePath;
    }

    [Fact]
    public void Parse_ShouldReturnNullForNonExistentFile()
    {
        // Arrange
        var parser = new CqlshrcParser();
        var nonExistentFile = Path.Combine(_tempDirectory, "does_not_exist");

        // Act
        var config = parser.Parse(nonExistentFile);

        // Assert
        config.Should().BeNull();
    }

    [Fact]
    public void Parse_ShouldParseBasicAuthentication()
    {
        // Arrange
        var content = @"
[authentication]
username = cassandra
password = cassandra123
";
        var filePath = CreateTempFile(content);
        var parser = new CqlshrcParser();

        // Act
        var config = parser.Parse(filePath);

        // Assert
        config.Should().NotBeNull();
        config!.Username.Should().Be("cassandra");
        config.Password.Should().Be("cassandra123");
    }

    [Fact]
    public void Parse_ShouldParseConnectionSettings()
    {
        // Arrange
        var content = @"
[connection]
hostname = 10.0.0.1
port = 9043
timeout = 60
";
        var filePath = CreateTempFile(content);
        var parser = new CqlshrcParser();

        // Act
        var config = parser.Parse(filePath);

        // Assert
        config.Should().NotBeNull();
        config!.Hostname.Should().Be("10.0.0.1");
        config.Port.Should().Be(9043);
        config.Timeout.Should().Be(60);
    }

    [Fact]
    public void Parse_ShouldParseSslSettings()
    {
        // Arrange
        var content = @"
[ssl]
certfile = /path/to/cert.pem
keyfile = /path/to/key.pem
ca_certs = /path/to/ca.pem
validate = true
";
        var filePath = CreateTempFile(content);
        var parser = new CqlshrcParser();

        // Act
        var config = parser.Parse(filePath);

        // Assert
        config.Should().NotBeNull();
        config!.CertFile.Should().Be("/path/to/cert.pem");
        config.KeyFile.Should().Be("/path/to/key.pem");
        config.CaCerts.Should().Be("/path/to/ca.pem");
        config.Validate.Should().BeTrue();
    }

    [Fact]
    public void Parse_ShouldHandleCompleteConfiguration()
    {
        // Arrange
        var content = @"
[authentication]
username = admin
password = secret123

[connection]
hostname = cassandra.example.com
port = 19042
timeout = 120

[ssl]
certfile = /etc/cassandra/cert.pem
validate = false
";
        var filePath = CreateTempFile(content);
        var parser = new CqlshrcParser();

        // Act
        var config = parser.Parse(filePath);

        // Assert
        config.Should().NotBeNull();
        config!.Username.Should().Be("admin");
        config.Password.Should().Be("secret123");
        config.Hostname.Should().Be("cassandra.example.com");
        config.Port.Should().Be(19042);
        config.Timeout.Should().Be(120);
        config.CertFile.Should().Be("/etc/cassandra/cert.pem");
        config.Validate.Should().BeFalse();
    }

    [Fact]
    public void Parse_ShouldIgnoreComments()
    {
        // Arrange
        var content = @"
# This is a comment
[authentication]
# Another comment
username = user1  # Inline comment
password = pass1

; Semicolon comment
[connection]
hostname = localhost ; Another inline comment
";
        var filePath = CreateTempFile(content);
        var parser = new CqlshrcParser();

        // Act
        var config = parser.Parse(filePath);

        // Assert
        config.Should().NotBeNull();
        config!.Username.Should().Be("user1");
        config.Password.Should().Be("pass1");
        config.Hostname.Should().Be("localhost");
    }

    [Fact]
    public void Parse_ShouldHandleEmptyFile()
    {
        // Arrange
        var filePath = CreateTempFile("");
        var parser = new CqlshrcParser();

        // Act
        var config = parser.Parse(filePath);

        // Assert
        config.Should().NotBeNull();
        config!.Username.Should().BeNull();
        config.Password.Should().BeNull();
        config.Hostname.Should().Be("localhost");
        config.Port.Should().Be(9042);
    }

    [Fact]
    public void Parse_ShouldHandleWhitespace()
    {
        // Arrange
        var content = @"
[authentication]
  username   =   user_with_spaces  
  password = pass_with_spaces    

[connection]
    hostname    =    10.0.0.1    
";
        var filePath = CreateTempFile(content);
        var parser = new CqlshrcParser();

        // Act
        var config = parser.Parse(filePath);

        // Assert
        config.Should().NotBeNull();
        config!.Username.Should().Be("user_with_spaces");
        config.Password.Should().Be("pass_with_spaces");
        config.Hostname.Should().Be("10.0.0.1");
    }

    [Fact]
    public void Parse_ShouldHandleInvalidPortGracefully()
    {
        // Arrange
        var content = @"
[connection]
port = invalid_port
";
        var filePath = CreateTempFile(content);
        var parser = new CqlshrcParser();

        // Act
        var config = parser.Parse(filePath);

        // Assert
        config.Should().NotBeNull();
        config!.Port.Should().Be(9042); // Default value
    }

    [Fact]
    public void Parse_ShouldHandleBooleanValues()
    {
        // Arrange
        var content = @"
[ssl]
validate = true
[another_section]
validate = false
[yet_another]
validate = True
[final]
validate = FALSE
";
        var filePath = CreateTempFile(content);
        var parser = new CqlshrcParser();

        // Act
        var config = parser.Parse(filePath);

        // Assert
        config.Should().NotBeNull();
        config!.Validate.Should().BeTrue(); // Should only read from [ssl] section
    }

    [Fact]
    public void Parse_ShouldOnlyReadRelevantSections()
    {
        // Arrange
        var content = @"
[irrelevant_section]
username = wrong_user
password = wrong_pass

[authentication]
username = correct_user
password = correct_pass

[another_irrelevant]
hostname = wrong_host
";
        var filePath = CreateTempFile(content);
        var parser = new CqlshrcParser();

        // Act
        var config = parser.Parse(filePath);

        // Assert
        config.Should().NotBeNull();
        config!.Username.Should().Be("correct_user");
        config.Password.Should().Be("correct_pass");
    }
}