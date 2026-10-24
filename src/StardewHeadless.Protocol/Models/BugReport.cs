using System.Text.Json;
using System.Text.Json.Serialization;

namespace StardewHeadless.Protocol.Models;

/// <summary>
/// Data payload ketika pemain menekan F9 atau mengetik !bug di chat.
/// Disimpan langsung ke logs/bug-reports.jsonl agar agent AI dapat menganalisis disinkronisasi.
/// </summary>
public class BugReport
{
    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = DateTime.UtcNow.ToString("o");

    [JsonPropertyName("reporter")]
    public BugReporterInfo Reporter { get; set; } = new();

    [JsonPropertyName("userNote")]
    public string UserNote { get; set; } = string.Empty;

    [JsonPropertyName("clientState")]
    public ClientBugState ClientState { get; set; } = new();

    [JsonPropertyName("serverState")]
    public ServerBugState ServerState { get; set; } = new();

    [JsonPropertyName("agentGuidance")]
    public string AgentGuidance { get; set; } = "Periksa ketidakcocokan antara clientState dan serverState untuk mendeteksi desinkronisasi atau deadlock.";

    public string ToJson()
    {
        return JsonSerializer.Serialize(this, Logging.LogJsonContext.IndentedOptions);
    }
}

public class BugReporterInfo
{
    [JsonPropertyName("farmerId")]
    public long FarmerId { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("pingMs")]
    public int PingMs { get; set; }
}

public class ClientBugState
{
    [JsonPropertyName("location")]
    public string Location { get; set; } = string.Empty;

    [JsonPropertyName("tilePosition")]
    public TileVector TilePosition { get; set; } = new();

    [JsonPropertyName("facingDirection")]
    public int FacingDirection { get; set; }

    [JsonPropertyName("activeMenu")]
    public string? ActiveMenu { get; set; }

    [JsonPropertyName("canMove")]
    public bool CanMove { get; set; }

    [JsonPropertyName("otherFarmers")]
    public List<OtherFarmerBugInfo> OtherFarmers { get; set; } = new();

    [JsonPropertyName("recentPackets")]
    public List<string> RecentPackets { get; set; } = new();
}

public class OtherFarmerBugInfo
{
    [JsonPropertyName("farmerId")]
    public long FarmerId { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("location")]
    public string Location { get; set; } = string.Empty;

    [JsonPropertyName("tilePosition")]
    public TileVector TilePosition { get; set; } = new();

    [JsonPropertyName("facingDirection")]
    public int FacingDirection { get; set; }

    [JsonPropertyName("isSleeping")]
    public bool IsSleeping { get; set; }
}

public class ServerBugState
{
    [JsonPropertyName("worldTime")]
    public int WorldTime { get; set; }

    [JsonPropertyName("ticksElapsed")]
    public long TicksElapsed { get; set; }

    [JsonPropertyName("sleepReadyCount")]
    public int SleepReadyCount { get; set; }

    [JsonPropertyName("sleepTotalCount")]
    public int SleepTotalCount { get; set; }

    [JsonPropertyName("activeLocks")]
    public List<string> ActiveLocks { get; set; } = new();
}
