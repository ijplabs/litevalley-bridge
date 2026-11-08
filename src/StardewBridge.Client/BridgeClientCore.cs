using StardewBridge.Client.Modules;
using System.IO;
using System.Text.Json;
using StardewHeadless.Protocol;
using StardewHeadless.Protocol.Models;
using StardewHeadless.Protocol.Packets;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Network;

namespace StardewBridge.Client;

public static class BridgeClientCore
{
    public static bool IsConnectedToHeadless { get; private set; }
    public static ServerWelcomePacket? LastServerWelcome { get; private set; }
    public static bool HandshakeSent { get; private set; }
    public static bool IsAuthenticated { get; private set; }
    public static AuthChallengePacket? LastAuthChallenge { get; private set; }
    public static string SessionToken { get; private set; } = string.Empty;
    public static string CurrentProfession { get; private set; } = "Unassigned";
    public static int VillageTreasury { get; private set; } = 0;
    public static int AssignedCabinIndex { get; private set; } = 0;
    public static IMonitor? Monitor => _monitor;

    private static IMonitor? _monitor;
    private static IModHelper? _helper;
    private static string _logsDir = string.Empty;

    public static void Initialize(IMonitor monitor, IModHelper helper)
    {
        _monitor = monitor;
        _helper = helper;
        Professions.ClientProfessionBuffHandler.Initialize(monitor);

        // Cari atau buat folder logs/ di root workspace proyek
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var parentDir = Directory.GetParent(baseDir)?.FullName ?? baseDir;
        _logsDir = Path.Combine(Directory.GetCurrentDirectory(), "logs");
        if (!Directory.Exists(_logsDir))
        {
            Directory.CreateDirectory(_logsDir);
        }
    }

    private static int _heartbeatTickCounter = 0;

    public static void OnUpdateTicked()
    {
        if (Game1.client != null && Game1.client.readyToPlay)
        {
            if (!HandshakeSent)
            {
                SendHandshake();
            }
            else if (IsConnectedToHeadless)
            {
                _heartbeatTickCounter++;
                if (_heartbeatTickCounter >= 1800) // Setiap 30 detik (60 ticks/detik)
                {
                    _heartbeatTickCounter = 0;
                    SendIntegrityHeartbeat();
                }
            }
        }
        else
        {
            HandshakeSent = false;
            IsConnectedToHeadless = false;
            _heartbeatTickCounter = 0;
        }
    }

    public static void SendIntegrityHeartbeat()
    {
        if (Game1.client == null || Game1.player == null) return;

        try
        {
            long farmerId = Game1.player.UniqueMultiplayerID;
            long timestamp = DateTime.UtcNow.Ticks;
            string nonce = Guid.NewGuid().ToString("N")[..8];
            string signature = ClientIntegrityHeartbeatPacket.ComputeSignature(farmerId, timestamp, nonce);
            var patches = Security.ClientIntegrityAttestor.ScanSensitiveHarmonyPatches();

            var packet = new ClientIntegrityHeartbeatPacket
            {
                FarmerId = farmerId,
                TimestampTicks = timestamp,
                Nonce = nonce,
                Signature = signature,
                DetectedPatches = patches
            };

            byte[] payload = packet.Serialize();
            Game1.client.sendMessage(new OutgoingMessage(33, farmerId, (object)payload));
        }
        catch (Exception ex)
        {
            _monitor?.Log($"[StardewBridge::AntiCheat] Gagal mengirim integrity heartbeat 0x9C: {ex.Message}", LogLevel.Trace);
        }
    }

