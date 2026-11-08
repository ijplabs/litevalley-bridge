using StardewValley.Locations;
using System;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Monsters;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;

namespace StardewBridge.Client.Tools;

public enum SmartToolCategory
{
    None,
    Scythe,
    Axe,
    Pickaxe,
    Hoe,
    WateringCan,
    FishingRod,
    Pan,
    MilkPail,
    Shears,
    Weapon
}

/// <summary>
/// Mesin pintar pendeteksi objek 1-tile di depan pemain untuk pemilihan alat otomatis.
/// Mendukung on/off in-game via chat command, console command, dan UI badge.
/// </summary>
public static class SmartToolsEngine
{
    private static IMonitor? _monitor;

    /// <summary>
    /// Status on/off fitur SmartTools. Default aktif (true).
    /// </summary>
    public static bool IsEnabled { get; set; } = true;

    public static void Initialize(IMonitor monitor)
    {
        _monitor = monitor;
    }

    public static void SetEnabled(bool enabled, bool showNotification = true)
    {
        IsEnabled = enabled;
        _monitor?.Log($"[SmartTools] Fitur diubah menjadi: {(enabled ? "AKTIF" : "NONAKTIF")}", LogLevel.Info);

        if (showNotification)
        {
            if (enabled)
            {
                Game1.playSound("drumkit6");
                Game1.addHUDMessage(new HUDMessage(I18n.Get("hud.smarttools.enabled"), HUDMessage.newQuest_type));
            }
            else
            {
                Game1.playSound("cancel");
                Game1.addHUDMessage(new HUDMessage(I18n.Get("hud.smarttools.disabled"), HUDMessage.error_type));
            }
        }
    }

    public static void Toggle(bool showNotification = true)
    {
        SetEnabled(!IsEnabled, showNotification);
    }

