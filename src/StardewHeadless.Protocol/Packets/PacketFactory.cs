namespace StardewHeadless.Protocol.Packets;

public static class PacketFactory
{
    public static BridgePacket? Parse(byte[] data)
    {
        if (data == null || data.Length == 0) return null;
        using var ms = new MemoryStream(data);
        using var reader = new BinaryReader(ms);
        var subOpCode = reader.ReadByte();
        return ParsePayload(subOpCode, reader);
    }

    public static BridgePacket? ParsePayload(byte subOpCode, BinaryReader reader)
    {
        BridgePacket? packet = subOpCode switch
        {
            ProtocolConstants.SubOpCodes.ClientHello => new ClientHelloPacket(),
            ProtocolConstants.SubOpCodes.ServerWelcome => new ServerWelcomePacket(),
            ProtocolConstants.SubOpCodes.ClientHeartbeat => new ClientHeartbeatPacket(),
            ProtocolConstants.SubOpCodes.RequestHostAction => new RequestHostActionPacket(),
            ProtocolConstants.SubOpCodes.HostActionResult => new HostActionResultPacket(),
            ProtocolConstants.SubOpCodes.QuorumStateBroadcast => new QuorumStatePacket(),
            ProtocolConstants.SubOpCodes.ForceClientReady => new ForceClientReadyPacket(),
            ProtocolConstants.SubOpCodes.ClientBugReportSignal => new ClientBugReportSignalPacket(),
            ProtocolConstants.SubOpCodes.ProfessionSelectRequest => new ProfessionSelectRequestPacket(),
            ProtocolConstants.SubOpCodes.ProfessionSyncResponse => new ProfessionSyncResponsePacket(),
            ProtocolConstants.SubOpCodes.VillageTreasuryUpdate => new VillageTreasuryUpdatePacket(),
            ProtocolConstants.SubOpCodes.TreasuryDepositRequest => new TreasuryDepositRequestPacket(),
            ProtocolConstants.SubOpCodes.AuthChallenge => new AuthChallengePacket(),
            ProtocolConstants.SubOpCodes.AuthSubmit => new AuthSubmitPacket(),
            ProtocolConstants.SubOpCodes.AuthSuccess => new AuthSuccessPacket(),
            ProtocolConstants.SubOpCodes.AuthReject => new AuthRejectPacket(),
            ProtocolConstants.SubOpCodes.ClientModAudit => new ClientModAuditPacket(),
            ProtocolConstants.SubOpCodes.ClientModAuditResponse => new ClientModAuditResponsePacket(),
            ProtocolConstants.SubOpCodes.SkullElevatorUseRequest => new SkullElevatorUseRequestPacket(),
            ProtocolConstants.SubOpCodes.SkullElevatorUseResponse => new SkullElevatorUseResponsePacket(),
            ProtocolConstants.SubOpCodes.StarterKitGrant => new StarterKitGrantPacket(),
            ProtocolConstants.SubOpCodes.StarterKitClaimAck => new StarterKitClaimAckPacket(),
            ProtocolConstants.SubOpCodes.ProfessionBuffSync => new ProfessionBuffSyncPacket(),
            ProtocolConstants.SubOpCodes.ClientIntegrityHeartbeat => new ClientIntegrityHeartbeatPacket(),
            ProtocolConstants.SubOpCodes.ClientIntegrityHeartbeatAck => new ClientIntegrityHeartbeatAckPacket(),
            _ => null
        };

        packet?.ReadPayload(reader);
        return packet;
    }
}
