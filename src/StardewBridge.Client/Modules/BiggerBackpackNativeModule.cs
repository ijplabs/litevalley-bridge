using System;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewBridge.Client.Tools;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;

namespace StardewBridge.Client.Modules;

/// <summary>
/// Modul Native BiggerBackpack (48-Slot Bag).
/// Menggantikan mod eksternal spacechase0.BiggerBackpack secara native:
/// - Menyediakan upgrade Tas Punggung Premium (48 slot) seharga 50.000g di Toko Pierre saat maxItems == 36.
/// - Menyesuaikan InventoryMenu menjadi 4 baris x 12 slot (kapasitas 48) secara presisi tanpa distorsi UI.
/// - Terintegrasi penuh dengan 10 slot Dedicated Vertical Toolbelt (Total 58 slot).
/// </summary>
public static class BiggerBackpackNativeModule
{
    public const int PremiumBackpackCapacity = 48;
    public const int PremiumBackpackPrice = 50000;
    public const int RequiredPreviousCapacity = 36;

    private static IMonitor? _monitor;

    public static void Initialize(IMonitor monitor)
    {
        _monitor = monitor;
    }

    public static void ApplyPatches(Harmony harmony)
    {
        // 1. Intersepsi constructor InventoryMenu untuk mengalokasikan 4 baris x 12 kolom saat maxItems >= 48
        var invConstructor = AccessTools.Constructor(
            typeof(InventoryMenu),
            new[]
            {
                typeof(int), typeof(int), typeof(bool), typeof(System.Collections.Generic.IList<Item>),
                typeof(InventoryMenu.highlightThisItem), typeof(int), typeof(int),
                typeof(int), typeof(int), typeof(bool)
            }
        );

        if (invConstructor != null)
        {
            harmony.Patch(
                invConstructor,
                prefix: new HarmonyMethod(typeof(BiggerBackpackNativeModule), nameof(InventoryMenu_Constructor_Prefix))
            );
        }

        // 2. Intersepsi performAction di GameLocation untuk pembelian tas 48 slot di SeedShop
        var performActionMethod = AccessTools.Method(
            typeof(GameLocation),
            nameof(GameLocation.performAction),
            new[] { typeof(string[]), typeof(Farmer), typeof(xTile.Dimensions.Location) }
        );

        if (performActionMethod != null)
        {
            harmony.Patch(
                performActionMethod,
                prefix: new HarmonyMethod(typeof(BiggerBackpackNativeModule), nameof(GameLocation_PerformAction_Prefix))
            );
        }

        // 3. Intersepsi constructor InventoryPage untuk menggeser elemen bawah (+64px) saat tas 48 slot
        var invPageConstructor = AccessTools.Constructor(
            typeof(InventoryPage),
            new[] { typeof(int), typeof(int), typeof(int), typeof(int) }
        );

        if (invPageConstructor != null)
        {
            harmony.Patch(
                invPageConstructor,
                postfix: new HarmonyMethod(typeof(BiggerBackpackNativeModule), nameof(InventoryPage_Constructor_Postfix))
            );
        }
    }

    /// <summary>
    /// Menyesuaikan kapasitas InventoryMenu menjadi 48 slot (4 baris) jika tas pemain sudah 48 slot.
    /// </summary>
    public static void InventoryMenu_Constructor_Prefix(
        bool playerInventory,
        ref int capacity,
        ref int rows
    )
    {
        if (playerInventory && Game1.player != null && Game1.player.maxItems.Value >= PremiumBackpackCapacity)
        {
            if (capacity == -1 || capacity == RequiredPreviousCapacity || capacity == PremiumBackpackCapacity)
            {
                capacity = PremiumBackpackCapacity;
                rows = 4;
            }
        }
    }