    /// <summary>
    /// Menganalisis objek/sprite pada 1 tile target di depan pemain atau tile kursor.
    /// Mengembalikan kategori tool yang tepat.
    /// </summary>
    public static SmartToolCategory ResolveTargetCategory(GameLocation? location, Vector2 targetTile, Farmer? player)
    {
        if (location == null || player == null) return SmartToolCategory.None;

        Point tilePixel = new Point((int)(targetTile.X * 64 + 32), (int)(targetTile.Y * 64 + 32));

        // 1. Monster check di depan pemain -> Weapon (Pedang/Club/Dagger)
        if (location.characters != null)
        {
            foreach (var character in location.characters)
            {
                if (character is Monster monster && monster.GetBoundingBox().Contains(tilePixel))
                {
                    return SmartToolCategory.Weapon;
                }
            }
        }

        // 2. Hewan ternak (FarmAnimal) -> Milk Pail / Shears
        if (location.animals != null)
        {
            foreach (var animal in location.animals.Values)
            {
                if (animal.Tile == targetTile || animal.GetBoundingBox().Contains(tilePixel))
                {
                    string type = animal.type.Value ?? string.Empty;
                    if (type.Contains("Sheep", StringComparison.OrdinalIgnoreCase))
                    {
                        return SmartToolCategory.Shears;
                    }
                    if (type.Contains("Cow", StringComparison.OrdinalIgnoreCase) || type.Contains("Goat", StringComparison.OrdinalIgnoreCase))
                    {
                        return SmartToolCategory.MilkPail;
                    }
                }
            }
        }

        // 3. ResourceClumps (Tunggul kayu besar, bongkahan batu, meteorit)
        if (location.resourceClumps != null)
        {
            foreach (var clump in location.resourceClumps)
            {
                if (clump.getBoundingBox().Contains(tilePixel))
                {
                    int sheetIdx = clump.parentSheetIndex.Value;
                    if (sheetIdx == 600 || sheetIdx == 602)
                    {
                        return SmartToolCategory.Axe; // Stump atau Hollow Log
                    }
                    if (sheetIdx == 672 || (sheetIdx >= 752 && sheetIdx <= 758) || sheetIdx == 622)
                    {
                        return SmartToolCategory.Pickaxe; // Boulder atau Meteorite
                    }
                }
            }
        }

        // 4. Objects di atas tile (Batu, Ranting, Semak liar, Artifact spot)
        if (location.Objects != null && location.Objects.TryGetValue(targetTile, out var obj) && obj != null)
        {
            if (obj is StardewValley.Objects.Chest)
            {
                return SmartToolCategory.None;
            }
            if (obj.IsTwig())
            {
                return SmartToolCategory.Axe;
            }
            if (obj.IsBreakableStone() || (obj.Name != null && !obj.Name.Contains("Chest", StringComparison.OrdinalIgnoreCase) && obj.Name.Contains("Stone", StringComparison.OrdinalIgnoreCase)))
            {
                return SmartToolCategory.Pickaxe;
            }
            if (obj.IsWeeds())
            {
                return SmartToolCategory.Scythe;
            }
            if (obj.QualifiedItemId == "(O)590" || obj.QualifiedItemId == "(O)SeedSpot")
            {
                return SmartToolCategory.Hoe;
            }
        }

        // 5. TerrainFeatures (Rumput, Pohon, Tanah Cangkul)
        if (location.terrainFeatures != null && location.terrainFeatures.TryGetValue(targetTile, out var tf) && tf != null)
        {
            if (tf is Grass)
            {
                return SmartToolCategory.Scythe;
            }
            if (tf is Tree tree)
            {
                if (tree.tapped.Value) return SmartToolCategory.None;
                return SmartToolCategory.Axe;
            }
            if (tf is FruitTree fruitTree)
            {
                if (fruitTree.daysUntilMature.Value <= 0) return SmartToolCategory.None;
                return SmartToolCategory.Axe;
            }
            if (tf is Bush)
            {
                return SmartToolCategory.Axe;
            }
            if (tf is HoeDirt dirt)
            {
                // Tanaman mati -> Sabit
                if (dirt.crop != null && dirt.crop.dead.Value)
                {
                    return SmartToolCategory.Scythe;
                }
                // Tanaman panen sabit -> Sabit
                if (dirt.crop != null)
                {
                    var cropData = dirt.crop.GetData();
                    if (cropData != null && cropData.HarvestMethod == StardewValley.GameData.Crops.HarvestMethod.Scythe)
                    {
                        if (dirt.crop.currentPhase.Value >= dirt.crop.phaseDays.Count - 1 &&
                            (!dirt.crop.fullyGrown.Value || dirt.crop.dayOfCurrentPhase.Value <= 0))
                        {
                            return SmartToolCategory.Scythe;
                        }
                    }
                }
                // Tanaman hidup belum disiram -> Watering Can
                if (dirt.crop != null && dirt.state.Value != HoeDirt.watered)
                {
                    return SmartToolCategory.WateringCan;
                }
                // Tanah cangkul kosong belum disiram -> Watering Can (kecuali pemain sedang memegang Pickaxe untuk meratakan tanah/un-hoe)
                if (dirt.crop == null)
                {
                    if (player.CurrentTool is StardewValley.Tools.Pickaxe)
                    {
                        return SmartToolCategory.Pickaxe;
                    }
                    if (dirt.state.Value != HoeDirt.watered)
                    {
                        return SmartToolCategory.WateringCan;
                    }
                }
            }
        }

        // 6. Properti Peta & Air
        try
        {
            if (location.orePanPoint.Value != Point.Zero &&
                Vector2.Distance(targetTile, new Vector2(location.orePanPoint.Value.X, location.orePanPoint.Value.Y)) <= 1.5f)
            {
                return SmartToolCategory.Pan;
            }

            if (location.CanRefillWateringCanOnTile((int)targetTile.X, (int)targetTile.Y))
            {
                return SmartToolCategory.WateringCan;
            }

            if (location.isWaterTile((int)targetTile.X, (int)targetTile.Y))
            {
                return SmartToolCategory.FishingRod;
            }

            // Tanah kosong diggable
            if (location.doesTileHaveProperty((int)targetTile.X, (int)targetTile.Y, "Diggable", "Back") != null)
            {
                if (location is MineShaft)
                {
                    return SmartToolCategory.None;
                }
                return SmartToolCategory.Hoe;
            }
        }
        catch
        {
        }

        return SmartToolCategory.None;
    }

