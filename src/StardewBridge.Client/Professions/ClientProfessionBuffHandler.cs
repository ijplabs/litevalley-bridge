using System;
using HarmonyLib;
using StardewHeadless.Protocol.Packets;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley.Tools;

namespace StardewBridge.Client.Professions;

/// <summary>
/// Menangani paket buff profesi 0xA0, paket starter kit 0x9A, dan patch klien (BobberBar +16px, Forest Speed, Defense, Tool Stamina).
/// </summary>
public static class ClientProfessionBuffHandler
{
    private static IMonitor? _monitor;

    public static string ConfirmedProfession { get; private set; } = string.Empty;
    public static int SkillLevel { get; private set; } = 0;
    public static int Tier { get; private set; } = 1;
    public static int BobberBarExtraPixels { get; private set; } = 0;
    public static float ForestSpeedBonus { get; private set; } = 0f;
    public static int MinesDefenseBonus { get; private set; } = 0;
    public static int ToolStaminaDiscountPercent { get; private set; } = 0;

    public static void Initialize(IMonitor? monitor)
    {
        _monitor = monitor;
    }

    public static void ApplyPatches(Harmony harmony)
    {
        try
        {
            // 1. BobberBar +16px untuk Fisherman
            var bobberBarCtor = AccessTools.Constructor(typeof(BobberBar), new[]
            {
                typeof(string), typeof(float), typeof(bool), typeof(List<string>),
                typeof(string), typeof(bool), typeof(string), typeof(bool)
            }) ?? AccessTools.Constructor(typeof(BobberBar), new[]
            {
                typeof(string), typeof(float), typeof(bool), typeof(int),
                typeof(bool), typeof(string), typeof(int)
            });
            if (bobberBarCtor != null)
            {
                harmony.Patch(
                    original: bobberBarCtor,
                    postfix: new HarmonyMethod(typeof(ClientProfessionBuffHandler), nameof(BobberBar_Postfix))
                );
            }

            // 2. Bonus kecepatan di Hutan untuk Forager Tier 2+
            var speedMethod = AccessTools.Method(typeof(Farmer), nameof(Farmer.getMovementSpeed));
            if (speedMethod != null)
            {
                harmony.Patch(
                    original: speedMethod,
                    postfix: new HarmonyMethod(typeof(ClientProfessionBuffHandler), nameof(Farmer_getMovementSpeed_Postfix))
                );
            }

            // 3. Bonus Pertahanan Tambang (+3 Defense) untuk Miner Tier 3
            var damageMethod = AccessTools.Method(typeof(Farmer), nameof(Farmer.takeDamage));
            if (damageMethod != null)
            {
                harmony.Patch(
                    original: damageMethod,
                    prefix: new HarmonyMethod(typeof(ClientProfessionBuffHandler), nameof(Farmer_takeDamage_Prefix))
                );
            }

            // 4. Diskon Penggunaan Stamina Alat untuk perk profesi
            var staminaSetter = AccessTools.PropertySetter(typeof(Farmer), nameof(Farmer.Stamina));
            if (staminaSetter != null)
            {
                harmony.Patch(
                    original: staminaSetter,
                    prefix: new HarmonyMethod(typeof(ClientProfessionBuffHandler), nameof(Farmer_set_Stamina_Prefix))
                );
            }

            _monitor?.Log("[ClientProfessionBuffHandler] Harmony patches untuk BobberBar, Speed, Defense, dan Tool Stamina berhasil dipasang.", LogLevel.Info);
        }
        catch (Exception ex)
        {
            _monitor?.Log($"[ClientProfessionBuffHandler] Gagal memasang Harmony patches profesi: {ex.Message}", LogLevel.Warn);
        }
    }

    public static void HandleBuffSync(ProfessionBuffSyncPacket packet)
    {
        if (packet == null) return;

        ConfirmedProfession = packet.Profession;
        SkillLevel = packet.SkillLevel;
        Tier = packet.Tier;
        BobberBarExtraPixels = packet.BobberBarExtraPixels;
        ForestSpeedBonus = packet.ForestSpeedBonus;
        MinesDefenseBonus = packet.MinesDefenseBonus;
        ToolStaminaDiscountPercent = packet.ToolStaminaDiscountPercent;

        _monitor?.Log($"[ClientProfessionBuffHandler] Buff diperbarui: Profesi={ConfirmedProfession}, Tier={Tier}, BobberBarExtra={BobberBarExtraPixels}px, ForestSpeed=+{ForestSpeedBonus}, Defense=+{MinesDefenseBonus}", LogLevel.Info);
    }

