using System;
using System.Collections.Generic;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Tools;
namespace StardewBridge.Client.Tools;

/// <summary>
/// Mengelola 10 slot khusus Dedicated Toolbelt (Indeks 48..57) pada inventori pemain.
/// Memisahkan peralatan kerja dan senjata dari tas utama sehingga kapasitas tas 100% lebih lega.
/// Mengunci slot tas yang belum dibeli (12..47) agar pemain tidak bisa menaruh item di slot yang belum dibuka.
/// </summary>
public static class ToolbeltManager
{
    public const int ToolbeltStartIndex = 48;
    public const int ToolbeltSlotCount = 10;
    public const int TotalCapacityWithToolbelt = 58; // Indeks 0..57
    private static IMonitor? _monitor;
    public static void Initialize(IMonitor monitor)
    {
        _monitor = monitor;
    }

    /// <summary>
    /// Memeriksa apakah item merupakan Tool atau Senjata yang sah untuk disimpan di Toolbelt.
    /// Sesuai Wiki: Hoe, Pickaxe, Axe, Watering Can, Scythe, Fishing Rod, Pan, Milk Pail, Shears, Weapons, Slingshot, Wand.
    /// Objek biasa (bibit, kayu, batu, makanan, hasil panen) ditolak.
    /// </summary>
    public static bool IsToolbeltEligible(Item? item)
    {
        if (item == null) return false;
        // Semua turunan StardewValley.Tool (termasuk MeleeWeapon/Pedang/Sabit dan Slingshot)
        return item is StardewValley.Tool;
    }

    /// <summary>
    /// Memastikan koleksi inventori pemain memiliki kapasitas yang mencukupi untuk indeks Toolbelt (0..57).
    /// </summary>
    public static void EnsureCapacity(Farmer? farmer)
    {
        if (farmer == null) return;
        while (farmer.Items.Count < TotalCapacityWithToolbelt)
        {
            farmer.Items.Add(null);
        }
        MigrateTo58SlotsIfNeeded(farmer);
        SanitizeInventorySlots(farmer);
    }


    /// <summary>
    /// Melakukan migrasi otomatis untuk save lama (legacy 46 slot) ke arsitektur unified 58 slot.
    /// Memindahkan tool dari slot 36..45 ke slot 48..57 jika pemain belum meng-upgrade tas ke 48 slot.
    /// </summary>
    public static void MigrateTo58SlotsIfNeeded(Farmer? farmer)
    {
        if (farmer?.Items == null) return;

        if (farmer.modData != null && farmer.modData.ContainsKey("svheadless/migrated_58slot"))
        {
            return;
        }

        while (farmer.Items.Count < TotalCapacityWithToolbelt)
        {
            farmer.Items.Add(null);
        }

        // Jika pemain masih memakai tas level lama (<= 36) dan ada tool di indeks 36..45
        if (farmer.maxItems.Value <= 36)
        {
            for (int i = 0; i < 10; i++)
            {
                int oldToolIndex = 36 + i;
                int newToolIndex = ToolbeltStartIndex + i; // 48 + i
                if (oldToolIndex < farmer.Items.Count && farmer.Items[oldToolIndex] != null)
                {
                    if (newToolIndex < farmer.Items.Count && farmer.Items[newToolIndex] == null)
                    {
                        farmer.Items[newToolIndex] = farmer.Items[oldToolIndex];
                        farmer.Items[oldToolIndex] = null;
                        _monitor?.Log($"[Toolbelt Migration] Memindahkan {farmer.Items[newToolIndex]?.DisplayName} dari #{oldToolIndex} ke #{newToolIndex}", LogLevel.Info);
                    }
                }
            }
        }

        if (farmer.modData != null)
        {
            farmer.modData["svheadless/migrated_58slot"] = "true";
        }
    }

