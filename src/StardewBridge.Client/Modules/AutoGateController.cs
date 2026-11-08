using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace StardewBridge.Client.Modules;

/// <summary>
/// Kontroler Native Pintu Pagar Otomatis (AutoGateController).
/// Menggantikan mod Rakiin.AutomaticGates secara native:
/// - Membuka pintu pagar secara otomatis saat pemain mendekat (radius <= 1.8 tile).
/// - Menutup pintu pagar secara otomatis saat pemain menjauh (radius > 2.2 tile dengan histeresis anti-chatter).
/// - Dieksekusi secara sinkron di main game loop tanpa `async void` untuk mencegah race condition.
/// </summary>
public static class AutoGateController
{
    public const float OpenProximityThreshold = 1.8f;
    public const float CloseProximityThreshold = 2.2f;

    private static IMonitor? _monitor;
    private static readonly HashSet<Vector2> _trackedOpenGates = new();
    public static bool IsEnabled { get; set; } = true;

    public static void Initialize(IMonitor monitor, IModHelper helper)
    {
        _monitor = monitor;
        helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        helper.Events.Player.Warped += OnPlayerWarped;
    }

    private static void OnPlayerWarped(object? sender, WarpedEventArgs e)
    {
        _trackedOpenGates.Clear();
    }

    public static void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        if (!IsEnabled || !Context.IsWorldReady || Game1.currentLocation == null || Game1.player == null)
        {
            return;
        }

        // Jalankan pengecekan setiap 6 tick (~10 kali per detik) untuk performa optimal
        if (e.Ticks % 6 != 0)
        {
            return;
        }

        if (Game1.eventUp || Game1.dialogueUp || Game1.activeClickableMenu != null)
        {
            return;
        }

        UpdateProximityGates(Game1.player, Game1.currentLocation);
    }

    /// <summary>
    /// Memeriksa pagar dan mengoperasikan buka/tutup secara deterministik.
    /// </summary>
    public static void UpdateProximityGates(Farmer player, GameLocation location)
    {
        if (player == null || location == null) return;

        Vector2 playerPos = player.Tile;

        // 1. Periksa pagar di sekitar pemain untuk auto-open
        int minTileX = (int)Math.Floor(playerPos.X - 2);
        int maxTileX = (int)Math.Ceiling(playerPos.X + 2);
        int minTileY = (int)Math.Floor(playerPos.Y - 2);
        int maxTileY = (int)Math.Ceiling(playerPos.Y + 2);

        for (int x = minTileX; x <= maxTileX; x++)
        {
            for (int y = minTileY; y <= maxTileY; y++)
            {
                var tile = new Vector2(x, y);
                if (location.Objects.TryGetValue(tile, out var obj) && obj is Fence fence && fence.isGate.Value)
                {
                    float dist = Vector2.Distance(playerPos, tile);
                    if (dist <= OpenProximityThreshold)
                    {
                        // Jika pintu masih tertutup (gatePosition == 0)
                        if (fence.gatePosition.Value == 0)
                        {
                            fence.toggleGate(true, false, player);
                            _trackedOpenGates.Add(tile);
                            _monitor?.Log($"[AutoGate] Pintu pagar terbuka otomatis di ({tile.X}, {tile.Y})", LogLevel.Trace);
                        }
                        else
                        {
                            _trackedOpenGates.Add(tile);
                        }
                    }
                }
            }
        }

        // 2. Periksa pintu yang sebelumnya dibuka untuk auto-close jika pemain sudah menjauh
        if (_trackedOpenGates.Count > 0)
        {
            var gatesToClose = new List<Vector2>();

            foreach (var gateTile in _trackedOpenGates)
            {
                float dist = Vector2.Distance(playerPos, gateTile);
                if (dist > CloseProximityThreshold)
                {
                    gatesToClose.Add(gateTile);
                }
            }

            foreach (var gateTile in gatesToClose)
            {
                if (location.Objects.TryGetValue(gateTile, out var obj) && obj is Fence fence && fence.isGate.Value)
                {
                    if (fence.gatePosition.Value != 0)
                    {
                        fence.toggleGate(false, false, player);
                        _monitor?.Log($"[AutoGate] Pintu pagar tertutup otomatis di ({gateTile.X}, {gateTile.Y})", LogLevel.Trace);
                    }
                }
                _trackedOpenGates.Remove(gateTile);
            }
        }
    }
}
