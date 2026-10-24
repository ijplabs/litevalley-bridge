namespace StardewHeadless.Protocol.Enums;

/// <summary>
/// Jenis aksi yang membutuhkan wewenang host (Game1.IsMasterGame) namun didelegasikan dari client.
/// </summary>
public enum HostActionType : byte
{
    None = 0,
    CarpenterBuild = 1,
    CarpenterUpgradeHouse = 2,
    CarpenterCommunityUpgrade = 3,
    CarpenterMoveBuilding = 4,
    CarpenterPaintBuilding = 5,
    JojaMembershipPurchase = 6,
    JojaCommunityDevelopmentProject = 7,
    ManorHouseLedgerDonation = 8,
    PetAdoptionChoice = 9,
    UnlockCommunityCenterRoom = 10,
    ForceFestivalStart = 11,
    EmergencySaveRequest = 12
}
