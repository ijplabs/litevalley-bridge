namespace StardewHeadless.Protocol;

/// <summary>
/// Konstanta protokol komunikasi real-time antara Client Bridge dan Headless Server.
/// Berdasarkan spesifikasi docs/07-realtime-protocol-spec.md.
/// </summary>
public static class ProtocolConstants
{
    /// <summary>
    /// Versi protokol sinkronisasi StardewHeadless.
    /// </summary>
    public const int ProtocolVersion = 1;

    /// <summary>
    /// Default port server Lidgren untuk Stardew Valley multiplayer.
    /// </summary>
    public const int DefaultPort = 24642;

    /// <summary>
    /// Packet ID bawaan Stardew Valley untuk sinkronisasi dedicated server.
    /// </summary>
    public const byte VanillaDedicatedServerSyncPacketId = 33;

    /// <summary>
    /// Sub-opcodes di atas Packet 33 (0x80 - 0x8D) untuk bridge extensions.
    /// </summary>
    public static class SubOpCodes
    {
        public const byte ClientHello = 0x80;
        public const byte ServerWelcome = 0x81;
        public const byte ClientHeartbeat = 0x82;
        public const byte ServerTelemetry = 0x83;
        public const byte RequestHostAction = 0x84;
        public const byte HostActionResult = 0x85;
        public const byte QuorumStateBroadcast = 0x86;
        public const byte ForceClientReady = 0x87;
        public const byte NewDayBarrierProgress = 0x88;
        public const byte ReleaseEventLock = 0x89;
        public const byte AdminAuthRequest = 0x8A;
        public const byte AdminCommandRequest = 0x8B;
        public const byte AdminCommandResponse = 0x8C;
        public const byte MutexRecoverySignal = 0x8D;
        public const byte ClientBugReportSignal = 0x8E;

        // Sub-opcodes 0x90 - 0x9F untuk Sistem Profesi RPG & Ekonomi Komunal (docs/13)
        public const byte ProfessionSelectRequest = 0x90;
        public const byte ProfessionSyncResponse = 0x91;
        public const byte VillageTreasuryUpdate = 0x92;
        public const byte TreasuryDepositRequest = 0x93;

        // Sub-opcodes 0x94 - 0x97 untuk Sistem Keamanan, Lobby Gatekeeper, & Auth (docs/16)
        public const byte AuthChallenge = 0x94;
        public const byte AuthSubmit = 0x95;
        public const byte AuthSuccess = 0x96;
        public const byte AuthReject = 0x97;

        // Sub-opcodes 0x98 - 0x99 untuk Telemetri Audit Mod & Integritas Klien (docs/26)
        public const byte ClientModAudit = 0x98;
        public const byte ClientModAuditResponse = 0x99;

        // Sub-opcodes 0x9A - 0x9B untuk Starter Gift Kit v2 Otoritatif Server (Tahap 3)
        public const byte StarterKitGrant = 0x9A;
        public const byte StarterKitClaimAck = 0x9B;

        // Sub-opcodes 0x9C - 0x9D untuk Heartbeat Integritas & Anti-Cheat Berlapis (Tahap 5)
        public const byte ClientIntegrityHeartbeat = 0x9C;
        public const byte ClientIntegrityHeartbeatAck = 0x9D;

        // Sub-opcodes 0x9E - 0x9F untuk Skull Cavern Elevator Otoritatif Server (Tahap 1)
        public const byte SkullElevatorUseRequest = 0x9E;
        public const byte SkullElevatorUseResponse = 0x9F;

        // Sub-opcode 0xA0 untuk Sinkronisasi Buff & Perk Profesi RPG (Tahap 3)
        public const byte ProfessionBuffSync = 0xA0;
    }

    /// <summary>
    /// Mengecek apakah sebuah byte sub-opcode merupakan paket bridge khusus.
    /// </summary>
    public static bool IsBridgeSubOpCode(byte subOpCode) => subOpCode >= 0x80;
}
