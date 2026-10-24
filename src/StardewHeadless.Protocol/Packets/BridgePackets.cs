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
    public string BridgeVersion { get; set; } = "1.0.0";

    public override void WritePayload(BinaryWriter writer)
    {
        writer.Write(ProtocolVersion);
        writer.Write(FarmerId);
        writer.Write(ClientName);
        writer.Write(BridgeVersion);
    }

    public override void ReadPayload(BinaryReader reader)
    {
        ProtocolVersion = reader.ReadInt32();
        FarmerId = reader.ReadInt64();
        ClientName = reader.ReadString();
        BridgeVersion = reader.ReadString();
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
        }
    }

    public override void ReadPayload(BinaryReader reader)
    {
        FarmerId = reader.ReadInt64();
        HwidHash = reader.ReadString();
        int count = reader.ReadInt32();
        InstalledMods = new List<ClientModInfo>(count);
        for (int i = 0; i < count; i++)
        {
            InstalledMods.Add(new ClientModInfo
            {
                ModId = reader.ReadString(),
                ModName = reader.ReadString(),
                Version = reader.ReadString(),
                HasBinary = reader.ReadBoolean()
            });
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
