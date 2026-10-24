using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewBridge.Client.Tools;
using StardewBridge.Client.UI;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley.Network;

namespace StardewBridge.Client;

public static class ClientPatches
{
    public static void Apply(Harmony harmony)
    {
        // 1. Intersepsi Packet 33 (dedicatedServerSync) untuk StardewBridge sub-opcodes (0x80..0xFF)
        var processMethod = AccessTools.Method(typeof(Multiplayer), nameof(Multiplayer.processIncomingMessage));
        if (processMethod != null)
        {
            harmony.Patch(processMethod, prefix: new HarmonyMethod(typeof(ClientPatches), nameof(ProcessIncomingMessage_Prefix)));
        }

        // 2. Intersepsi Chat Box untuk perintah lokal SmartTools & unfreeze
        var chatBoxEnterMethod = AccessTools.Method(typeof(ChatBox), nameof(ChatBox.textBoxEnter), new[] { typeof(string) });
        if (chatBoxEnterMethod != null)
        {
            harmony.Patch(chatBoxEnterMethod, prefix: new HarmonyMethod(typeof(ClientPatches), nameof(ChatBoxEnter_Prefix)));
        }

        // 3. Daftarkan patches Toolbelt dan SmartTools
        ToolbeltManager.ApplyPatches(harmony);
        SmartToolsEngine.ApplyPatches(harmony);

        // 4. Patch anti-freeze engine pada Game1.toolAnimationDone jika alat dilepas saat animasi
        var toolAnimationDoneMethod = AccessTools.Method(
            typeof(Game1),
            nameof(Game1.toolAnimationDone),
            new[] { typeof(Farmer) }
        );
        if (toolAnimationDoneMethod != null)
        {
            harmony.Patch(
                toolAnimationDoneMethod,
                prefix: new HarmonyMethod(typeof(ClientPatches), nameof(ToolAnimationDone_Prefix))
            );
        }

        // 5. Geser notifikasi HUD (item grab, quest, achievements) agar maju ke kanan dan tidak tertutup slot tools
        var hudDrawMethod = AccessTools.Method(
            typeof(HUDMessage),
            nameof(HUDMessage.draw),
            new[] { typeof(SpriteBatch), typeof(int), typeof(int).MakeByRefType() }
        );
        if (hudDrawMethod != null)
        {
            harmony.Patch(
                hudDrawMethod,
                transpiler: new HarmonyMethod(typeof(ClientPatches), nameof(HUDMessage_Draw_Transpiler))
            );
        }

        // 6. Geser ChatBox agar chat input dan history tidak bertabrakan dengan vertical toolbelt
        var chatBoxUpdatePosMethod = AccessTools.Method(typeof(ChatBox), "updatePosition");
        if (chatBoxUpdatePosMethod != null)
        {
            harmony.Patch(
                chatBoxUpdatePosMethod,
                postfix: new HarmonyMethod(typeof(ClientPatches), nameof(ChatBox_UpdatePosition_Postfix))
            );
        }

        // 7. Dynamic Door Router untuk Rumah Fisik Utama (The Grand Estate)
        var onUseDoorMethod = AccessTools.Method(
            typeof(Building),
            nameof(Building.OnUseHumanDoor),
            new[] { typeof(Farmer) }
        );
        if (onUseDoorMethod != null)
        {
            harmony.Patch(
                onUseDoorMethod,
                prefix: new HarmonyMethod(typeof(ClientPatches), nameof(OnUseHumanDoor_Prefix))
            );
        }

        // 8. Patch Farmer.getMailboxPosition agar seluruh pemain virtual melihat notifikasi surat di kotak surat utama Farm
        var getMailboxPosMethod = AccessTools.Method(
            typeof(Farmer),
            nameof(Farmer.getMailboxPosition)
        );
        if (getMailboxPosMethod != null)
        {
            harmony.Patch(
                getMailboxPosMethod,
                prefix: new HarmonyMethod(typeof(ClientPatches), nameof(GetMailboxPosition_Prefix))
            );
        }

        // 9. Patch LanguageSelectionMenu..ctor agar Bahasa Indonesia diposisikan di Halaman 1 (Page 1)
        var langMenuCtor = AccessTools.Constructor(typeof(LanguageSelectionMenu));
        if (langMenuCtor != null)
        {
            harmony.Patch(
                langMenuCtor,
                postfix: new HarmonyMethod(typeof(ClientPatches), nameof(LanguageSelectionMenu_Ctor_Postfix))
            );
        }

        var applyLangMethod = AccessTools.Method(typeof(LanguageSelectionMenu), nameof(LanguageSelectionMenu.ApplyLanguage));
        if (applyLangMethod != null)
        {
            harmony.Patch(
                applyLangMethod,
                prefix: new HarmonyMethod(typeof(ClientPatches), nameof(LanguageSelectionMenu_ApplyLanguage_Prefix)),
                postfix: new HarmonyMethod(typeof(ClientPatches), nameof(LanguageSelectionMenu_ApplyLanguage_Postfix))
            );
        }
    }