    /// <summary>
    /// Memeriksa dan membersihkan:
    /// 1. Slot Toolbelt (36..45) dari item non-tool.
    /// 2. Slot tas terkunci (maxItems..47) dari item apa pun yang terdampar,
    ///    dan memindahkannya kembali ke slot tas aktif (0..maxItems-1).
    /// </summary>
    public static void SanitizeInventorySlots(Farmer? farmer)
    {
        if (farmer?.Items == null) return;
        int unlockedLimit = Math.Min(farmer.maxItems.Value, ToolbeltStartIndex);
        // 1. Evakuasi item dari slot tas yang TERKUNCI (unlockedLimit .. ToolbeltStartIndex - 1)
        for (int i = unlockedLimit; i < ToolbeltStartIndex && i < farmer.Items.Count; i++)
        {
            var item = farmer.Items[i];
            if (item != null)
            {
                _monitor?.Log($"[Inventory] Menemukan item {item.DisplayName} x{item.Stack} di slot terkunci #{i}. Memindahkan ke tas aktif...", LogLevel.Warn);
                EvacuateItemToActiveBag(farmer, item, i, unlockedLimit);
            }
        }
        // 2. Evakuasi item non-tool dari slot DEDICATED TOOLBELT (36..45)
        for (int i = ToolbeltStartIndex; i < ToolbeltStartIndex + ToolbeltSlotCount && i < farmer.Items.Count; i++)
        {
            var item = farmer.Items[i];
            if (item != null && !IsToolbeltEligible(item))
            {
                _monitor?.Log($"[Toolbelt] Menemukan item non-tool ({item.DisplayName} x{item.Stack}) di slot toolbelt #{i}. Memindahkan ke tas aktif...", LogLevel.Warn);
                EvacuateItemToActiveBag(farmer, item, i, unlockedLimit);
            }
        }
    }
    private static void EvacuateItemToActiveBag(Farmer farmer, Item item, int sourceIndex, int unlockedLimit)
    {
        // 1. Cari slot kosong di tas aktif (0 .. unlockedLimit - 1)
        for (int b = 0; b < unlockedLimit && b < farmer.Items.Count; b++)
        {
            if (farmer.Items[b] == null)
            {
                farmer.Items[b] = item;
                farmer.Items[sourceIndex] = null;
                return;
            }
        }
        // 2. Jika tas aktif penuh, coba stack dengan item sejenis di tas aktif
        for (int b = 0; b < unlockedLimit && b < farmer.Items.Count; b++)
        {
            var bagItem = farmer.Items[b];
            if (bagItem != null && bagItem.canStackWith(item))
            {
                item.addToStack(bagItem);
                if (item.Stack == 0)
                {
                    farmer.Items[sourceIndex] = null;
                    return;
                }
            }
        }
        // 3. Jika tas aktif benar-benar penuh dan tidak bisa di-stack, drop ke lantai
        farmer.Items[sourceIndex] = null;
        if (farmer.currentLocation != null)
        {
            Game1.createItemDebris(item, farmer.Position, farmer.FacingDirection, farmer.currentLocation);
            _monitor?.Log($"[Inventory] Tas aktif penuh, menjatuhkan {item.DisplayName} ke tanah dekat pemain.", LogLevel.Warn);
        }
    }

