using System.Text.Json;
using System.Text.Json.Serialization;

namespace StardewHeadless.Protocol.Logging;

/// <summary>
/// Model entri log terstruktur sesuai spesifikasi docs/08-testing-and-agent-observability.md.
/// </summary>
public class LogEntry
{
    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = DateTime.UtcNow.ToString("o");

    [JsonPropertyName("level")]
    public string Level { get; set; } = "INFO";

    [JsonPropertyName("source")]
    public string Source { get; set; } = "Server";

    [JsonPropertyName("category")]
    public string Category { get; set; } = "General";

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("details")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object?>? Details { get; set; }

    [JsonPropertyName("exception")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Exception { get; set; }

    public string ToJson()
    {
        return JsonSerializer.Serialize(this, LogJsonContext.DefaultOptions);
    }
}

public static class LogJsonContext
{
    public static readonly JsonSerializerOptions DefaultOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static readonly JsonSerializerOptions IndentedOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}
