using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;

namespace StardewBridge.Client.UI;

/// <summary>
/// Menambahkan tombol ekspor savegame (.zip) di bawah tombol delete pada layar Load Game Menu.
/// Memungkinkan pemain mengekspor kebun lokal langsung menjadi arsip .zip sekali klik
/// yang otomatis disimpan ke folder Downloads dan dibuka di Windows Explorer.
/// </summary>
public static class SaveExportMenuHandler
{
    private static IMonitor? _monitor;
    private static IModHelper? _helper;
    private static Texture2D? _buttonTexture;
    private static string? _hoveredTooltip;

    public static void Initialize(IMonitor monitor, IModHelper helper)
    {
        _monitor = monitor;
        _helper = helper;

        helper.Events.Display.RenderedActiveMenu += OnRenderedActiveMenu;
        helper.Events.Input.ButtonPressed += OnButtonPressed;
    }

    private static LoadGameMenu? GetActiveLoadMenu()
    {
        if (Game1.activeClickableMenu is LoadGameMenu direct)
            return direct;
        if (Game1.activeClickableMenu is TitleMenu && TitleMenu.subMenu is LoadGameMenu sub)
            return sub;
        return null;
    }

    public static Rectangle GetExportButtonBounds(LoadGameMenu menu, int buttonIndex)
    {
        if (buttonIndex < 0 || buttonIndex >= menu.deleteButtons.Count)
            return Rectangle.Empty;

        var delBtn = menu.deleteButtons[buttonIndex];
        int x = delBtn.bounds.X;
        // Posisikan tepat di bawah tombol delete dengan jarak estetis 8px
        int y = delBtn.bounds.Y + delBtn.bounds.Height + 8;
        int w = delBtn.bounds.Width;
        int h = delBtn.bounds.Height;
        return new Rectangle(x, y, w, h);
    }

    private static void EnsureTexture()
    {
        if (_buttonTexture != null || _helper == null) return;
        try
        {
            _buttonTexture = _helper.ModContent.Load<Texture2D>("assets/ExportButton.png");
        }
        catch (Exception ex)
        {
            _monitor?.Log($"[SaveExport] Gagal memuat tekstur assets/ExportButton.png: {ex.Message}", LogLevel.Warn);
        }
    }

    private static void OnRenderedActiveMenu(object? sender, RenderedActiveMenuEventArgs e)
    {
        var loadMenu = GetActiveLoadMenu();
        if (loadMenu == null || loadMenu.deleteConfirmationScreen) return;

        EnsureTexture();
        if (_buttonTexture == null) return;

        _hoveredTooltip = null;
        int mouseX = Game1.getMouseX();
        int mouseY = Game1.getMouseY();

        for (int i = 0; i < loadMenu.deleteButtons.Count; i++)
        {
            int slotIndex = loadMenu.currentItemIndex + i;
            if (slotIndex >= loadMenu.MenuSlots.Count) continue;

            var bounds = GetExportButtonBounds(loadMenu, i);
            if (bounds.IsEmpty) continue;

            bool isHovered = bounds.Contains(mouseX, mouseY);
            Rectangle sourceRect = isHovered 
                ? new Rectangle(18, 0, 18, 18) 
                : new Rectangle(0, 0, 18, 18);

            Vector2 center = new Vector2(bounds.X + bounds.Width / 2f, bounds.Y + bounds.Height / 2f);
            Vector2 origin = new Vector2(9, 9);
            float scale = isHovered ? 2.95f : 2.66f;

            // Render tombol ekspor savegame
            e.SpriteBatch.Draw(
                _buttonTexture,
                center,
                sourceRect,
                Color.White * 0.95f,
                0f,
                origin,
                scale,
                SpriteEffects.None,
                0.89f
            );

            if (isHovered)
            {
                if (loadMenu.MenuSlots[slotIndex] is LoadGameMenu.SaveFileSlot saveSlot && saveSlot.Farmer != null)
                {
                    _hoveredTooltip = $"Ekspor '{saveSlot.Farmer.farmName}' (.zip)";
                }
                else
                {
                    _hoveredTooltip = "Ekspor ke .zip";
                }
            }
        }

        // Tampilkan hover text ringkas ala vanilla Stardew
        if (!string.IsNullOrEmpty(_hoveredTooltip))
        {
            IClickableMenu.drawHoverText(e.SpriteBatch, _hoveredTooltip, Game1.dialogueFont);
        }

        // Pastikan kursor mouse selalu digambar pada layer paling atas (tidak tertutup button atau tooltip)
        loadMenu.drawMouse(e.SpriteBatch);
    }