    public static void HandleStarterKitGrant(StarterKitGrantPacket packet)
    {
        if (packet == null || Game1.player == null) return;

        _monitor?.Log($"[ClientProfessionBuffHandler] Menerima paket Starter Kit {packet.Profession.ToUpper()} (Musim: {packet.Season}) dengan {packet.Items.Count} item...", LogLevel.Info);

        try
        {
            foreach (var itemEntry in packet.Items)
            {
                Item? createdItem = null;

                if (itemEntry.ItemType == "(T)")
                {
                    createdItem = itemEntry.ItemId switch
                    {
                        "Pickaxe" => new Pickaxe { UpgradeLevel = itemEntry.UpgradeLevel },
                        "Axe" => new Axe { UpgradeLevel = itemEntry.UpgradeLevel },
                        "FishingRod" => new FishingRod(itemEntry.UpgradeLevel),
                        "MilkPail" => new MilkPail(),
                        "Shears" => new Shears(),
                        _ => null
                    };
                }
                else
                {
                    createdItem = ItemRegistry.Create($"{itemEntry.ItemType}{itemEntry.ItemId}", itemEntry.Stack, itemEntry.Quality);
                }

                if (createdItem == null) continue;

                // Jika target toolbelt dan player memiliki slot 48..57
                bool placedInToolbelt = false;
                if (itemEntry.TargetToolbelt && Game1.player.Items != null && Game1.player.Items.Count >= 58)
                {
                    for (int i = 48; i < 58; i++)
                    {
                        if (Game1.player.Items[i] == null)
                        {
                            Game1.player.Items[i] = createdItem;
                            placedInToolbelt = true;
                            break;
                        }
                    }
                }

                if (!placedInToolbelt)
                {
                    Game1.player.addItemToInventoryBool(createdItem);
                }
            }

            try
            {
                Game1.playSound("achievement");
            }
            catch
            {
                // Fallback jika audio off
            }

            Game1.addHUDMessage(new HUDMessage($"Paket Peralatan Starter Kit {packet.Profession.ToUpper()} diterima!", HUDMessage.achievement_type));

            // Kirim ACK 0x9B kembali ke server
            var ack = new StarterKitClaimAckPacket
            {
                FarmerId = Game1.player.UniqueMultiplayerID,
                Success = true,
                Message = $"Starter Kit {packet.Profession} berhasil ditempatkan di inventori."
            };
            BridgeClientCore.SendBridgePacket(ack);
        }
        catch (Exception ex)
        {
            _monitor?.Log($"[ClientProfessionBuffHandler] Gagal memproses Starter Kit Grant: {ex.Message}", LogLevel.Error);
        }
    }

    #region Harmony Patch Implementations

    public static void BobberBar_Postfix(BobberBar __instance, ref int ___bobberBarHeight)
    {
        if (BobberBarExtraPixels > 0)
        {
            ___bobberBarHeight += BobberBarExtraPixels;
        }
    }

    public static void Farmer_getMovementSpeed_Postfix(Farmer __instance, ref float __result)
    {
        if (ForestSpeedBonus > 0 && __instance != null && __instance.IsLocalPlayer)
        {
            var loc = Game1.currentLocation;
            if (loc != null && (loc.IsOutdoors && (loc.Name.Contains("Forest", StringComparison.OrdinalIgnoreCase) || loc.Name.Contains("Woods", StringComparison.OrdinalIgnoreCase))))
            {
                __result += ForestSpeedBonus;
            }
        }
    }

    public static void Farmer_takeDamage_Prefix(Farmer __instance, ref int damage)
    {
        if (MinesDefenseBonus > 0 && __instance != null && __instance.IsLocalPlayer && damage > 0)
        {
            var loc = Game1.currentLocation;
            if (loc is MineShaft || (loc != null && (loc.Name.Contains("Mine", StringComparison.OrdinalIgnoreCase) || loc.Name.Contains("Volcano", StringComparison.OrdinalIgnoreCase))))
            {
                damage = Math.Max(1, damage - MinesDefenseBonus);
            }
        }
    }

    public static void Farmer_set_Stamina_Prefix(Farmer __instance, ref float value)
    {
        if (ToolStaminaDiscountPercent > 0 && __instance != null && __instance.IsLocalPlayer && __instance.UsingTool && value < __instance.stamina)
        {
            float drain = __instance.stamina - value;
            float discountedDrain = drain * (1f - (Math.Clamp(ToolStaminaDiscountPercent, 0, 90) / 100f));
            value = __instance.stamina - discountedDrain;
        }
    }

    #endregion
    public static void ResetBuffs()
    {
        ConfirmedProfession = string.Empty;
        SkillLevel = 0;
        Tier = 1;
        BobberBarExtraPixels = 0;
        ForestSpeedBonus = 0f;
        MinesDefenseBonus = 0;
        ToolStaminaDiscountPercent = 0;
    }

}