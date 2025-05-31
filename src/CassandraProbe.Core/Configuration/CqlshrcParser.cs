using System.Text.RegularExpressions;

namespace CassandraProbe.Core.Configuration;

public class CqlshrcParser
{
    private readonly Dictionary<string, Dictionary<string, string>> _sections = new();

    public static CqlshrcSettings Parse(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"CQLSHRC file not found: {filePath}");

        var parser = new CqlshrcParser();
        return parser.ParseFile(filePath);
    }

    private CqlshrcSettings ParseFile(string filePath)
    {
        var lines = File.ReadAllLines(filePath);
        string? currentSection = null;

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            
            // Skip empty lines and comments
            if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith("#") || trimmedLine.StartsWith(";"))
                continue;

            // Check for section header
            var sectionMatch = Regex.Match(trimmedLine, @"^\[(.+)\]$");
            if (sectionMatch.Success)
            {
                currentSection = sectionMatch.Groups[1].Value.ToLower();
                if (!_sections.ContainsKey(currentSection))
                    _sections[currentSection] = new Dictionary<string, string>();
                continue;
            }

            // Parse key-value pair
            if (currentSection != null)
            {
                var keyValueMatch = Regex.Match(trimmedLine, @"^([^=]+)=(.+)$");
                if (keyValueMatch.Success)
                {
                    var key = keyValueMatch.Groups[1].Value.Trim().ToLower();
                    var value = keyValueMatch.Groups[2].Value.Trim();
                    _sections[currentSection][key] = value.Trim('"', '\'');
                }
            }
        }

        return BuildSettings();
    }

    private CqlshrcSettings BuildSettings()
    {
        var settings = new CqlshrcSettings();

        // Authentication section
        if (_sections.TryGetValue("authentication", out var authSection))
        {
            if (authSection.TryGetValue("username", out var username))
                settings.Username = username;
            if (authSection.TryGetValue("password", out var password))
                settings.Password = password;
        }

        // Connection section
        if (_sections.TryGetValue("connection", out var connectionSection))
        {
            if (connectionSection.TryGetValue("hostname", out var hostname))
                settings.Hostname = hostname;
            if (connectionSection.TryGetValue("port", out var portStr) && int.TryParse(portStr, out var port))
                settings.Port = port;
            if (connectionSection.TryGetValue("timeout", out var timeoutStr) && int.TryParse(timeoutStr, out var timeout))
                settings.TimeoutSeconds = timeout;
        }

        // SSL section
        if (_sections.TryGetValue("ssl", out var sslSection))
        {
            settings.UseSsl = true;
            if (sslSection.TryGetValue("certfile", out var certFile))
                settings.CertFile = certFile;
            if (sslSection.TryGetValue("validate", out var validate))
                settings.ValidateSsl = validate.ToLower() == "true";
        }

        return settings;
    }
}

public class CqlshrcSettings
{
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? Hostname { get; set; }
    public int? Port { get; set; }
    public int? TimeoutSeconds { get; set; }
    public bool UseSsl { get; set; }
    public string? CertFile { get; set; }
    public bool ValidateSsl { get; set; } = true;
}