    private static void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        if (e.Button != SButton.MouseLeft) return;

        var loadMenu = GetActiveLoadMenu();
        if (loadMenu == null || loadMenu.deleteConfirmationScreen) return;

        int mouseX = Game1.getMouseX();
        int mouseY = Game1.getMouseY();

        for (int i = 0; i < loadMenu.deleteButtons.Count; i++)
        {
            int slotIndex = loadMenu.currentItemIndex + i;
            if (slotIndex >= loadMenu.MenuSlots.Count) continue;

            var bounds = GetExportButtonBounds(loadMenu, i);
            if (bounds.Contains(mouseX, mouseY))
            {
                _helper?.Input.Suppress(SButton.MouseLeft);

                if (loadMenu.MenuSlots[slotIndex] is LoadGameMenu.SaveFileSlot saveSlot && saveSlot.Farmer != null)
                {
                    ExportSave(saveSlot.Farmer);
                }
                return;
            }
        }
    }

    private static void ExportSave(Farmer farmer)
    {
        if (string.IsNullOrWhiteSpace(farmer.slotName))
        {
            Game1.playSound("cancel");
            _monitor?.Log("[SaveExport] Nama slot savegame tidak valid.", LogLevel.Warn);
            return;
        }

        try
        {
            string savesDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "StardewValley",
                "Saves"
            );
            string sourceFolder = Path.Combine(savesDir, farmer.slotName);

            if (!Directory.Exists(sourceFolder))
            {
                Game1.playSound("cancel");
                _monitor?.Log($"[SaveExport] Direktori savegame tidak ditemukan: {sourceFolder}", LogLevel.Error);
                return;
            }

            // Tentukan target folder: Prioritas Downloads, Fallback Desktop
            string destDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            if (!Directory.Exists(destDir))
            {
                destDir = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            }

            string zipFileName = $"{farmer.slotName}.zip";
            string targetZipPath = Path.Combine(destDir, zipFileName);

            if (File.Exists(targetZipPath))
            {
                File.Delete(targetZipPath);
            }

            // Buat arsip .zip dengan struktur direktori bawaan menggunakan forward slash '/' untuk kompatibilitas Linux/Cloud
            using (var zipStream = new FileStream(targetZipPath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
            {
                var dirInfo = new DirectoryInfo(sourceFolder);
                foreach (var file in dirInfo.GetFiles("*", SearchOption.AllDirectories))
                {
                    string relPath = Path.GetRelativePath(sourceFolder, file.FullName).Replace('\\', '/');
                    string entryName = $"{farmer.slotName}/{relPath}";
                    archive.CreateEntryFromFile(file.FullName, entryName, CompressionLevel.Optimal);
                }
            }

            Game1.playSound("coin");
            _monitor?.Log($"[SaveExport] Berhasil mengekspor savegame '{farmer.slotName}' -> {targetZipPath}", LogLevel.Info);

            // Buka Windows Explorer langsung memilih berkas .zip
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    Process.Start("explorer.exe", $"/select,\"{targetZipPath}\"");
                }
                else if (OperatingSystem.IsMacOS())
                {
                    Process.Start("open", $"-R \"{targetZipPath}\"");
                }
                else if (OperatingSystem.IsLinux())
                {
                    Process.Start("xdg-open", $"\"{destDir}\"");
                }
            }
            catch (Exception ex)
            {
                _monitor?.Log($"[SaveExport] Gagal membuka explorer: {ex.Message}", LogLevel.Trace);
            }

            // Tampilkan pemberitahuan HUD & pop-up informatif
            Game1.addHUDMessage(new HUDMessage($"Savegame '{farmer.farmName}' diekspor ke: {zipFileName}", HUDMessage.newQuest_type));

            string message = $"Savegame kebun '{farmer.farmName}' ({farmer.Name}) berhasil diekspor ke format .zip!\n\n" +
                             $"Lokasi: {targetZipPath}\n\n" +
                             "Folder Downloads telah dibuka di Explorer. Anda dapat langsung mengunggah berkas ini ke LiteValley Dashboard.";

            if (Game1.activeClickableMenu is TitleMenu)
            {
                TitleMenu.subMenu = new DialogueBox(message);
            }
            else
            {
                Game1.activeClickableMenu = new DialogueBox(message);
            }
        }
        catch (Exception ex)
        {
            Game1.playSound("cancel");
            _monitor?.Log($"[SaveExport] Gagal mengekspor savegame: {ex.Message}", LogLevel.Error);
            Game1.addHUDMessage(new HUDMessage($"Gagal mengekspor savegame: {ex.Message}", HUDMessage.error_type));
        }
    }
}
