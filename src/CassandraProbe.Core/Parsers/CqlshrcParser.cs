using System.Text.RegularExpressions;

namespace CassandraProbe.Core.Parsers;

public class CqlshrcParser
{
    public CqlshrcConfig? Parse(string filePath)
    {
        if (!File.Exists(filePath))
            return null;

        var config = new CqlshrcConfig();
        string? currentSection = null;

        foreach (var line in File.ReadAllLines(filePath))
        {
            var trimmedLine = line.Trim();
            
            // Skip empty lines and comments
            if (string.IsNullOrWhiteSpace(trimmedLine) || 
                trimmedLine.StartsWith("#") || 
                trimmedLine.StartsWith(";"))
                continue;

            // Remove inline comments
            var commentIndex = trimmedLine.IndexOfAny(new[] { '#', ';' });
            if (commentIndex > 0)
                trimmedLine = trimmedLine.Substring(0, commentIndex).Trim();

            // Check for section headers
            var sectionMatch = Regex.Match(trimmedLine, @"^\[(\w+)\]$");
            if (sectionMatch.Success)
            {
                currentSection = sectionMatch.Groups[1].Value.ToLower();
                continue;
            }

            // Parse key-value pairs
            var kvMatch = Regex.Match(trimmedLine, @"^(\w+)\s*=\s*(.+)$");
            if (kvMatch.Success)
            {
                var key = kvMatch.Groups[1].Value.Trim();
                var value = kvMatch.Groups[2].Value.Trim();

                switch (currentSection)
                {
                    case "authentication":
                        ParseAuthentication(config, key, value);
                        break;
                    case "connection":
                        ParseConnection(config, key, value);
                        break;
                    case "ssl":
                        ParseSsl(config, key, value);
                        break;
                }
            }
        }

        return config;
    }

    private void ParseAuthentication(CqlshrcConfig config, string key, string value)
    {
        switch (key.ToLower())
        {
            case "username":
                config.Username = value;
                break;
            case "password":
                config.Password = value;
                break;
        }
    }

    private void ParseConnection(CqlshrcConfig config, string key, string value)
    {
        switch (key.ToLower())
        {
            case "hostname":
            case "host":
                config.Hostname = value;
                break;
            case "port":
                if (int.TryParse(value, out var port))
                    config.Port = port;
                break;
            case "timeout":
                if (int.TryParse(value, out var timeout))
                    config.Timeout = timeout;
                break;
        }
    }

    private void ParseSsl(CqlshrcConfig config, string key, string value)
    {
        switch (key.ToLower())
        {
            case "certfile":
                config.CertFile = value;
                break;
            case "keyfile":
                config.KeyFile = value;
                break;
            case "ca_certs":
                config.CaCerts = value;
                break;
            case "validate":
                config.Validate = value.ToLower() == "true" || value == "1";
                break;
        }
    }
}

public class CqlshrcConfig
{
    // Authentication
    public string? Username { get; set; }
    public string? Password { get; set; }
    
    // Connection
    public string Hostname { get; set; } = "localhost";
    public int Port { get; set; } = 9042;
    public int? Timeout { get; set; }
    
    // SSL
    public string? CertFile { get; set; }
    public string? KeyFile { get; set; }
    public string? CaCerts { get; set; }
    public bool? Validate { get; set; }
}