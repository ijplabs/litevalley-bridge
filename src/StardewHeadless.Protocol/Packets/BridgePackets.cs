using System.Security.Cryptography;
using System.Text;
using StardewHeadless.Protocol.Enums;

namespace StardewHeadless.Protocol.Packets;

public abstract class BridgePacket
{
    public abstract byte SubOpCode { get; }

    public abstract void WritePayload(BinaryWriter writer);
    public abstract void ReadPayload(BinaryReader reader);

    public byte[] Serialize()
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms, Encoding.UTF8);
        writer.Write(SubOpCode);
        WritePayload(writer);
        return ms.ToArray();
    }
}

public class ClientHelloPacket : BridgePacket
{
    public override byte SubOpCode => ProtocolConstants.SubOpCodes.ClientHello;

    public int ProtocolVersion { get; set; } = ProtocolConstants.ProtocolVersion;
    public long FarmerId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public string BridgeVersion { get; set; } = "0.2.0";
    public string? SessionToken { get; set; }

    public override void WritePayload(BinaryWriter writer)
    {
        writer.Write(ProtocolVersion);
        writer.Write(FarmerId);
        writer.Write(ClientName);
        writer.Write(BridgeVersion);
        if (!string.IsNullOrEmpty(SessionToken))
        {
            writer.Write(SessionToken);
        }
    }

    public override void ReadPayload(BinaryReader reader)
    {
        ProtocolVersion = reader.ReadInt32();
        FarmerId = reader.ReadInt64();
        ClientName = reader.ReadString();
        BridgeVersion = reader.ReadString();
        if (reader.BaseStream.Position < reader.BaseStream.Length)
        {
            SessionToken = reader.ReadString();
        }
    }
}

public class ServerWelcomePacket : BridgePacket
{
    public override byte SubOpCode => ProtocolConstants.SubOpCodes.ServerWelcome;

    public int ProtocolVersion { get; set; } = ProtocolConstants.ProtocolVersion;
    public long AssignedFarmerId { get; set; }
    public string ServerName { get; set; } = "StardewHeadless";
    public long ServerTick { get; set; }
    public int Year { get; set; } = 1;
    public string Season { get; set; } = "spring";
    public int DayOfMonth { get; set; } = 1;
    public int TimeOfDay { get; set; } = 600;

    public override void WritePayload(BinaryWriter writer)
    {
        writer.Write(ProtocolVersion);
        writer.Write(AssignedFarmerId);
        writer.Write(ServerName);
        writer.Write(ServerTick);
        writer.Write(Year);
        writer.Write(Season);
        writer.Write(DayOfMonth);
        writer.Write(TimeOfDay);
    }

    public override void ReadPayload(BinaryReader reader)
    {
        ProtocolVersion = reader.ReadInt32();
        AssignedFarmerId = reader.ReadInt64();
        ServerName = reader.ReadString();
        ServerTick = reader.ReadInt64();
        Year = reader.ReadInt32();
        Season = reader.ReadString();
        DayOfMonth = reader.ReadInt32();
        TimeOfDay = reader.ReadInt32();
    }
}

public class ClientHeartbeatPacket : BridgePacket
{
    public override byte SubOpCode => ProtocolConstants.SubOpCodes.ClientHeartbeat;

    public long FarmerId { get; set; }
    public int Sequence { get; set; }
    public long ClientTimestampUtcTicks { get; set; }

    public override void WritePayload(BinaryWriter writer)
    {
        writer.Write(FarmerId);
        writer.Write(Sequence);
        writer.Write(ClientTimestampUtcTicks);
    }

    public override void ReadPayload(BinaryReader reader)
    {
        FarmerId = reader.ReadInt64();
        Sequence = reader.ReadInt32();
        ClientTimestampUtcTicks = reader.ReadInt64();
    }
}

public class RequestHostActionPacket : BridgePacket
{
    public override byte SubOpCode => ProtocolConstants.SubOpCodes.RequestHostAction;

    public long ActionId { get; set; }
    public HostActionType ActionType { get; set; }
    public string TargetLocation { get; set; } = string.Empty;
    public int TargetTileX { get; set; }
    public int TargetTileY { get; set; }
    public string ExtraDataJson { get; set; } = string.Empty;