    /// <summary>
    /// Menggeser komponen bawah InventoryPage (TrashCan, OrganizeButton, Equipment, Portrait) ke bawah (+64px)
    /// agar baris ke-4 tas 48-slot tidak bertabrakan dengan elemen UI bawah.
    /// </summary>
    public static void InventoryPage_Constructor_Postfix(InventoryPage __instance)
    {
        if (Game1.player != null && Game1.player.maxItems.Value >= PremiumBackpackCapacity)
        {
            if (__instance.trashCan != null)
            {
                __instance.trashCan.bounds.Y += 64;
            }
            if (__instance.organizeButton != null)
            {
                __instance.organizeButton.bounds.Y += 64;
            }
            if (__instance.equipmentIcons != null)
            {
                foreach (var icon in __instance.equipmentIcons)
                {
                    icon.bounds.Y += 64;
                }
            }
            if (__instance.portrait != null)
            {
                __instance.portrait.bounds.Y += 64;
            }
        }
    }

    /// <summary>
    /// Intersepsi aksi tile "BuyBackpack" di GameLocation (khususnya SeedShop).
    /// Jika tas pemain saat ini 36 slot, tawarkan pembelian Tas Punggung Premium 48 slot seharga 50.000g.
    /// </summary>
    public static bool GameLocation_PerformAction_Prefix(
        GameLocation __instance,
        string[] action,
        Farmer who,
        xTile.Dimensions.Location tileLocation,
        ref bool __result
    )
    {
        if (action == null || action.Length == 0 || who == null) return true;

        if (action[0] == "BuyBackpack" && who.maxItems.Value == RequiredPreviousCapacity)
        {
            // Tampilkan dialog konfirmasi pembelian tas 48 slot
            string question = $"Beli Tas Punggung Premium ({PremiumBackpackCapacity} slot) seharga {PremiumBackpackPrice:N0}g?";
            Response[] responses = new Response[2]
            {
                new Response("Yes", "Ya, beli sekarang"),
                new Response("No", "Tidak sekarang")
            };

            __instance.createQuestionDialogue(question, responses, (f, answer) =>
            {
                if (answer == "Yes")
                {
                    TryPurchasePremiumBackpack(f);
                }
            });

            __result = true;
            return false; // Skip penanganan vanilla yang biasanya mengabaikan jika maxItems >= 36
        }

        return true;
    }

    /// <summary>
    /// Mengeksekusi transaksi pembelian Tas Punggung Premium 48 slot.
    /// </summary>
    public static bool TryPurchasePremiumBackpack(Farmer farmer)
    {
        if (farmer == null) return false;

        if (farmer.maxItems.Value < RequiredPreviousCapacity)
        {
            _monitor?.Log("[BiggerBackpack] Pemain harus memiliki Tas Deluxe (36 slot) terlebih dahulu.", LogLevel.Warn);
            return false;
        }

        if (farmer.maxItems.Value >= PremiumBackpackCapacity)
        {
            _monitor?.Log("[BiggerBackpack] Pemain sudah memiliki kapasitas tas maksimum (48 slot).", LogLevel.Info);
            return false;
        }

        if (farmer.Money < PremiumBackpackPrice)
        {
            try
            {
                Game1.playSound("cancel");
                Game1.drawObjectDialogue("Uang Anda tidak mencukupi untuk membeli Tas Punggung Premium (50.000g).");
            }
            catch
            {
            }
            return false;
        }

        if (ReferenceEquals(Game1.player, farmer))
        {
            farmer.Money -= PremiumBackpackPrice;
        }
        else
        {
            farmer._money = Math.Max(0, farmer._money - PremiumBackpackPrice);
        }

        farmer.increaseBackpackSize(12); // Menambah 12 slot sehingga maxItems menjadi 48!
        ToolbeltManager.EnsureCapacity(farmer);

        try
        {
            Game1.playSound("purchase");
            Game1.drawObjectDialogue($"Selamat! Anda berhasil membeli Tas Punggung Premium. Kapasitas tas Anda sekarang {farmer.maxItems.Value} slot!");
        }
        catch
        {
        }
        _monitor?.Log($"[BiggerBackpack] {farmer.Name} berhasil membeli Tas Punggung Premium. Kapasitas kini {farmer.maxItems.Value} slot tas + 10 toolbelt (Total 58 slot).", LogLevel.Info);
        return true;
    }
}
