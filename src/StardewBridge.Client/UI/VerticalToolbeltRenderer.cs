using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewBridge.Client.Tools;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;

namespace StardewBridge.Client.UI;

/// <summary>
/// Merender UI Vertical Dedicated Toolbelt (10 Slot) di pojok kiri-tengah layar.
/// Menggunakan tekstur kotak slot bawaan game (Game1.menuTexture) sehingga menyatu 100% dengan UI vanilla.
/// </summary>
public static class VerticalToolbeltRenderer
{
    private static IMonitor? _monitor;
    private static IModHelper? _helper;
    private static float _transparency = 1f;
    private static Item? _hoverItem;

    // Dimensi dan posisi UI
    public const int BoxWidth = 96;
    public const int BoxHeight = 672; // 10 * 64 + 32 padding
    public const int SlotSize = 64;

    public static int GetBoxX() => 16;
    public static int GetBoxY() => Math.Max(16, (Game1.uiViewport.Height - BoxHeight) / 2);

    public static Rectangle GetBadgeBounds()
    {
        int boxX = GetBoxX();
        int boxY = GetBoxY();
        return new Rectangle(boxX, boxY - 26, BoxWidth, 22);
    }

    public static Rectangle GetSlotBounds(int slotIndex)
    {
        int boxX = GetBoxX();
        int boxY = GetBoxY();
        return new Rectangle(boxX + 16, boxY + 16 + slotIndex * SlotSize, SlotSize, SlotSize);
    }

    /// <summary>
    /// Menghitung offset X dinamis untuk notifikasi HUD (HUDMessage item grab / info / quest)
    /// dan ChatBox agar bergeser maju ke sebelah kanan Dedicated Toolbelt (tidak tertutup slot tools).
    /// </summary>
    public static int GetHudNotificationXOffset()
    {
        // BoxX (16) + BoxWidth (96) + Margin (16) = 128
        return GetBoxX() + BoxWidth + 16;
    }

    public static void Initialize(IMonitor monitor, IModHelper helper)
    {
        _monitor = monitor;
        _helper = helper;
        helper.Events.Display.RenderedHud += OnRenderedHud;
        helper.Events.Display.RenderedActiveMenu += OnRenderedActiveMenu;
        helper.Events.Input.ButtonPressed += OnButtonPressed;
    }

    private static void OnRenderedHud(object? sender, RenderedHudEventArgs e)
    {
        if (Game1.activeClickableMenu != null || Game1.eventUp) return;

        ToolbeltManager.EnsureCapacity(Game1.player);
        DrawToolbelt(e.SpriteBatch, isMenuMode: false);

        if (_hoverItem != null)
        {
            IClickableMenu.drawToolTip(e.SpriteBatch, _hoverItem.getDescription(), _hoverItem.DisplayName, _hoverItem);
            _hoverItem = null;
        }
    }

    private static void OnRenderedActiveMenu(object? sender, RenderedActiveMenuEventArgs e)
    {
        // Tampilkan juga saat membuka menu inventori (GameMenu) agar pemain bisa menata slot toolbelt
        if (Game1.activeClickableMenu is GameMenu gameMenu && gameMenu.currentTab == 0)
        {
            ToolbeltManager.EnsureCapacity(Game1.player);
            DrawToolbelt(e.SpriteBatch, isMenuMode: true);

            if (_hoverItem != null)
            {
                IClickableMenu.drawToolTip(e.SpriteBatch, _hoverItem.getDescription(), _hoverItem.DisplayName, _hoverItem);
                _hoverItem = null;
            }
        }
    }