    public override void WritePayload(BinaryWriter writer)
    {
        writer.Write(ActionId);
        writer.Write((byte)ActionType);
        writer.Write(TargetLocation);
        writer.Write(TargetTileX);
        writer.Write(TargetTileY);
        writer.Write(ExtraDataJson);
    }

    public override void ReadPayload(BinaryReader reader)
    {
        ActionId = reader.ReadInt64();
        ActionType = (HostActionType)reader.ReadByte();
        TargetLocation = reader.ReadString();
        TargetTileX = reader.ReadInt32();
        TargetTileY = reader.ReadInt32();
        ExtraDataJson = reader.ReadString();
    }
}

public class HostActionResultPacket : BridgePacket
{
    public override byte SubOpCode => ProtocolConstants.SubOpCodes.HostActionResult;

    public long ActionId { get; set; }
    public HostActionResultCode ResultCode { get; set; }
    public string Message { get; set; } = string.Empty;

    public override void WritePayload(BinaryWriter writer)
    {
        writer.Write(ActionId);
        writer.Write((byte)ResultCode);
        writer.Write(Message);
    }

    public override void ReadPayload(BinaryReader reader)
    {
        ActionId = reader.ReadInt64();
        ResultCode = (HostActionResultCode)reader.ReadByte();
        Message = reader.ReadString();
    }
}

public class QuorumStatePacket : BridgePacket
{
    public override byte SubOpCode => ProtocolConstants.SubOpCodes.QuorumStateBroadcast;

    public int SleepReadyCount { get; set; }
    public int SleepTotalCount { get; set; }
    public bool AllReady { get; set; }
    public int CountdownSeconds { get; set; }

    public override void WritePayload(BinaryWriter writer)
    {
        writer.Write(SleepReadyCount);
        writer.Write(SleepTotalCount);
        writer.Write(AllReady);
        writer.Write(CountdownSeconds);
    }

    public override void ReadPayload(BinaryReader reader)
    {
        SleepReadyCount = reader.ReadInt32();
        SleepTotalCount = reader.ReadInt32();
        AllReady = reader.ReadBoolean();
        CountdownSeconds = reader.ReadInt32();
    }
}

public class ForceClientReadyPacket : BridgePacket
{
    public override byte SubOpCode => ProtocolConstants.SubOpCodes.ForceClientReady;

    public byte ReadyType { get; set; } // 1 = Sleep, 2 = Festival
    public string Reason { get; set; } = string.Empty;

    public override void WritePayload(BinaryWriter writer)
    {
        writer.Write(ReadyType);
        writer.Write(Reason);
    }

    public override void ReadPayload(BinaryReader reader)
    {
        ReadyType = reader.ReadByte();
        Reason = reader.ReadString();
    }
}

public class ClientBugReportSignalPacket : BridgePacket
{
    public override byte SubOpCode => ProtocolConstants.SubOpCodes.ClientBugReportSignal;

    public string UserNote { get; set; } = string.Empty;
    public string ClientLocation { get; set; } = string.Empty;
    public float TileX { get; set; }
    public float TileY { get; set; }
    public string ActiveMenu { get; set; } = string.Empty;

    public override void WritePayload(BinaryWriter writer)
    {
        writer.Write(UserNote);
        writer.Write(ClientLocation);
        writer.Write(TileX);
        writer.Write(TileY);
        writer.Write(ActiveMenu);
    }

    public override void ReadPayload(BinaryReader reader)
    {
        UserNote = reader.ReadString();
        ClientLocation = reader.ReadString();
        TileX = reader.ReadSingle();
        TileY = reader.ReadSingle();
        ActiveMenu = reader.ReadString();
    }
}

public class ProfessionSelectRequestPacket : BridgePacket
{
    public override byte SubOpCode => ProtocolConstants.SubOpCodes.ProfessionSelectRequest;

    public long FarmerId { get; set; }
    public string ProfessionName { get; set; } = string.Empty;

    public override void WritePayload(BinaryWriter writer)
    {
        writer.Write(FarmerId);
        writer.Write(ProfessionName);
    }

    public override void ReadPayload(BinaryReader reader)
    {
        FarmerId = reader.ReadInt64();
        ProfessionName = reader.ReadString();
    }
}