    public static void SendHandshake()
    {
        if (Game1.client == null) return;

        try
        {
            _monitor?.Log("[StardewBridge] Mengirim ClientHello handshake ke server via Packet 33...", LogLevel.Info);

            var hello = new ClientHelloPacket
            {
                ProtocolVersion = ProtocolConstants.ProtocolVersion,
                FarmerId = Game1.player.UniqueMultiplayerID,
                ClientName = Game1.player.Name,
                BridgeVersion = "0.1.0",
                SessionToken = SessionToken
            };

            byte[] payload = hello.Serialize();
            Game1.client.sendMessage(new OutgoingMessage(33, Game1.player.UniqueMultiplayerID, (object)payload));
            HandshakeSent = true;

            // Kirimkan telemetri manifest mod klien untuk audit integritas dan anti-cheat
            SendModAudit();
        }
        catch (Exception ex)
        {
            _monitor?.Log($"[StardewBridge] Gagal mengirim handshake: {ex.Message}", LogLevel.Error);
        }
    }

    public static string GenerateHwidHash()
    {
        try
        {
            string machineData = Environment.MachineName + "_" + Environment.UserName + "_" + Environment.OSVersion.VersionString;
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            byte[] hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(machineData));
            return Convert.ToHexString(hash);
        }
        catch
        {
            return "UNKNOWN_HWID";
        }
    }

    public static void SendModAudit()
    {
        if (Game1.client == null || _helper == null) return;

        try
        {
            var mods = new List<ClientModInfo>();
            string? modsDir = Directory.GetParent(_helper.DirectoryPath)?.FullName;

            foreach (var mod in _helper.ModRegistry.GetAll())
            {
                if (mod.Manifest == null) continue;

                string dllSha = string.Empty;
                if (!string.IsNullOrEmpty(mod.Manifest.EntryDll))
                {
                    try
                    {
                        if (mod.Manifest.UniqueID.Equals("IJP.StardewBridge", StringComparison.OrdinalIgnoreCase))
                        {
                            string ownDll = Path.Combine(_helper.DirectoryPath, mod.Manifest.EntryDll);
                            if (File.Exists(ownDll))
                            {
                                dllSha = Security.ClientIntegrityAttestor.ComputeFileSha256(ownDll);
                            }
                        }
                        else if (!string.IsNullOrEmpty(modsDir) && Directory.Exists(modsDir))
                        {
                            var files = Directory.GetFiles(modsDir, mod.Manifest.EntryDll, SearchOption.AllDirectories);
                            if (files.Length > 0)
                            {
                                dllSha = Security.ClientIntegrityAttestor.ComputeFileSha256(files[0]);
                            }
                        }
                    }
                    catch { }
                }

                mods.Add(new ClientModInfo
                {
                    ModId = mod.Manifest.UniqueID,
                    ModName = mod.Manifest.Name,
                    Version = mod.Manifest.Version?.ToString() ?? "1.0.0",
                    HasBinary = !string.IsNullOrEmpty(mod.Manifest.EntryDll),
                    DllSha256 = dllSha
                });
            }

            var auditPacket = new ClientModAuditPacket
            {
                FarmerId = Game1.player.UniqueMultiplayerID,
                HwidHash = GenerateHwidHash(),
                InstalledMods = mods
            };

            byte[] payload = auditPacket.Serialize();
            Game1.client.sendMessage(new OutgoingMessage(33, Game1.player.UniqueMultiplayerID, (object)payload));
            _monitor?.Log($"[StardewBridge::AntiCheat] Mengirim telemetri manifest {mods.Count} mod ke server...", LogLevel.Info);
        }
        catch (Exception ex)
        {
            _monitor?.Log($"[StardewBridge::AntiCheat] Gagal mengirim audit mod: {ex.Message}", LogLevel.Error);
        }
    }

    public static void HandleServerPacket(IncomingMessage msg, byte subOpCode)
    {
        try
        {
            var packet = PacketFactory.ParsePayload(subOpCode, msg.Reader);
            if (packet == null)
            {
                _monitor?.Log($"[StardewBridge] Menerima sub-opcode tidak dikenal: 0x{subOpCode:X2}", LogLevel.Warn);
                return;
            }

            switch (packet)
            {
                case ServerWelcomePacket welcome:
                    LastServerWelcome = welcome;
                    IsConnectedToHeadless = true;
                    _monitor?.Log($"[StardewBridge] Handshake SUKSES! Terhubung ke '{welcome.ServerName}', Tahun {welcome.Year} {welcome.Season} Hari {welcome.DayOfMonth}, Jam {welcome.TimeOfDay}", LogLevel.Info);
                    Game1.addHUDMessage(new HUDMessage(I18n.Get("hud.bridge.connected", new { server = welcome.ServerName }), HUDMessage.newQuest_type));
                    break;

                case ProfessionSyncResponsePacket profResp:
                    CurrentProfession = profResp.ConfirmedProfession;
                    VillageTreasury = profResp.VillageBankBalance;
                    _monitor?.Log($"[StardewBridge::Economy] {profResp.Message} (Kas Desa: {VillageTreasury}g)", LogLevel.Info);
                    Game1.addHUDMessage(new HUDMessage(I18n.Get("hud.profession.assigned", new { profession = profResp.ConfirmedProfession }), HUDMessage.newQuest_type));
                    break;

                case VillageTreasuryUpdatePacket treasuryUpdate:
                    VillageTreasury = treasuryUpdate.NewBalance;
                    _monitor?.Log($"[StardewBridge::Economy] Kas Desa diperbarui: {VillageTreasury}g (+{treasuryUpdate.LastContributionAmount}g dari {treasuryUpdate.LastContributorName})", LogLevel.Info);
                    Game1.addHUDMessage(new HUDMessage(I18n.Get("hud.treasury.updated", new { total = VillageTreasury, amount = treasuryUpdate.LastContributionAmount, contributor = treasuryUpdate.LastContributorName }), HUDMessage.achievement_type));
                    break;

                case AuthChallengePacket challenge:
                    LastAuthChallenge = challenge;
                    _monitor?.Log($"[StardewBridge::Security] Server meminta autentikasi ({challenge.ServerName}): {challenge.Message}", LogLevel.Info);
                    _monitor?.Log("[StardewBridge::Security] Ketik 'login <password>' jika akun lama, atau 'register <password> <farmer|miner|fisherman|rancher|forager>' jika pemain baru.", LogLevel.Info);
                    Game1.addHUDMessage(new HUDMessage(I18n.Get("hud.auth.prompt"), HUDMessage.error_type));
                    break;

                case AuthSuccessPacket success:
                    IsAuthenticated = true;
                    SessionToken = success.SessionToken;
                    CurrentProfession = success.LockedProfession;
                    AssignedCabinIndex = success.AssignedCabinIndex;
                    _monitor?.Log($"[StardewBridge::Security] Autentikasi SUKSES! {success.WelcomeMessage}", LogLevel.Info);
                    Game1.addHUDMessage(new HUDMessage(I18n.Get("hud.auth.success", new { cabin = success.AssignedCabinIndex }), HUDMessage.newQuest_type));
                    break;

                case AuthRejectPacket reject:
                    _monitor?.Log($"[StardewBridge::Security] Autentikasi DITOLAK (Kode {reject.ReasonCode}): {reject.ErrorMessage}", LogLevel.Warn);
                    Game1.addHUDMessage(new HUDMessage(I18n.Get("hud.auth.rejected", new { reason = reject.ErrorMessage }), HUDMessage.error_type));
                    break;

                case ClientModAuditResponsePacket auditResp:
                    if (!auditResp.IsAllowed)
                    {
                        _monitor?.Log($"[StardewBridge::AntiCheat] DITOLAK OLEH SERVER: {auditResp.RejectionReason}", LogLevel.Error);
                        Game1.addHUDMessage(new HUDMessage(I18n.Get("hud.auth.kicked", new { reason = auditResp.RejectionReason }), HUDMessage.error_type));
                    }
                    else
                    {
                        _monitor?.Log("[StardewBridge::AntiCheat] Integritas mod klien diverifikasi oleh server. Aman!", LogLevel.Info);
                    }
                    break;

                case SkullElevatorUseResponsePacket elevatorResp:
                    SkullCavernElevatorModule.HandleElevatorResponse(elevatorResp);
                    break;

                case StarterKitGrantPacket starterGrant:
                    Professions.ClientProfessionBuffHandler.HandleStarterKitGrant(starterGrant);
                    break;

                case ProfessionBuffSyncPacket buffSync:
                    Professions.ClientProfessionBuffHandler.HandleBuffSync(buffSync);
                    break;

                default:
                    _monitor?.Log($"[StardewBridge] Menerima paket server {packet.GetType().Name} (0x{subOpCode:X2})", LogLevel.Trace);
                    break;
            }
        }
        catch (Exception ex)
        {
            _monitor?.Log($"[StardewBridge] Exception saat memproses paket server 0x{subOpCode:X2}: {ex.Message}", LogLevel.Error);
        }
    }

    public static void CaptureBugReport(string userNote)
    {
        try
        {
            var player = Game1.player;
            var loc = player?.currentLocation?.NameOrUniqueName ?? "Unknown";
            var tileX = player?.TilePoint.X ?? 0;
            var tileY = player?.TilePoint.Y ?? 0;
            var activeMenu = Game1.activeClickableMenu?.GetType().FullName;

            var otherFarmersList = new List<OtherFarmerBugInfo>();
            if (Game1.otherFarmers != null)
            {
                foreach (var other in Game1.otherFarmers.Values)
                {
                    if (other != null)
                    {
                        otherFarmersList.Add(new OtherFarmerBugInfo
                        {
                            FarmerId = other.UniqueMultiplayerID,
                            Name = other.Name,
                            Location = other.currentLocation?.NameOrUniqueName ?? "None",
                            TilePosition = new TileVector
                            {
                                X = other.TilePoint.X,
                                Y = other.TilePoint.Y
                            },
                            FacingDirection = other.FacingDirection,
                            IsSleeping = other.isInBed?.Value ?? false
                        });
                    }
                }
            }

            var report = new BugReport
            {
                UserNote = userNote,
                Reporter = new BugReporterInfo
                {
                    FarmerId = player?.UniqueMultiplayerID ?? 0,
                    Name = player?.Name ?? "Player",
                    PingMs = (int)(Game1.client?.GetPingToHost() ?? 0)
                },
                ClientState = new ClientBugState
                {
                    Location = loc,
                    TilePosition = new TileVector { X = tileX, Y = tileY },
                    FacingDirection = player?.FacingDirection ?? 0,
                    CanMove = player?.CanMove ?? false,
                    ActiveMenu = activeMenu,
                    OtherFarmers = otherFarmersList
                },
                ServerState = new ServerBugState
                {
                    WorldTime = Game1.timeOfDay,
                    TicksElapsed = Game1.ticks,
                    SleepReadyCount = player?.team?.announcedSleepingFarmers?.Count ?? 0,
                    SleepTotalCount = (Game1.otherFarmers?.Count ?? 0) + 1
                }
            };

            var reportPath = Path.Combine(_logsDir, "bug-reports.jsonl");
            var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = false });

            bool written = false;
            for (int attempt = 0; attempt < 10 && !written; attempt++)
            {
                try
                {
                    using (var stream = new FileStream(reportPath, FileMode.Append, FileAccess.Write, FileShare.Read))
                    using (var writer = new StreamWriter(stream, System.Text.Encoding.UTF8))
                    {
                        writer.WriteLine(json);
                        writer.Flush();
                    }
                    written = true;
                }
                catch (IOException)
                {
                    Thread.Sleep(25);
                }
            }

            // Jika terhubung ke server, kirimkan juga sinyal bug report via Packet 33
            if (IsConnectedToHeadless && Game1.client != null && player != null)
            {
                var signal = new ClientBugReportSignalPacket
                {
                    UserNote = userNote,
                    ClientLocation = loc,
                    TileX = tileX,
                    TileY = tileY,
                    ActiveMenu = activeMenu ?? string.Empty
                };
                byte[] payload = signal.Serialize();
                Game1.client.sendMessage(new OutgoingMessage(33, player.UniqueMultiplayerID, (object)payload));
            }

            _monitor?.Log($"[StardewBridge] Laporan bug tersimpan ke {reportPath}: '{userNote}'", LogLevel.Info);
            Game1.addHUDMessage(new HUDMessage(I18n.Get("hud.bug.reported", new { note = userNote }), HUDMessage.error_type));
        }
        catch (Exception ex)
        {
            _monitor?.Log($"[StardewBridge] Gagal mencatat bug report: {ex.Message}", LogLevel.Error);
        }
    }

    public static void SelectProfession(string professionName)
    {
        if (Game1.client == null || Game1.player == null)
        {
            _monitor?.Log("[StardewBridge] Anda harus berada di dalam game untuk memilih profesi.", LogLevel.Warn);
            return;
        }

        var req = new ProfessionSelectRequestPacket
        {
            FarmerId = Game1.player.UniqueMultiplayerID,
            ProfessionName = professionName
        };
        byte[] payload = req.Serialize();
        Game1.client.sendMessage(new OutgoingMessage(33, Game1.player.UniqueMultiplayerID, (object)payload));
        _monitor?.Log($"[StardewBridge] Mengirim pendaftaran profesi '{professionName}' ke server via Packet 33...", LogLevel.Info);
    }

    public static void DonateToTreasury(int amount)
    {
        if (Game1.client == null || Game1.player == null)
        {
            _monitor?.Log("[StardewBridge] Anda harus berada di dalam game untuk menyumbang.", LogLevel.Warn);
            return;
        }

        if (Game1.player.Money < amount)
        {
            _monitor?.Log($"[StardewBridge] Uang tidak cukup. Saldo Anda: {Game1.player.Money}g, jumlah donasi: {amount}g", LogLevel.Warn);
            return;
        }

        Game1.player.Money -= amount;
        var req = new TreasuryDepositRequestPacket
        {
            FarmerId = Game1.player.UniqueMultiplayerID,
            Amount = amount
        };
        byte[] payload = req.Serialize();
        Game1.client.sendMessage(new OutgoingMessage(33, Game1.player.UniqueMultiplayerID, (object)payload));
        _monitor?.Log($"[StardewBridge] Mendonasikan {amount}g dari dompet pribadi ke Kas Desa.", LogLevel.Info);
    }

    public static void SubmitAuth(string password, string? chosenProfession = null)
    {
        if (Game1.client == null || Game1.player == null)
        {
            _monitor?.Log("[StardewBridge] Anda harus terhubung ke server untuk melakukan autentikasi.", LogLevel.Warn);
            return;
        }

        var packet = new AuthSubmitPacket
        {
            FarmerId = Game1.player.UniqueMultiplayerID,
            Username = Game1.player.Name,
            PasswordHash = password, // Server akan meng-hash dengan salt yang tersimpan di SQLite
            ChosenProfession = chosenProfession ?? string.Empty
        };

        byte[] payload = packet.Serialize();
        Game1.client.sendMessage(new OutgoingMessage(33, Game1.player.UniqueMultiplayerID, (object)payload));
        _monitor?.Log($"[StardewBridge] Mengirim autentikasi akun '{Game1.player.Name}' ke server via Packet 33...", LogLevel.Info);
    }

    public static void SendBridgePacket(BridgePacket packet)
    {
        if (Game1.client == null || Game1.player == null || packet == null) return;
        byte[] payload = packet.Serialize();
        Game1.client.sendMessage(new OutgoingMessage(33, Game1.player.UniqueMultiplayerID, (object)payload));
    }

    public static void ResetConnection()
    {
        IsConnectedToHeadless = false;
        HandshakeSent = false;
        IsAuthenticated = false;
        LastServerWelcome = null;
        LastAuthChallenge = null;
        SessionToken = string.Empty;
        CurrentProfession = "Unassigned";
        VillageTreasury = 0;
        AssignedCabinIndex = 0;
    }

}