    private static void DrawToolbelt(SpriteBatch b, bool isMenuMode)
    {
        int boxX = GetBoxX();
        int boxY = GetBoxY();

        // 1. Kalkulasi transparansi dinamis saat bermain di HUD
        if (!isMenuMode && Game1.player != null)
        {
            Point standingPixel = Game1.player.StandingPixel;
            Vector2 playerLocalPos = Game1.GlobalToLocal(new Vector2(standingPixel.X, standingPixel.Y));

            if (playerLocalPos.X < 140 && playerLocalPos.Y >= boxY - 32 && playerLocalPos.Y <= boxY + BoxHeight + 32)
            {
                _transparency = Math.Max(0.33f, _transparency - 0.15f);
            }
            else
            {
                _transparency = Math.Min(1f, _transparency + 0.075f);
            }
        }
        else
        {
            _transparency = 1f;
        }

        // 2. Gambar background box menggunakan menuTexture vanilla
        IClickableMenu.drawTextureBox(
            b,
            Game1.menuTexture,
            new Rectangle(0, 256, 60, 60),
            boxX,
            boxY,
            BoxWidth,
            BoxHeight,
            Color.White * _transparency,
            1f,
            drawShadow: false
        );

        // 3. Gambar status badge SmartTools di atas box
        string stText = SmartToolsEngine.IsEnabled ? I18n.Get("ui.smarttools.badge.on") : I18n.Get("ui.smarttools.badge.off");
        Color stColor = SmartToolsEngine.IsEnabled ? Color.LightGreen : Color.OrangeRed;
        b.DrawString(Game1.tinyFont, stText, new Vector2(boxX + 18, boxY - 22), stColor * _transparency);

        // 4. Gambar 10 kotak slot dan item di dalamnya
        int mouseX = Game1.getOldMouseX();
        int mouseY = Game1.getOldMouseY();

        for (int i = 0; i < ToolbeltManager.ToolbeltSlotCount; i++)
        {
            int inventoryIndex = ToolbeltManager.ToolbeltStartIndex + i;
            bool isSelected = Game1.player?.CurrentToolIndex == inventoryIndex;
            var slotBounds = GetSlotBounds(i);
            Vector2 slotPos = new Vector2(slotBounds.X, slotBounds.Y);

            // Kotak slot: tile 56 = bingkai merah terpilih, tile 10 = kotak normal
            b.Draw(
                Game1.menuTexture,
                slotPos,
                Game1.getSourceRectForStandardTileSheet(Game1.menuTexture, isSelected ? 56 : 10),
                Color.White * _transparency
            );

            // Item di dalam slot
            if (Game1.player != null && Game1.player.Items.Count > inventoryIndex && Game1.player.Items[inventoryIndex] != null)
            {
                var tool = Game1.player.Items[inventoryIndex];
                tool.drawInMenu(b, slotPos, isSelected ? 0.9f : 0.8f, _transparency, 0.88f);

                // Hover check untuk tooltip
                if (slotBounds.Contains(mouseX, mouseY))
                {
                    _hoverItem = tool;
                }
            }
        }
    }

    private static void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        if (!Context.IsWorldReady || Game1.player == null) return;
        if (e.Button != SButton.MouseLeft && e.Button != SButton.MouseRight) return;

        int mouseX = Game1.getOldMouseX();
        int mouseY = Game1.getOldMouseY();

        // 1. Cek klik pada badge SmartTools [ST: ON/OFF]
        if (GetBadgeBounds().Contains(mouseX, mouseY))
        {
            if (e.Button == SButton.MouseLeft)
            {
                _helper?.Input.Suppress(e.Button);
                SmartToolsEngine.Toggle();
            }
            return;
        }

        // 2. Cek klik pada salah satu dari 10 slot Toolbelt
        for (int i = 0; i < ToolbeltManager.ToolbeltSlotCount; i++)
        {
            var bounds = GetSlotBounds(i);
            if (!bounds.Contains(mouseX, mouseY)) continue;

            _helper?.Input.Suppress(e.Button);
            int inventoryIndex = ToolbeltManager.ToolbeltStartIndex + i;
            ToolbeltManager.EnsureCapacity(Game1.player);

            if (Game1.activeClickableMenu == null)
            {
                if (e.Button == SButton.MouseLeft)
                {
                    // Mode HUD: langsung pilih alat di slot ini
                    Game1.player.CurrentToolIndex = inventoryIndex;
                    if (Game1.player.ActiveObject != null)
                    {
                        Game1.player.showCarrying();
                        Game1.playSound("pickUpItem");
                    }
                    else
                    {
                        Game1.player.showNotCarrying();
                        Game1.playSound("stoneStep");
                    }
                }
                return;
            }
            else if (Game1.activeClickableMenu is GameMenu gameMenu && gameMenu.currentTab == 0)
            {
                // Mode Inventory Menu: klik kanan pasang umpan / tackle pada pancingan
                if (e.Button == SButton.MouseRight)
                {
                    if (Game1.player.CursorSlotItem is StardewValley.Object baitObj && Game1.player.Items[inventoryIndex] is StardewValley.Tools.FishingRod rod)
                    {
                        var leftover = rod.attach(baitObj);
                        Game1.player.CursorSlotItem = leftover;
                        Game1.playSound("dwop");
                    }
                    return;
                }

                // Mode Inventory Menu: swap / place / pick (MouseLeft)
                if (Game1.player.CursorSlotItem != null)
                {
                    if (ToolbeltManager.IsToolbeltEligible(Game1.player.CursorSlotItem))
                    {
                        // Swap tool di kursor dengan slot toolbelt
                        var temp = Game1.player.Items[inventoryIndex];
                        Game1.player.Items[inventoryIndex] = Game1.player.CursorSlotItem;
                        Game1.player.CursorSlotItem = temp;
                        Game1.playSound("dwop");
                    }
                    else
                    {
                        // Tolak item non-tool
                        Game1.playSound("cancel");
                        Game1.showRedMessage("Slot ini khusus untuk Tools & Senjata!");
                    }
                }
                else if (Game1.player.Items[inventoryIndex] != null)
                {
                    // Ambil tool ke kursor
                    Game1.player.CursorSlotItem = Game1.player.Items[inventoryIndex];
                    Game1.player.Items[inventoryIndex] = null;
                    Game1.playSound("dwop");
                }
                return;
            }
        }
    }
}