public class ProfessionSyncResponsePacket : BridgePacket
{
    public override byte SubOpCode => ProtocolConstants.SubOpCodes.ProfessionSyncResponse;

    public long FarmerId { get; set; }
    public string ConfirmedProfession { get; set; } = string.Empty;
    public bool Success { get; set; } = true;
    public string Message { get; set; } = string.Empty;
    public int VillageBankBalance { get; set; }

    public override void WritePayload(BinaryWriter writer)
    {
        writer.Write(FarmerId);
        writer.Write(ConfirmedProfession);
        writer.Write(Success);
        writer.Write(Message);
        writer.Write(VillageBankBalance);
    }

    public override void ReadPayload(BinaryReader reader)
    {
        FarmerId = reader.ReadInt64();
        ConfirmedProfession = reader.ReadString();
        Success = reader.ReadBoolean();
        Message = reader.ReadString();
        VillageBankBalance = reader.ReadInt32();
    }
}

public class VillageTreasuryUpdatePacket : BridgePacket
{
    public override byte SubOpCode => ProtocolConstants.SubOpCodes.VillageTreasuryUpdate;

    public int NewBalance { get; set; }
    public string LastContributorName { get; set; } = string.Empty;
    public int LastContributionAmount { get; set; }

    public override void WritePayload(BinaryWriter writer)
    {
        writer.Write(NewBalance);
        writer.Write(LastContributorName);
        writer.Write(LastContributionAmount);
    }

    public override void ReadPayload(BinaryReader reader)
    {
        NewBalance = reader.ReadInt32();
        LastContributorName = reader.ReadString();
        LastContributionAmount = reader.ReadInt32();
    }
}

public class TreasuryDepositRequestPacket : BridgePacket
{
    public override byte SubOpCode => ProtocolConstants.SubOpCodes.TreasuryDepositRequest;

    public long FarmerId { get; set; }
    public int Amount { get; set; }

    public override void WritePayload(BinaryWriter writer)
    {
        writer.Write(FarmerId);
        writer.Write(Amount);
    }

    public override void ReadPayload(BinaryReader reader)
    {
        FarmerId = reader.ReadInt64();
        Amount = reader.ReadInt32();
    }
}

public class AuthChallengePacket : BridgePacket
{
    public override byte SubOpCode => ProtocolConstants.SubOpCodes.AuthChallenge;

    public string ChallengeNonce { get; set; } = string.Empty;
    public string ServerName { get; set; } = string.Empty;
    public bool IsRegistrationRequired { get; set; }
    public string Message { get; set; } = string.Empty;

    public override void WritePayload(BinaryWriter writer)
    {
        writer.Write(ChallengeNonce);
        writer.Write(ServerName);
        writer.Write(IsRegistrationRequired);
        writer.Write(Message);
    }

    public override void ReadPayload(BinaryReader reader)
    {
        ChallengeNonce = reader.ReadString();
        ServerName = reader.ReadString();
        IsRegistrationRequired = reader.ReadBoolean();
        Message = reader.ReadString();
    }
}

public class AuthSubmitPacket : BridgePacket
{
    public override byte SubOpCode => ProtocolConstants.SubOpCodes.AuthSubmit;

    public long FarmerId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string ChosenProfession { get; set; } = string.Empty;

    public override void WritePayload(BinaryWriter writer)
    {
        writer.Write(FarmerId);
        writer.Write(Username);
        writer.Write(PasswordHash);
        writer.Write(ChosenProfession);
    }

    public override void ReadPayload(BinaryReader reader)
    {
        FarmerId = reader.ReadInt64();
        Username = reader.ReadString();
        PasswordHash = reader.ReadString();
        ChosenProfession = reader.ReadString();
    }
}

public class AuthSuccessPacket : BridgePacket
{
    public override byte SubOpCode => ProtocolConstants.SubOpCodes.AuthSuccess;

    public long FarmerId { get; set; }
    public string SessionToken { get; set; } = string.Empty;
    public int AssignedCabinIndex { get; set; }
    public string LockedProfession { get; set; } = string.Empty;
    public string WelcomeMessage { get; set; } = string.Empty;

