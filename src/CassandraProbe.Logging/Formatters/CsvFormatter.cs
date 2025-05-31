using System.Text;
using CassandraProbe.Core.Models;

namespace CassandraProbe.Logging.Formatters;

public class CsvFormatter
{
    private static readonly string[] Headers = 
    {
        "Timestamp", "Host", "Port", "ProbeType", "Success", 
        "Duration(ms)", "ErrorMessage", "Datacenter", "Rack"
    };

    public static string FormatHeader()
    {
        return string.Join(",", Headers);
    }

    public static string FormatResult(ProbeResult result, string sessionId)
    {
        var values = new[]
        {
            result.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss"),
            result.Host.Address.ToString(),
            result.Host.NativePort.ToString(),
            result.ProbeType.ToString(),
            result.Success.ToString(),
            result.Duration.TotalMilliseconds.ToString("0.##"),
            EscapeCsvValue(result.ErrorMessage ?? ""),
            EscapeCsvValue(result.Host.Datacenter ?? ""),
            EscapeCsvValue(result.Host.Rack ?? "")
        };

        return string.Join(",", values);
    }

    public static string FormatSession(ProbeSession session)
    {
        var sb = new StringBuilder();
        sb.AppendLine(FormatHeader());

        foreach (var result in session.Results.OrderBy(r => r.Timestamp))
        {
            sb.AppendLine(FormatResult(result, session.Id));
        }

        return sb.ToString();
    }

    private static string EscapeCsvValue(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        // Check if quoting is needed before replacing newlines
        bool needsQuoting = value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r');

        // Replace newlines and carriage returns with spaces to avoid breaking CSV structure
        value = value.Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ');

        // Escape quotes and wrap in quotes if needed
        if (needsQuoting)
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}