namespace StardewHeadless.Protocol.Enums;

/// <summary>
/// Status hasil eksekusi Host Action di server.
/// </summary>
public enum HostActionResultCode : byte
{
    Success = 0,
    Unauthorized = 1,
    InvalidLocation = 2,
    InsufficientFunds = 3,
    InsufficientMaterials = 4,
    ConflictOccupied = 5,
    InvalidBuildingType = 6,
    InternalError = 7,
    Timeout = 8
}