    /// <summary>
    /// Mencoba memasukkan Tool/Senjata ke dalam 10 slot Toolbelt (36..45).
    /// Mengembalikan true jika berhasil masuk ke Toolbelt; false jika Toolbelt penuh (sehingga overflow ke tas).
    /// </summary>
    public static bool TryAddToolToToolbelt(Farmer farmer, Item item)
    {
        if (farmer == null || item == null) return false;
        if (!IsToolbeltEligible(item)) return false;
        EnsureCapacity(farmer);
        for (int i = ToolbeltStartIndex; i < ToolbeltStartIndex + ToolbeltSlotCount; i++)
        {
            if (farmer.Items[i] == null)
            {
                farmer.Items[i] = item;
                _monitor?.Log($"[Toolbelt] {item.DisplayName} berhasil dimasukkan ke Dedicated Toolbelt slot {i - ToolbeltStartIndex + 1} (Inventori #{i})", LogLevel.Trace);
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Memindahkan peralatan awal pemain baru (Axe, Hoe, Pickaxe, Watering Can, Scythe)
    /// dari tas reguler ke dalam slot Dedicated Toolbelt.
    /// </summary>
    public static void MigrateStarterToolsToToolbelt(Farmer farmer)
    {
        if (farmer == null) return;
        EnsureCapacity(farmer);
        int movedCount = 0;
        int checkLimit = Math.Min(farmer.maxItems.Value, ToolbeltStartIndex);
        for (int i = 0; i < checkLimit; i++)
        {
            var item = farmer.Items[i];
            if (item != null && IsToolbeltEligible(item))
            {
                if (TryAddToolToToolbelt(farmer, item))
                {
                    farmer.Items[i] = null;
                    movedCount++;
                }
            }
        }
        if (movedCount > 0)
        {
            _monitor?.Log($"[Toolbelt] Berhasil memindahkan {movedCount} alat awal dari tas ke Dedicated Toolbelt. Tas utama kini lega!", LogLevel.Info);
        }
    }

    /// <summary>
    /// Mendaftarkan Harmony Patch untuk isolasi slot tas, penguncian slot belum dibuka, dan prioritas Toolbelt.
    /// </summary>
    public static void ApplyPatches(Harmony harmony)
    {
        // 1. Intersepsi penambahan item: prioritaskan Toolbelt untuk Tool/Weapon
        var addItemMethod = AccessTools.Method(
            typeof(Farmer),
            nameof(Farmer.addItemToInventory),
            new[] { typeof(Item), typeof(List<Item>) }
        );
        if (addItemMethod != null)
        {
            harmony.Patch(
                addItemMethod,
                prefix: new HarmonyMethod(typeof(ToolbeltManager), nameof(AddItemToInventory_Prefix))
            );
        }
        // 2. Intersepsi perhitungan slot kosong tas agar mengabaikan slot Toolbelt (36..45)
        var freeSpotsMethod = AccessTools.Method(typeof(Farmer), nameof(Farmer.freeSpotsInInventory));
        if (freeSpotsMethod != null)
        {
            harmony.Patch(
                freeSpotsMethod,
                prefix: new HarmonyMethod(typeof(ToolbeltManager), nameof(FreeSpotsInInventory_Prefix))
            );
        }
        // 3. Intersepsi status tas penuh agar hanya memeriksa tas reguler (0..MaxItems-1)
        var isInventoryFullMethod = AccessTools.Method(typeof(Farmer), nameof(Farmer.isInventoryFull));
        if (isInventoryFullMethod != null)
        {
            harmony.Patch(
                isInventoryFullMethod,
                prefix: new HarmonyMethod(typeof(ToolbeltManager), nameof(IsInventoryFull_Prefix))
            );
        }
        // 4. Intersepsi shiftToolbar agar tombol TAB hanya menukar baris tas dan TIDAK menggeser Toolbelt (36..45)
        var shiftToolbarMethod = AccessTools.Method(
            typeof(Farmer),
            nameof(Farmer.shiftToolbar),
            new[] { typeof(bool) }
        );
        if (shiftToolbarMethod != null)
        {
            harmony.Patch(
                shiftToolbarMethod,
                prefix: new HarmonyMethod(typeof(ToolbeltManager), nameof(ShiftToolbar_Prefix))
            );
        }
        // 5. Intersepsi organizeItemsInList agar tombol Rapikan/Organize hanya mengurutkan tas aktif (0..MaxItems-1)
        var organizeMethod = AccessTools.Method(
            typeof(ItemGrabMenu),
            nameof(ItemGrabMenu.organizeItemsInList),
            new[] { typeof(IList<Item>) }
        );
        if (organizeMethod != null)
        {
            harmony.Patch(
                organizeMethod,
                prefix: new HarmonyMethod(typeof(ToolbeltManager), nameof(OrganizeItemsInList_Prefix))
            );
        }
        // 6. Intersepsi InventoryMenu.leftClick untuk MENCEGAH menaruh item di slot tas yang belum dibuka
        var leftClickMethod = AccessTools.Method(
            typeof(InventoryMenu),
            nameof(InventoryMenu.leftClick),
            new[] { typeof(int), typeof(int), typeof(Item), typeof(bool) }
        );
        if (leftClickMethod != null)
        {
            harmony.Patch(
                leftClickMethod,
                prefix: new HarmonyMethod(typeof(ToolbeltManager), nameof(InventoryMenu_LeftClick_Prefix))
            );
        }
        // 7. Intersepsi InventoryMenu.rightClick untuk MENCEGAH interaksi di slot tas yang belum dibuka
        var rightClickMethod = AccessTools.Method(
            typeof(InventoryMenu),
            nameof(InventoryMenu.rightClick),
            new[] { typeof(int), typeof(int), typeof(Item), typeof(bool), typeof(bool) }
        );
        if (rightClickMethod != null)
        {
            harmony.Patch(
                rightClickMethod,
                prefix: new HarmonyMethod(typeof(ToolbeltManager), nameof(InventoryMenu_RightClick_Prefix))
            );
        }
        // 8. Intersepsi InventoryMenu.hover untuk menyembunyikan tooltip pada slot tas yang terkunci
        var hoverMethod = AccessTools.Method(
            typeof(InventoryMenu),
            nameof(InventoryMenu.hover),
            new[] { typeof(int), typeof(int), typeof(Item) }
        );
        if (hoverMethod != null)
        {
            harmony.Patch(
                hoverMethod,
                prefix: new HarmonyMethod(typeof(ToolbeltManager), nameof(InventoryMenu_Hover_Prefix))
            );
        }
    }
    public static bool AddItemToInventory_Prefix(Farmer __instance, Item item, List<Item> affected_items_list, ref Item? __result)
    {
        if (item == null) return true;
        if (IsToolbeltEligible(item))
        {
            // Coba masukkan ke 10 slot Dedicated Toolbelt terlebih dahulu
            if (TryAddToolToToolbelt(__instance, item))
            {
                __instance.OnItemReceived(item, item.Stack, null, hideHudNotification: false);
                affected_items_list?.Add(item);
                __result = null; // Berhasil masuk seutuhnya ke Toolbelt
                return false;    // Lewati penempatan di tas reguler
            }
        }
        return true;
    }
    public static bool FreeSpotsInInventory_Prefix(Farmer __instance, ref int __result)
    {
        EnsureCapacity(__instance);
        int count = 0;
        int max = __instance.maxItems.Value;
        for (int i = 0; i < max && i < __instance.Items.Count && i < ToolbeltStartIndex; i++)
        {
            if (__instance.Items[i] != null)
            {
                count++;
            }
        }
        __result = Math.Max(0, max - count);
        return false;
    }
    public static bool IsInventoryFull_Prefix(Farmer __instance, ref bool __result)
    {
        EnsureCapacity(__instance);
        int max = __instance.maxItems.Value;
        for (int i = 0; i < max && i < __instance.Items.Count && i < ToolbeltStartIndex; i++)
        {
            if (__instance.Items[i] == null)
            {
                __result = false;
                return false;
            }
        }
        __result = true;
        return false;
    }

    /// <summary>
    /// Intersepsi rotasi toolbar (tombol TAB).
    /// Membatasi perputaran baris hanya pada tas reguler (0..MaxItems-1).
    /// Slot Toolbelt (36..45) sama sekali TIDAK ikut berputar.
    /// </summary>
    public static bool ShiftToolbar_Prefix(Farmer __instance, bool right)
    {
        if (__instance?.Items == null) return false;
        EnsureCapacity(__instance);
        int bagCapacity = Math.Min(__instance.maxItems.Value, ToolbeltStartIndex);
        if (bagCapacity <= 12)
        {
            // Tas hanya 1 baris (12 slot), tidak ada baris lain untuk di-swap.
            return false;
        }
        // Cek kondisi pemain sesuai standar vanilla
        if (__instance.UsingTool || Game1.dialogueUp || !__instance.CanMove || !__instance.Items.HasAny() || Game1.eventUp || Game1.farmEvent != null)
        {
            return false;
        }
        try
        {
            Game1.playSound("shwip");
        }
        catch
        {
        }
        __instance.CurrentItem?.actionWhenStopBeingHeld(__instance);
        // Rotasi HANYA baris-baris tas (0 .. bagCapacity - 1). Slot 36..45 (Toolbelt) TIDAK disentuh.
        if (right)
        {
            var row0 = new List<Item?>();
            for (int i = 0; i < 12; i++)
            {
                row0.Add(__instance.Items[i]);
            }
            for (int i = 12; i < bagCapacity; i++)
            {
                __instance.Items[i - 12] = __instance.Items[i];
            }
            int lastRowStart = bagCapacity - 12;
            for (int i = 0; i < 12; i++)
            {
                __instance.Items[lastRowStart + i] = row0[i];
            }
        }
        else
        {
            int lastRowStart = bagCapacity - 12;
            var lastRow = new List<Item?>();
            for (int i = 0; i < 12; i++)
            {
                lastRow.Add(__instance.Items[lastRowStart + i]);
            }
            for (int i = lastRowStart - 1; i >= 0; i--)
            {
                __instance.Items[i + 12] = __instance.Items[i];
            }
            for (int i = 0; i < 12; i++)
            {
                __instance.Items[i] = lastRow[i];
            }
        }
        try
        {
            __instance.netItemStowed?.Set(false);
        }
        catch
        {
        }
        __instance.CurrentItem?.actionWhenBeingHeld(__instance);
        // Notifikasi Toolbar HUD vanilla untuk menjalankan animasi geser
        if (Game1.onScreenMenus != null)
        {
            for (int i = 0; i < Game1.onScreenMenus.Count; i++)
            {
                if (Game1.onScreenMenus[i] is Toolbar toolbar)
                {
                    try
                    {
                        toolbar.shifted(right);
                    }
                    catch
                    {
                    }
                    break;
                }
            }
        }
        return false; // Skip method vanilla
    }

    /// <summary>
    /// Intersepsi fitur Rapikan/Organize.
    /// Membatasi pengurutan item hanya pada tas aktif pemain (0..MaxItems-1).
    /// Slot Toolbelt (36..45) dipertahankan utuh dan tidak tersentuh.
    /// </summary>
    public static bool OrganizeItemsInList_Prefix(IList<Item> items)
    {
        if (items == null) return true;
        if (Game1.player != null && ReferenceEquals(items, Game1.player.Items))
        {
            SanitizeInventorySlots(Game1.player);
            int bagCapacity = Math.Min(Game1.player.maxItems.Value, ToolbeltStartIndex);
            var bagItems = new List<Item>();
            var bagTools = new List<Item>();
            for (int i = 0; i < bagCapacity && i < items.Count; i++)
            {
                var it = items[i];
                if (it == null) continue;
                if (it is Tool)
                {
                    bagTools.Add(it);
                }
                else
                {
                    bagItems.Add(it);
                }
            }
            // Gabungkan stackable item yang sama di dalam tas
            for (int i = 0; i < bagItems.Count; i++)
            {
                var item1 = bagItems[i];
                if (item1.getRemainingStackSpace() > 0)
                {
                    for (int j = i + 1; j < bagItems.Count; j++)
                    {
                        var item2 = bagItems[j];
                        if (item1.canStackWith(item2))
                        {
                            item2.addToStack(item1);
                            if (item2.Stack == 0)
                            {
                                bagItems.RemoveAt(j);
                                j--;
                            }
                        }
                    }
                }
            }
            // Urutkan item tas
            bagItems.Sort();
            bagItems.InsertRange(0, bagTools);
            // Tulis kembali HANYA ke slot tas aktif (0 .. bagCapacity - 1)
            for (int i = 0; i < bagCapacity && i < items.Count; i++)
            {
                items[i] = (i < bagItems.Count ? bagItems[i] : null)!;
            }
            return false; // Skip method vanilla
        }
        return true;
    }

    /// <summary>
    /// Mencegah pemain mengklik atau menaruh item di baris tas yang masih terkunci (indeks >= maxItems).
    /// </summary>
    public static bool InventoryMenu_LeftClick_Prefix(InventoryMenu __instance, int x, int y, Item toPlace, bool playSound, ref Item? __result)
    {
        if (__instance == null || __instance.inventory == null) return true;
        if (__instance.playerInventory || (Game1.player != null && ReferenceEquals(__instance.actualInventory, Game1.player.Items)))
        {
            int max = Game1.player?.maxItems.Value ?? 12;
            foreach (var c in __instance.inventory)
            {
                if (c != null && c.containsPoint(x, y))
                {
                    if (int.TryParse(c.name, out int whichIndex) && whichIndex >= max)
                    {
                        // Slot terkunci! Tolak interaksi
                        if (toPlace != null && playSound)
                        {
                            try
                            {
                                Game1.playSound("cancel");
                            }
                            catch
                            {
                            }
                        }
                        __result = toPlace; // Pertahankan item tetap di kursor pemain
                        return false;       // Lewati logika penempatan vanilla
                    }
                    break;
                }
            }
        }
        return true;
    }

    /// <summary>
    /// Mencegah klik kanan (split/ambil satu) di baris tas yang masih terkunci.
    /// </summary>
    public static bool InventoryMenu_RightClick_Prefix(InventoryMenu __instance, int x, int y, Item toAddTo, bool playSound, bool onlyCheckToolAttachments, ref Item? __result)
    {
        if (__instance == null || __instance.inventory == null) return true;
        if (__instance.playerInventory || (Game1.player != null && ReferenceEquals(__instance.actualInventory, Game1.player.Items)))
        {
            int max = Game1.player?.maxItems.Value ?? 12;
            foreach (var c in __instance.inventory)
            {
                if (c != null && c.containsPoint(x, y))
                {
                    if (int.TryParse(c.name, out int whichIndex) && whichIndex >= max)
                    {
                        __result = toAddTo;
                        return false;
                    }
                    break;
                }
            }
        }
        return true;
    }

    /// <summary>
    /// Menyembunyikan tooltip saat kursor mengarah ke baris tas yang masih terkunci.
    /// </summary>
    public static bool InventoryMenu_Hover_Prefix(InventoryMenu __instance, int x, int y, Item heldItem, ref Item? __result)
    {
        if (__instance == null || __instance.inventory == null) return true;
        if (__instance.playerInventory || (Game1.player != null && ReferenceEquals(__instance.actualInventory, Game1.player.Items)))
        {
            int max = Game1.player?.maxItems.Value ?? 12;
            foreach (var c in __instance.inventory)
            {
                if (c != null && c.containsPoint(x, y))
                {
                    if (int.TryParse(c.name, out int whichIndex) && whichIndex >= max)
                    {
                        __result = null;
                        return false;
                    }
                    break;
                }
            }
        }
        return true;
    }
}