    public override void WritePayload(BinaryWriter writer)
    {
        writer.Write(FarmerId);
        writer.Write(SessionToken);
        writer.Write(AssignedCabinIndex);
        writer.Write(LockedProfession);
        writer.Write(WelcomeMessage);
    }

    public override void ReadPayload(BinaryReader reader)
    {
        FarmerId = reader.ReadInt64();
        SessionToken = reader.ReadString();
        AssignedCabinIndex = reader.ReadInt32();
        LockedProfession = reader.ReadString();
        WelcomeMessage = reader.ReadString();
    }
}

public class AuthRejectPacket : BridgePacket
{
    public override byte SubOpCode => ProtocolConstants.SubOpCodes.AuthReject;

    public long FarmerId { get; set; }
    public byte ReasonCode { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;

    public override void WritePayload(BinaryWriter writer)
    {
        writer.Write(FarmerId);
        writer.Write(ReasonCode);
        writer.Write(ErrorMessage);
    }

    public override void ReadPayload(BinaryReader reader)
    {
        FarmerId = reader.ReadInt64();
        ReasonCode = reader.ReadByte();
        ErrorMessage = reader.ReadString();
    }
}

public class ClientModInfo
{
    public string ModId { get; set; } = string.Empty;
    public string ModName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public bool HasBinary { get; set; }
    public string DllSha256 { get; set; } = string.Empty;
}

public class ClientModAuditPacket : BridgePacket
{
    public override byte SubOpCode => ProtocolConstants.SubOpCodes.ClientModAudit;

    public long FarmerId { get; set; }
    public string HwidHash { get; set; } = string.Empty;
    public List<ClientModInfo> InstalledMods { get; set; } = new();

    public override void WritePayload(BinaryWriter writer)
    {
        writer.Write(FarmerId);
        writer.Write(HwidHash);
        writer.Write(InstalledMods.Count);
        foreach (var mod in InstalledMods)
        {
            writer.Write(mod.ModId);
            writer.Write(mod.ModName);
            writer.Write(mod.Version);
            writer.Write(mod.HasBinary);
            writer.Write(mod.DllSha256 ?? string.Empty);
        }
    }

    public override void ReadPayload(BinaryReader reader)
    {
        FarmerId = reader.ReadInt64();
        HwidHash = reader.ReadString();
        int count = Math.Clamp(reader.ReadInt32(), 0, 512);
        InstalledMods = new List<ClientModInfo>(count);
        for (int i = 0; i < count; i++)
        {
            var info = new ClientModInfo
            {
                ModId = reader.ReadString(),
                ModName = reader.ReadString(),
                Version = reader.ReadString(),
                HasBinary = reader.ReadBoolean(),
                DllSha256 = reader.ReadString()
            };
            InstalledMods.Add(info);
        }
    }
}

public class ClientModAuditResponsePacket : BridgePacket
{
    public override byte SubOpCode => ProtocolConstants.SubOpCodes.ClientModAuditResponse;

    public long FarmerId { get; set; }
    public bool IsAllowed { get; set; }
    public string RejectionReason { get; set; } = string.Empty;

    public override void WritePayload(BinaryWriter writer)
    {
        writer.Write(FarmerId);
        writer.Write(IsAllowed);
        writer.Write(RejectionReason);
    }

    public override void ReadPayload(BinaryReader reader)
    {
        FarmerId = reader.ReadInt64();
        IsAllowed = reader.ReadBoolean();
        RejectionReason = reader.ReadString();
    }
}

public class HarmonyPatchAnomaly
{
    public string MethodName { get; set; } = string.Empty;
    public string PatchOwnerId { get; set; } = string.Empty;
    public string PatchAssemblyName { get; set; } = string.Empty;
}

public class ClientIntegrityHeartbeatPacket : BridgePacket
{
    public override byte SubOpCode => ProtocolConstants.SubOpCodes.ClientIntegrityHeartbeat;

