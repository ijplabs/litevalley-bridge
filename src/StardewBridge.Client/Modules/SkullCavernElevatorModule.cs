using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewHeadless.Protocol.Packets;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Network;

namespace StardewBridge.Client.Modules;

/// <summary>
/// Modul Klien Lift Skull Cavern (Server-Authoritative).
/// Menggantikan mod SkullCavernElevator dengan validasi otoritatif server:
/// - Menyediakan interaksi lift di lantai masuk Skull Cavern (tile 4, 3).
/// - Mengirimkan permintaan via Packet 33 (SubOpCode 0x9E) ke server.
/// - Server memvalidasi rekor terdalam (deepestMineLevel), menghitung biaya (diskon miner),
///   memotong saldo, menyetor ke Kas Desa, lalu membalas via SubOpCode 0x9F.
/// - Klien melakukan teleportasi ke lantai yang dituju secara mulus.
/// </summary>
public static class SkullCavernElevatorModule
{
    public static readonly Point ElevatorTile = new(4, 3);
    private static IMonitor? _monitor;
    private static IModHelper? _helper;

    public static void Initialize(IMonitor monitor, IModHelper helper)
    {
        _monitor = monitor;
        _helper = helper;
        helper.Events.Input.ButtonPressed += OnButtonPressed;
    }

    private static void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        if (!Context.IsWorldReady || Game1.currentLocation == null || Game1.player == null) return;
        if (Game1.activeClickableMenu != null || Game1.dialogueUp || Game1.eventUp) return;

        if (e.Button.IsActionButton())
        {
            Vector2 grabTile = e.Cursor.GrabTile;
            if (IsElevatorLocation(Game1.currentLocation) && (int)grabTile.X == ElevatorTile.X && (int)grabTile.Y == ElevatorTile.Y)
            {
                _helper?.Input.Suppress(e.Button);
                OpenElevatorMenu();
            }
        }
    }

    public static bool IsElevatorLocation(GameLocation location)
    {
        if (location == null) return false;
        if (location is MineShaft shaft)
        {
            return shaft.mineLevel == 121; // Lantai 1 Skull Cavern
        }
        return location.Name.Equals("SkullCave", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Membuka dialog pilihan lantai Skull Cavern berdasarkan rekor terdalam pemain.
    /// </summary>
    public static void OpenElevatorMenu()
    {
        var player = Game1.player;
        if (player == null) return;

        int maxFloor = Math.Max(0, player.deepestMineLevel - 120);
        if (maxFloor < 5)
        {
            Game1.playSound("cancel");
            Game1.drawObjectDialogue("Lift Skull Cavern terkunci! Anda harus mencapai setidaknya Lantai 5 dengan tangga terlebih dahulu.");
            return;
        }

        int highestElevatorFloor = (maxFloor / 5) * 5;
        var options = new List<Response>();

        var targetFloors = new SortedSet<int>();
        targetFloors.Add(5);
        if (highestElevatorFloor >= 25) targetFloors.Add(25);
        if (highestElevatorFloor >= 50) targetFloors.Add(50);
        if (highestElevatorFloor >= 75) targetFloors.Add(75);
        if (highestElevatorFloor >= 100) targetFloors.Add(100);

        for (int floor = Math.Max(5, highestElevatorFloor - 20); floor <= highestElevatorFloor; floor += 5)
        {
            targetFloors.Add(floor);
        }

        foreach (int f in targetFloors)
        {
            int estimatedFee = Math.Max(250, (f / 10) * 250);
            options.Add(new Response($"floor_{f}", $"Lantai {f} (~{estimatedFee}g)"));
        }
        options.Add(new Response("cancel", "Batal"));

        Game1.currentLocation.createQuestionDialogue(
            "Pilih Lantai Skull Cavern:",
            options.ToArray(),
            (who, answer) =>
            {
                if (answer.StartsWith("floor_"))
                {
                    if (int.TryParse(answer.Substring("floor_".Length), out int targetFloor))
                    {
                        RequestElevatorWarp(targetFloor);
                    }
                }
            }
        );
    }

    /// <summary>
    /// Mengirimkan paket SkullElevatorUseRequest (0x9E) ke server.
    /// </summary>
    public static void RequestElevatorWarp(int targetFloor)
    {
        if (Game1.client == null)
        {
            _monitor?.Log("[SkullElevator] Game1.client null, tidak dapat mengirim permintaan lift.", LogLevel.Warn);
            return;
        }

        _monitor?.Log($"[SkullElevator] Mengirim permintaan lift ke Lantai {targetFloor}...", LogLevel.Info);

        var req = new SkullElevatorUseRequestPacket
        {
            FarmerId = Game1.player.UniqueMultiplayerID,
            TargetFloor = targetFloor
        };

        byte[] payload = req.Serialize();
        Game1.client.sendMessage(new OutgoingMessage(33, Game1.player.UniqueMultiplayerID, (object)payload));
    }

    /// <summary>
    /// Menangani respon dari server (0x9F).
    /// </summary>
    public static void HandleElevatorResponse(SkullElevatorUseResponsePacket resp)
    {
        if (resp == null) return;

        if (!resp.Success)
        {
            Game1.playSound("cancel");
            Game1.drawObjectDialogue(resp.Message);
            _monitor?.Log($"[SkullElevator] Permintaan lift ditolak: {resp.Message}", LogLevel.Warn);
            return;
        }

        try
        {
            Game1.playSound("mineElevator");
        }
        catch
        {
        }

        Game1.addHUDMessage(new HUDMessage(resp.Message, HUDMessage.newQuest_type));
        _monitor?.Log($"[SkullElevator] Berhasil menuju Lantai {resp.TargetFloor} (Biaya terpotong: {resp.FeeDeducted}g).", LogLevel.Info);

        // Teleportasi ke lantai tambang Skull Cavern (120 + targetFloor)
        int targetMineLevel = 120 + resp.TargetFloor;
        Game1.enterMine(targetMineLevel);
    }
}