    /// <summary>
    /// Memeriksa apakah suatu item cocok dengan kategori tool yang dicari.
    /// </summary>
    public static bool MatchesCategory(Item? item, SmartToolCategory category)
    {
        if (item == null) return false;

        return category switch
        {
            SmartToolCategory.Scythe => item is MeleeWeapon mw && mw.isScythe(),
            SmartToolCategory.Axe => item is Axe,
            SmartToolCategory.Pickaxe => item is Pickaxe,
            SmartToolCategory.Hoe => item is Hoe,
            SmartToolCategory.WateringCan => item is WateringCan,
            SmartToolCategory.FishingRod => item is FishingRod,
            SmartToolCategory.Pan => item is Pan,
            SmartToolCategory.MilkPail => item is MilkPail,
            SmartToolCategory.Shears => item is Shears,
            SmartToolCategory.Weapon => item is MeleeWeapon weapon && !weapon.isScythe(),
            _ => false
        };
    }

    /// <summary>
    /// Mencari alat yang cocok di Toolbelt (36..45) terlebih dahulu, lalu di Tas Reguler (0..maxItems-1).
    /// Mengatur CurrentToolIndex ke alat tersebut jika ditemukan.
    /// </summary>
    public static bool AutoSelectToolForTarget(GameLocation location, Vector2 targetTile, Farmer player)
    {
        if (!IsEnabled) return false;
        if (player == null || player.UsingTool || !player.CanMove) return false;

        var category = ResolveTargetCategory(location, targetTile, player);
        if (category == SmartToolCategory.None) return false;

        // Jika pemain sudah memegang alat yang cocok, tidak perlu berganti
        if (MatchesCategory(player.CurrentItem, category))
        {
            return true;
        }

        ToolbeltManager.EnsureCapacity(player);

        // 1. Cari di 10 slot Toolbelt (36..45) terlebih dahulu
        for (int i = ToolbeltManager.ToolbeltStartIndex; i < ToolbeltManager.ToolbeltStartIndex + ToolbeltManager.ToolbeltSlotCount; i++)
        {
            if (MatchesCategory(player.Items[i], category))
            {
                player.CurrentToolIndex = i;
                _monitor?.Log($"[SmartTools] Auto-switch ke Toolbelt slot #{i - ToolbeltManager.ToolbeltStartIndex + 1} ({category})", LogLevel.Trace);
                return true;
            }
        }

        // 2. Fallback: Cari di tas reguler (0..MaxItems-1)
        for (int i = 0; i < player.maxItems.Value && i < ToolbeltManager.ToolbeltStartIndex; i++)
        {
            if (MatchesCategory(player.Items[i], category))
            {
                player.CurrentToolIndex = i;
                _monitor?.Log($"[SmartTools] Auto-switch ke Tas slot #{i + 1} ({category})", LogLevel.Trace);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Mendaftarkan patch pada Game1.pressUseToolButton untuk memilih alat sebelum animasi ayunan dimulai.
    /// </summary>
    public static void ApplyPatches(Harmony harmony)
    {
        var useToolMethod = AccessTools.Method(typeof(Game1), nameof(Game1.pressUseToolButton));
        if (useToolMethod != null)
        {
            harmony.Patch(
                useToolMethod,
                prefix: new HarmonyMethod(typeof(SmartToolsEngine), nameof(PressUseToolButton_Prefix))
            );
        }
    }

    public static void PressUseToolButton_Prefix()
    {
        if (!IsEnabled) return;
        if (Game1.fadeToBlack || Game1.activeClickableMenu != null || Game1.eventUp) return;

        var player = Game1.player;
        if (player == null || player.UsingTool || !player.CanMove) return;

        // Jika pemain sengaja memegang bibit, makanan, furnitur, dll. di tangan, jangan interupsi!
        if (player.ActiveObject != null)
        {
            return;
        }

        // Tentukan 1 tile sasaran (kursor jika dalam radius 1 tile, atau tile di depan pemain)
        Vector2 targetTile;
        Vector2 mouseWorld = new Vector2(Game1.getOldMouseX() + Game1.viewport.X, Game1.getOldMouseY() + Game1.viewport.Y);

        if (Game1.wasMouseVisibleThisFrame && Utility.withinRadiusOfPlayer((int)mouseWorld.X, (int)mouseWorld.Y, 1, player))
        {
            targetTile = new Vector2((int)(mouseWorld.X / 64f), (int)(mouseWorld.Y / 64f));
        }
        else
        {
            Vector2 toolPixel = player.GetToolLocation();
            targetTile = new Vector2((int)(toolPixel.X / 64f), (int)(toolPixel.Y / 64f));
        }

        // Auto-switch alat
        AutoSelectToolForTarget(Game1.currentLocation, targetTile, player);
    }
}