    public static bool ProcessIncomingMessage_Prefix(Multiplayer __instance, IncomingMessage msg)
    {
        if (msg.MessageType == 33 && msg.Reader != null)
        {
            long origPos = msg.Reader.BaseStream.Position;
            if (msg.Reader.BaseStream.Length > origPos)
            {
                byte subOpCode = msg.Reader.ReadByte();

                if (subOpCode >= 0x80)
                {
                    // subOpCode telah dibaca (consumed). Posisi reader sekarang tepat di awal data payload.
                    BridgeClientCore.HandleServerPacket(msg, subOpCode);
                    return false; // Mencegah dedicatedServerSync vanilla menangani atau me-throw exception
                }

                // Kembalikan posisi reader untuk penanganan vanilla jika ini adalah paket vanilla (< 0x80)
                msg.Reader.BaseStream.Position = origPos;
            }
        }
        return true;
    }

    public static bool ChatBoxEnter_Prefix(ChatBox __instance, string text_to_send)
    {
        if (string.IsNullOrWhiteSpace(text_to_send)) return true;

        string trimmed = text_to_send.Trim();

        // Cek apakah chat merupakan perintah unfreeze darurat
        if (trimmed.Equals("!unfreeze", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("/unfreeze", StringComparison.OrdinalIgnoreCase))
        {
            if (Game1.player != null)
            {
                Game1.player.forceCanMove();
                Game1.playSound("drumkit6");
                Game1.addHUDMessage(new HUDMessage(I18n.Get("hud.antifreeze.restored"), HUDMessage.newQuest_type));
            }
            return false;
        }

        // Cek apakah chat merupakan perintah berkunjung (/visit)
        if (trimmed.StartsWith("!visit", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("/visit", StringComparison.OrdinalIgnoreCase))
        {
            var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 1)
            {
                VisitCabin(parts[1]);
            }
            else
            {
                Game1.addHUDMessage(new HUDMessage(I18n.Get("hud.visit.usage"), HUDMessage.error_type));
            }
            return false;
        }

        // Cek apakah chat merupakan perintah SmartTools lokal
        if (trimmed.StartsWith("!smarttools", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("!st", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("/smarttools", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("/st", StringComparison.OrdinalIgnoreCase))
        {
            var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string sub = parts.Length > 1 ? parts[1].ToLowerInvariant() : "toggle";

            switch (sub)
            {
                case "on":
                case "1":
                case "enable":
                case "aktif":
                    SmartToolsEngine.SetEnabled(true, showNotification: true);
                    break;

                case "off":
                case "0":
                case "disable":
                case "nonaktif":
                    SmartToolsEngine.SetEnabled(false, showNotification: true);
                    break;

                default:
                    SmartToolsEngine.Toggle(showNotification: true);
                    break;
            }

            // Kembalikan false agar perintah tidak dikirim ke server / tidak bocor ke obrolan pemain lain
            return false;
        }

        return true;
    }

    public static void ToolAnimationDone_Prefix(Farmer who)
    {
        if (who != null && who.CurrentTool == null)
        {
            // Vanilla Stardew Valley langsung return jika CurrentTool == null, meninggalkan UsingTool=true dan CanMove=false.
            // Kami pulihkan kontrol secara aman agar pemain tidak mengalami freeze permanen.
            who.UsingTool = false;
            who.CanMove = true;
            try
            {
                who.FarmerSprite?.StopAnimation();
            }
            catch
            {
            }
        }
    }

    public static IEnumerable<CodeInstruction> HUDMessage_Draw_Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var list = new List<CodeInstruction>(instructions);
        var getLeftMethod = AccessTools.PropertyGetter(typeof(Rectangle), nameof(Rectangle.Left));
        var getHudOffsetMethod = AccessTools.Method(typeof(VerticalToolbeltRenderer), nameof(VerticalToolbeltRenderer.GetHudNotificationXOffset));

        for (int i = 0; i < list.Count; i++)
        {
            // Deteksi ldc.i4.s 16 (atau ldc.i4 16) yang didahului oleh get_Left() atau neg
            if ((list[i].opcode == OpCodes.Ldc_I4_S || list[i].opcode == OpCodes.Ldc_I4) &&
                Convert.ToInt32(list[i].operand) == 16)
            {
                if (i > 0 && (list[i - 1].Calls(getLeftMethod) || list[i - 1].opcode == OpCodes.Neg))
                {
                    list[i] = new CodeInstruction(OpCodes.Call, getHudOffsetMethod);
                }
            }
        }
        return list;
    }

    public static void ChatBox_UpdatePosition_Postfix(ChatBox __instance)
    {
        int offset = VerticalToolbeltRenderer.GetHudNotificationXOffset() - 8;
        if (__instance.xPositionOnScreen < offset)
        {
            __instance.xPositionOnScreen = offset;
            __instance.chatBox.X = offset;
            if (__instance.chatBoxCC != null)
                __instance.chatBoxCC.bounds.X = offset;
            if (__instance.emojiMenuIcon != null)
                __instance.emojiMenuIcon.bounds.X = offset + __instance.chatBox.Width;
        }
    }
    public static bool OnUseHumanDoor_Prefix(Building __instance, Farmer who, ref bool __result)
    {
        try
        {
            if (__instance.isCabin && (__instance.modData.ContainsKey("svheadless/main_estate_house") || (__instance.tileX.Value == 58 && __instance.tileY.Value == 14)))
            {
                var targetBuilding = FindAssignedCabin(who);
                if (targetBuilding != null && targetBuilding.GetIndoors() is Cabin targetIndoors)
                {
                    var entry = targetIndoors.getEntryLocation();
                    Game1.warpFarmer(targetIndoors.NameOrUniqueName, entry.X, entry.Y, 0);
                    __result = true;
                    return false; // Skip original method agar tidak masuk ke kabin fisik default!
                }
            }
        }
        catch (Exception ex)
        {
            BridgeClientCore.Monitor?.Log($"[DoorRouter] Error saat routing pintu: {ex.Message}", StardewModdingAPI.LogLevel.Warn);
        }
        return true;
    }

    public static Building? FindAssignedCabin(Farmer who)
    {
        var farm = Game1.getFarm();
        if (farm == null) return null;

        // 1. Cocokkan berdasarkan kepemilikan owner / farmhand
        foreach (var b in farm.buildings)
        {
            if (b.isCabin && !b.modData.ContainsKey("svheadless/lobby_cabin") && b.GetIndoors() is Cabin c)
            {
                if ((c.owner != null && c.owner.UniqueMultiplayerID == who.UniqueMultiplayerID) ||
                    (false))
                {
                    return b;
                }
            }
        }

        // 2. Fallback: Ambil kabin pertama yang belum berpenghuni
        foreach (var b in farm.buildings)
        {
            if (b.isCabin && !b.modData.ContainsKey("svheadless/lobby_cabin") && b.GetIndoors() is Cabin c)
            {
                if (!c.HasOwner)
                {
                    return b;
                }
            }
        }

        // 3. Fallback: Ambil kabin non-lobby pertama
        return farm.buildings.FirstOrDefault(b => b.isCabin && !b.modData.ContainsKey("svheadless/lobby_cabin"));
    }

    public static void VisitCabin(string targetArg)
    {
        var farm = Game1.getFarm();
        if (farm == null) return;

        var cabins = farm.buildings.Where(b => b.isCabin && !b.modData.ContainsKey("svheadless/lobby_cabin")).ToList();
        Building? target = null;

        if (int.TryParse(targetArg, out int idx) && idx >= 1 && idx <= cabins.Count)
        {
            target = cabins[idx - 1];
        }
        else
        {
            target = cabins.FirstOrDefault(b => b.GetIndoors() is Cabin c && (
                c.owner?.Name?.Contains(targetArg, StringComparison.OrdinalIgnoreCase) == true ||
                false
            ));
        }

        if (target != null && target.GetIndoors() is Cabin targetIndoors)
        {
            var entry = targetIndoors.getEntryLocation();
            Game1.warpFarmer(targetIndoors.NameOrUniqueName, entry.X, entry.Y, 0);
            string ownerName = targetIndoors.owner?.Name ?? $"Kabin #{cabins.IndexOf(target) + 1}";
            Game1.addHUDMessage(new HUDMessage(I18n.Get("hud.visit.success", new { owner = ownerName }), HUDMessage.newQuest_type));
        }
        else
        {
            Game1.addHUDMessage(new HUDMessage(I18n.Get("hud.visit.notfound", new { target = targetArg }), HUDMessage.error_type));
        }
    }
    public static bool GetMailboxPosition_Prefix(Farmer __instance, ref Point __result)
    {
        try
        {
            var farm = Game1.getFarm();
            if (farm != null)
            {
                var mainHouse = farm.buildings.FirstOrDefault(b => b.isCabin && b.modData.ContainsKey("svheadless/main_estate_house"))
                                ?? farm.buildings.FirstOrDefault(b => b.isCabin && !b.modData.ContainsKey("svheadless/lobby_cabin") && b.tileX.Value > 0);

                if (mainHouse != null)
                {
                    // Kotak surat kabin terletak di pojok kanan bawah teras: (tileX + tilesWide - 1, tileY + tilesHigh - 1) -> (62, 16)
                    __result = new Point(
                        mainHouse.tileX.Value + mainHouse.tilesWide.Value - 1,
                        mainHouse.tileY.Value + mainHouse.tilesHigh.Value - 1
                    );
                    return false; // Skip perhitungan off-screen negatif!
                }
            }
        }
        catch (Exception ex)
        {
            BridgeClientCore.Monitor?.Log($"[Mailbox] Error saat menghitung posisi mailbox: {ex.Message}", StardewModdingAPI.LogLevel.Warn);
        }
        return true;
    }

    public static void LanguageSelectionMenu_Ctor_Postfix(LanguageSelectionMenu __instance)
    {
        try
        {
            var field = AccessTools.Field(typeof(LanguageSelectionMenu), "languages");
            if (field == null) return;

            var origDict = field.GetValue(__instance) as System.Collections.IDictionary;
            if (origDict == null || !origDict.Contains("ModLanguage_Indonesian")) return;

            var dictType = field.FieldType;
            var reordered = (System.Collections.IDictionary)Activator.CreateInstance(dictType)!;

            // 1. Masukkan English pertama jika ada
            if (origDict.Contains("en"))
            {
                reordered["en"] = origDict["en"]!;
            }

            // 2. Masukkan Bahasa Indonesia tepat di urutan ke-2 (Halaman 1)
            reordered["ModLanguage_Indonesian"] = origDict["ModLanguage_Indonesian"]!;

            // 3. Masukkan sisa bahasa lainnya
            foreach (System.Collections.DictionaryEntry entry in origDict)
            {
                string key = (string)entry.Key;
                if (key != "en" && key != "ModLanguage_Indonesian")
                {
                    reordered[key] = entry.Value!;
                }
            }

            field.SetValue(__instance, reordered);

            // Re-setup tombol agar Bahasa Indonesia langsung aktif di Halaman 1
            var setupButtonsMethod = AccessTools.Method(typeof(LanguageSelectionMenu), "SetupButtons");
            setupButtonsMethod?.Invoke(__instance, null);
            BridgeClientCore.Monitor?.Log("[i18n] Bahasa Indonesia berhasil diposisikan ke Halaman 1 (Page 1) pada Menu Pilihan Bahasa!", StardewModdingAPI.LogLevel.Info);
        }
        catch (Exception ex)
        {
            BridgeClientCore.Monitor?.Log($"[i18n] Gagal memindahkan Bahasa Indonesia ke Halaman 1: {ex.Message}", StardewModdingAPI.LogLevel.Warn);
        }
    }
public static void LanguageSelectionMenu_ApplyLanguage_Prefix()
    {
        ModEntry.Instance?.InvalidateLanguageCaches();
    }

    public static void LanguageSelectionMenu_ApplyLanguage_Postfix()
    {
        ModEntry.Instance?.InvalidateLanguageCaches();
    }
}