    public long FarmerId { get; set; }
    public long TimestampTicks { get; set; }
    public string Nonce { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
    public List<HarmonyPatchAnomaly> DetectedPatches { get; set; } = new();

    public override void WritePayload(BinaryWriter writer)
    {
        writer.Write(FarmerId);
        writer.Write(TimestampTicks);
        writer.Write(Nonce);
        writer.Write(Signature);
        writer.Write(DetectedPatches.Count);
        foreach (var p in DetectedPatches)
        {
            writer.Write(p.MethodName ?? string.Empty);
            writer.Write(p.PatchOwnerId ?? string.Empty);
            writer.Write(p.PatchAssemblyName ?? string.Empty);
        }
    }

    public override void ReadPayload(BinaryReader reader)
    {
        FarmerId = reader.ReadInt64();
        TimestampTicks = reader.ReadInt64();
        Nonce = reader.ReadString();
        Signature = reader.ReadString();
        int count = Math.Clamp(reader.ReadInt32(), 0, 512);
        DetectedPatches = new List<HarmonyPatchAnomaly>(count);
        for (int i = 0; i < count; i++)
        {
            DetectedPatches.Add(new HarmonyPatchAnomaly
            {
                MethodName = reader.ReadString(),
                PatchOwnerId = reader.ReadString(),
                PatchAssemblyName = reader.ReadString()
            });
        }
    }

    public static string ComputeSignature(long farmerId, long timestampTicks, string nonce, string secretKey = "StardewBridgeDefaultKey")
    {
        string payload = $"{farmerId}:{timestampTicks}:{nonce}";
        byte[] keyBytes = Encoding.UTF8.GetBytes(string.IsNullOrEmpty(secretKey) ? "StardewBridgeDefaultKey" : secretKey);
        byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);

        using var hmac = new HMACSHA256(keyBytes);
        byte[] hash = hmac.ComputeHash(payloadBytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public bool VerifySignature(string secretKey = "StardewBridgeDefaultKey")
    {
        string expected = ComputeSignature(FarmerId, TimestampTicks, Nonce, secretKey);
        return string.Equals(expected, Signature, StringComparison.OrdinalIgnoreCase);
    }
}

public class ClientIntegrityHeartbeatAckPacket : BridgePacket
{
    public override byte SubOpCode => ProtocolConstants.SubOpCodes.ClientIntegrityHeartbeatAck;

    public long FarmerId { get; set; }
    public bool IsValid { get; set; }
    public int ActiveStrikes { get; set; }

    public override void WritePayload(BinaryWriter writer)
    {
        writer.Write(FarmerId);
        writer.Write(IsValid);
        writer.Write(ActiveStrikes);
    }

    public override void ReadPayload(BinaryReader reader)
    {
        FarmerId = reader.ReadInt64();
        IsValid = reader.ReadBoolean();
        ActiveStrikes = reader.ReadInt32();
    }
}

public class SkullElevatorUseRequestPacket : BridgePacket
{
    public override byte SubOpCode => ProtocolConstants.SubOpCodes.SkullElevatorUseRequest;

    public long FarmerId { get; set; }
    public int TargetFloor { get; set; }

    public override void WritePayload(BinaryWriter writer)
    {
        writer.Write(FarmerId);
        writer.Write(TargetFloor);
    }

    public override void ReadPayload(BinaryReader reader)
    {
        FarmerId = reader.ReadInt64();
        TargetFloor = reader.ReadInt32();
    }
}

public class SkullElevatorUseResponsePacket : BridgePacket
{
    public override byte SubOpCode => ProtocolConstants.SubOpCodes.SkullElevatorUseResponse;

    public long FarmerId { get; set; }
    public int TargetFloor { get; set; }
    public int FeeDeducted { get; set; }
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;

    public override void WritePayload(BinaryWriter writer)
    {
        writer.Write(FarmerId);
        writer.Write(TargetFloor);
        writer.Write(FeeDeducted);
        writer.Write(Success);
        writer.Write(Message);
    }

    public override void ReadPayload(BinaryReader reader)
    {
        FarmerId = reader.ReadInt64();
        TargetFloor = reader.ReadInt32();
        FeeDeducted = reader.ReadInt32();
        Success = reader.ReadBoolean();
        Message = reader.ReadString();
    }
}


public class StarterKitItemEntry
{
    public string ItemId { get; set; } = string.Empty;
    public string ItemType { get; set; } = "(O)"; // (O) for object, (BC) for bigcraftable, (T) for tool
    public int Stack { get; set; } = 1;
    public int Quality { get; set; } = 0;
    public int UpgradeLevel { get; set; } = 0;
    public bool TargetToolbelt { get; set; } = false;

    public void Write(BinaryWriter writer)
    {
        writer.Write(ItemId);
        writer.Write(ItemType);
        writer.Write(Stack);
        writer.Write(Quality);
        writer.Write(UpgradeLevel);
        writer.Write(TargetToolbelt);
    }

    public static StarterKitItemEntry Read(BinaryReader reader)
    {
        return new StarterKitItemEntry
        {
            ItemId = reader.ReadString(),
            ItemType = reader.ReadString(),
            Stack = reader.ReadInt32(),
            Quality = reader.ReadInt32(),
            UpgradeLevel = reader.ReadInt32(),
            TargetToolbelt = reader.ReadBoolean()
        };
    }
}

public class StarterKitGrantPacket : BridgePacket
{
    public override byte SubOpCode => ProtocolConstants.SubOpCodes.StarterKitGrant;

    public long FarmerId { get; set; }
    public string Profession { get; set; } = string.Empty;
    public string Season { get; set; } = "spring";
    public List<StarterKitItemEntry> Items { get; set; } = new();
    public string AuthSignature { get; set; } = string.Empty;

    public override void WritePayload(BinaryWriter writer)
    {
        writer.Write(FarmerId);
        writer.Write(Profession);
        writer.Write(Season);
        writer.Write(AuthSignature);
        writer.Write(Items.Count);
        foreach (var item in Items)
        {
            item.Write(writer);
        }
    }

    public override void ReadPayload(BinaryReader reader)
    {
        FarmerId = reader.ReadInt64();
        Profession = reader.ReadString();
        Season = reader.ReadString();
        AuthSignature = reader.ReadString();
        int count = reader.ReadInt32();
        Items = new List<StarterKitItemEntry>(count);
        for (int i = 0; i < count; i++)
        {
            Items.Add(StarterKitItemEntry.Read(reader));
        }
    }
}

public class StarterKitClaimAckPacket : BridgePacket
{
    public override byte SubOpCode => ProtocolConstants.SubOpCodes.StarterKitClaimAck;

    public long FarmerId { get; set; }
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;

    public override void WritePayload(BinaryWriter writer)
    {
        writer.Write(FarmerId);
        writer.Write(Success);
        writer.Write(Message);
    }

    public override void ReadPayload(BinaryReader reader)
    {
        FarmerId = reader.ReadInt64();
        Success = reader.ReadBoolean();
        Message = reader.ReadString();
    }
}

public class ProfessionBuffSyncPacket : BridgePacket
{
    public override byte SubOpCode => ProtocolConstants.SubOpCodes.ProfessionBuffSync;

    public long FarmerId { get; set; }
    public string Profession { get; set; } = string.Empty;
    public int SkillLevel { get; set; }
    public int Tier { get; set; } = 1; // 1, 2, 3
    public int BobberBarExtraPixels { get; set; } = 0; // +16 for Fisherman
    public float ForestSpeedBonus { get; set; } = 0f; // +0.5 for Forager Lv 5+
    public int MinesDefenseBonus { get; set; } = 0; // +3 for Miner Lv 10
    public int ToolStaminaDiscountPercent { get; set; } = 0; // 15-20%

    public override void WritePayload(BinaryWriter writer)
    {
        writer.Write(FarmerId);
        writer.Write(Profession);
        writer.Write(SkillLevel);
        writer.Write(Tier);
        writer.Write(BobberBarExtraPixels);
        writer.Write(ForestSpeedBonus);
        writer.Write(MinesDefenseBonus);
        writer.Write(ToolStaminaDiscountPercent);
    }

    public override void ReadPayload(BinaryReader reader)
    {
        FarmerId = reader.ReadInt64();
        Profession = reader.ReadString();
        SkillLevel = reader.ReadInt32();
        Tier = reader.ReadInt32();
        BobberBarExtraPixels = reader.ReadInt32();
        ForestSpeedBonus = reader.ReadSingle();
        MinesDefenseBonus = reader.ReadInt32();
        ToolStaminaDiscountPercent = reader.ReadInt32();
    }
}
