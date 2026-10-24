using System.Text.Json;
using System.Text.Json.Serialization;

namespace StardewHeadless.Protocol.Models;

public class LiveStateSnapshot
{
    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = DateTime.UtcNow.ToString("o");

    [JsonPropertyName("server")]
    public ServerPerformanceSnapshot Server { get; set; } = new();

    [JsonPropertyName("world")]
    public WorldStateSnapshot World { get; set; } = new();

    [JsonPropertyName("players")]
    public List<PlayerStateSnapshot> Players { get; set; } = new();

    [JsonPropertyName("activeLocks")]
    public List<MutexLockSnapshot> ActiveLocks { get; set; } = new();

    [JsonPropertyName("quorum")]
    public QuorumStateSnapshot Quorum { get; set; } = new();

    [JsonPropertyName("diagnostics")]
    public DiagnosticsSnapshot Diagnostics { get; set; } = new();

    public string ToJson(bool indented = true)
    {
        return JsonSerializer.Serialize(this, indented ? Logging.LogJsonContext.IndentedOptions : Logging.LogJsonContext.DefaultOptions);
    }
}

public class ServerPerformanceSnapshot
{
    [JsonPropertyName("uptimeSeconds")]
    public double UptimeSeconds { get; set; }

    [JsonPropertyName("tps")]
    public double Tps { get; set; } = 60.0;

    [JsonPropertyName("targetTps")]
    public double TargetTps { get; set; } = 60.0;

    [JsonPropertyName("memoryMb")]
    public double MemoryMb { get; set; }

    [JsonPropertyName("ticksElapsed")]
    public long TicksElapsed { get; set; }

    [JsonPropertyName("isPaused")]
    public bool IsPaused { get; set; }
}

public class WorldStateSnapshot
{
    [JsonPropertyName("year")]
    public int Year { get; set; } = 1;

    [JsonPropertyName("season")]
    public string Season { get; set; } = "spring";

    [JsonPropertyName("dayOfMonth")]
    public int DayOfMonth { get; set; } = 1;

    [JsonPropertyName("timeOfDay")]
    public int TimeOfDay { get; set; } = 600;

    [JsonPropertyName("daysPlayed")]
    public int DaysPlayed { get; set; }

    [JsonPropertyName("weather")]
    public string Weather { get; set; } = "Sun";

    [JsonPropertyName("festivalToday")]
    public string? FestivalToday { get; set; }
}

public class PlayerStateSnapshot
{
    [JsonPropertyName("farmerId")]
    public long FarmerId { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("location")]
    public string Location { get; set; } = "Farm";

    [JsonPropertyName("tilePosition")]
    public TileVector TilePosition { get; set; } = new();

    [JsonPropertyName("pingMs")]
    public int PingMs { get; set; }

    [JsonPropertyName("isSleeping")]
    public bool IsSleeping { get; set; }

    [JsonPropertyName("activeMenu")]
    public string? ActiveMenu { get; set; }

    [JsonPropertyName("bridgeVersion")]
    public int BridgeVersion { get; set; }
}

public class TileVector
{
    [JsonPropertyName("x")]
    public float X { get; set; }

    [JsonPropertyName("y")]
    public float Y { get; set; }
}

public class MutexLockSnapshot
{
    [JsonPropertyName("mutexId")]
    public string MutexId { get; set; } = string.Empty;

    [JsonPropertyName("owner")]
    public string Owner { get; set; } = string.Empty;

    [JsonPropertyName("acquiredAtUtc")]
    public string AcquiredAtUtc { get; set; } = string.Empty;
}

public class QuorumStateSnapshot
{
    [JsonPropertyName("sleepReadyCount")]
    public int SleepReadyCount { get; set; }

    [JsonPropertyName("sleepTotalCount")]
    public int SleepTotalCount { get; set; }

    [JsonPropertyName("allReady")]
    public bool AllReady { get; set; }
}

public class DiagnosticsSnapshot
{
    [JsonPropertyName("orphanedMutexesCount")]
    public int OrphanedMutexesCount { get; set; }

    [JsonPropertyName("packetErrorsCount")]
    public int PacketErrorsCount { get; set; }

    [JsonPropertyName("lastSaveStatus")]
    public string LastSaveStatus { get; set; } = "NotSaved";
}
