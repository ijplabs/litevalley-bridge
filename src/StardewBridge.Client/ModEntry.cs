using HarmonyLib;
using StardewBridge.Client.Tools;
using StardewBridge.Client.UI;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Network;

namespace StardewBridge.Client;

public class ModEntry : Mod
{
    public static ModEntry? Instance { get; private set; }
    private Harmony? _harmony;
    private string? _autoConnectTarget;
    private bool _hasTriggeredAutoConnect;
    private static int _frozenTicksCount = 0;

    public override void Entry(IModHelper helper)
    {
        Instance = this;

        // Tribute to Eric Barone & License Compliance
        this.Monitor.Log("LiteValley Bridge Client aktif. Tribute to ConcernedApe (Eric Barone) - Menjaga ekosistem resmi Stardew Valley.", LogLevel.Info);

        // 0. Inisialisasi sistem Internationalization (i18n)
        I18n.Initialize(helper.Translation);

        _harmony = new Harmony(this.ModManifest.UniqueID);

        // 1. Inisialisasi modul Toolbelt dan SmartTools
        ToolbeltManager.Initialize(this.Monitor);
        SmartToolsEngine.Initialize(this.Monitor);
        VerticalToolbeltRenderer.Initialize(this.Monitor, helper);

        // 2. Terapkan seluruh Harmony Patches
        ClientPatches.Apply(_harmony);

        // 3. Inisialisasi Bridge Core
        BridgeClientCore.Initialize(this.Monitor, helper);

        // 4. Daftarkan event game loop, input & content
        helper.Events.Input.ButtonPressed += OnButtonPressed;
        helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        helper.Events.GameLoop.DayStarted += OnDayStarted;
        helper.Events.Content.AssetRequested += OnAssetRequested;
        LocalizedContentManager.OnLanguageChange += OnLanguageChanged;

        // 5. Daftarkan perintah konsol
        helper.ConsoleCommands.Add("bug", I18n.Get("command.bug.desc"), OnBugCommand);
        helper.ConsoleCommands.Add("bridge", I18n.Get("command.bridge.desc"), OnBridgeCommand);
        helper.ConsoleCommands.Add("connect", I18n.Get("command.connect.desc"), OnConnectCommand);
        helper.ConsoleCommands.Add("profession", I18n.Get("command.profession.desc"), OnProfessionCommand);
        helper.ConsoleCommands.Add("treasury", I18n.Get("command.treasury.desc"), OnTreasuryCommand);
        helper.ConsoleCommands.Add("donate", I18n.Get("command.donate.desc"), OnDonateCommand);
        helper.ConsoleCommands.Add("login", I18n.Get("command.login.desc"), OnLoginCommand);
        helper.ConsoleCommands.Add("register", I18n.Get("command.register.desc"), OnRegisterCommand);
        helper.ConsoleCommands.Add("auth", I18n.Get("command.auth.desc"), OnAuthCommand);
        helper.ConsoleCommands.Add("smarttools", I18n.Get("command.smarttools.desc"), OnSmartToolsCommand);
        helper.ConsoleCommands.Add("unfreeze", I18n.Get("command.unfreeze.desc"), OnUnfreezeCommand);

        // Periksa apakah game diluncurkan dengan argumen --connect <ip:port>
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].Equals("--connect", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                _autoConnectTarget = args[i + 1];
                this.Monitor.Log($"[StardewBridge] Terdeteksi argumen --connect: '{_autoConnectTarget}'. Akan otomatis menghubungkan saat TitleMenu siap.", LogLevel.Info);
                break;
            }
        }

        this.Monitor.Log("StardewBridge Client loaded successfully. SmartTools & Dedicated Toolbelt aktif!", LogLevel.Info);
    }

    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        ToolbeltManager.MigrateStarterToolsToToolbelt(Game1.player);
    }

    private void OnDayStarted(object? sender, DayStartedEventArgs e)
    {
        ToolbeltManager.MigrateStarterToolsToToolbelt(Game1.player);
    }

    private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        if (e.Button == SButton.F9)
        {
            this.Monitor.Log("[StardewBridge] Tombol F9 ditekan. Mengambil snapshot bug report...", LogLevel.Info);
            BridgeClientCore.CaptureBugReport("F9 Manual Capture");
        }
        else if (e.Button == SButton.F8)
        {
            if (Game1.player != null)
            {
                Game1.player.forceCanMove();
                Game1.playSound("drumkit6");
                Game1.addHUDMessage(new HUDMessage(I18n.Get("hud.antifreeze.restored"), HUDMessage.newQuest_type));
                this.Monitor.Log("[AntiFreeze] Manual Force Unfreeze dipicu via tombol F8.", LogLevel.Info);
            }
        }
        else if (e.Button == SButton.Escape)
        {
            // Jika pemain terjebak dalam deadlock kontrol (CanMove == false / UsingTool == true) saat tidak ada menu & bukan event,
            // pulihkan kontrol pemain agar ESC dapat berfungsi membuka GameMenu secara normal
            if (Game1.activeClickableMenu == null && !Game1.eventUp && Game1.player != null)
            {
                if (!Game1.player.CanMove || Game1.player.UsingTool)
                {
                    Game1.player.forceCanMove();
                    this.Monitor.Log("[AntiFreeze] Memulihkan kontrol pemain melalui penekanan tombol ESC.", LogLevel.Info);
                }
            }
        }
    }

    private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        BridgeClientCore.OnUpdateTicked();

        // Control Deadlock Watchdog (Anti-Freeze Guard)
        if (Context.IsWorldReady && Game1.player != null)
        {
            var player = Game1.player;
            bool isControlLocked = (!player.CanMove || player.UsingTool) &&
                                   (player.FarmerSprite == null || (!player.FarmerSprite.PauseForSingleAnimation)) &&
                                   Game1.activeClickableMenu == null &&
                                   !Game1.eventUp &&
                                   player.freezePause <= 0;

            if (isControlLocked)
            {
                _frozenTicksCount++;
                if (_frozenTicksCount >= 20) // ~0.33 detik macet tanpa ada animasi/menu/event yang membenarkannya
                {
                    _frozenTicksCount = 0;
                    player.forceCanMove();
                    this.Monitor.Log("[AntiFreeze] Terdeteksi kontrol pemain macet (CanMove=false/UsingTool=true tanpa animasi). Memulihkan pergerakan otomatis!", LogLevel.Warn);
                }
            }
            else
            {
                _frozenTicksCount = 0;
            }
        }

        if (!string.IsNullOrEmpty(_autoConnectTarget) && !_hasTriggeredAutoConnect)
        {
            if (Game1.activeClickableMenu is TitleMenu && TitleMenu.subMenu == null)
            {
                _hasTriggeredAutoConnect = true;
                ConnectToServer(_autoConnectTarget);
            }
        }
    }

    private void ConnectToServer(string address)
    {
        this.Monitor.Log($"[StardewBridge] Memulai direct connection ke {address}...", LogLevel.Info);
        try
        {
            TitleMenu.subMenu = new FarmhandMenu(Game1.Multiplayer.InitClient(new LidgrenClient(address)));
        }
        catch (Exception ex)
        {
            this.Monitor.Log($"[StardewBridge] Gagal direct connect ke {address}: {ex.Message}", LogLevel.Error);
        }
    }

    private void OnSmartToolsCommand(string command, string[] args)
    {
        string sub = args.Length > 0 ? args[0].ToLowerInvariant() : "toggle";
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

        string statusText = SmartToolsEngine.IsEnabled ? I18n.Get("ui.smarttools.badge.on") : I18n.Get("ui.smarttools.badge.off");
        this.Monitor.Log(I18n.Get("command.smarttools.status", new { status = statusText }), LogLevel.Info);
    }

    private void OnConnectCommand(string command, string[] args)
    {
        var target = args.Length > 0 ? args[0] : "127.0.0.1:24642";
        ConnectToServer(target);
    }

    private void OnBugCommand(string command, string[] args)
    {
        var note = args.Length > 0 ? string.Join(" ", args) : "Manual console bug report";
        BridgeClientCore.CaptureBugReport(note);
    }

    private void OnBridgeCommand(string command, string[] args)
    {
        if (BridgeClientCore.IsConnectedToHeadless)
        {
            var info = BridgeClientCore.LastServerWelcome;
            this.Monitor.Log(I18n.Get("command.bridge.connected", new { server = info?.ServerName, year = info?.Year, season = info?.Season, day = info?.DayOfMonth, time = info?.TimeOfDay }), LogLevel.Info);
        }
        else
        {
            this.Monitor.Log(I18n.Get("command.bridge.disconnected"), LogLevel.Warn);
        }
    }

    private void OnProfessionCommand(string command, string[] args)
    {
        if (args.Length == 0)
        {
            var prof = BridgeClientCore.CurrentProfession;
            if (string.IsNullOrEmpty(prof) || prof.Equals("None", StringComparison.OrdinalIgnoreCase))
            {
                this.Monitor.Log(I18n.Get("command.profession.none"), LogLevel.Info);
            }
            else
            {
                this.Monitor.Log(I18n.Get("command.profession.active", new { profession = prof.ToUpper() }), LogLevel.Info);
            }
            return;
        }

        var requestedProf = args[0].Trim().ToLowerInvariant();
        var validProfessions = new[] { "farmer", "miner", "fisherman", "rancher", "forager" };
        if (!validProfessions.Contains(requestedProf))
        {
            this.Monitor.Log(I18n.Get("command.profession.invalid", new { profession = requestedProf }), LogLevel.Warn);
            return;
        }

        BridgeClientCore.SelectProfession(requestedProf);
    }

    private void OnTreasuryCommand(string command, string[] args)
    {
        this.Monitor.Log(I18n.Get("command.treasury.balance", new { balance = BridgeClientCore.VillageTreasury }), LogLevel.Info);
    }

    private void OnDonateCommand(string command, string[] args)
    {
        if (args.Length == 0 || !int.TryParse(args[0], out int amount) || amount <= 0)
        {
            this.Monitor.Log(I18n.Get("command.donate.usage"), LogLevel.Warn);
            return;
        }

        BridgeClientCore.DonateToTreasury(amount);
    }

    private void OnLoginCommand(string command, string[] args)
    {
        if (args.Length == 0)
        {
            this.Monitor.Log(I18n.Get("command.login.usage"), LogLevel.Warn);
            return;
        }

        BridgeClientCore.SubmitAuth(args[0]);
    }

    private void OnRegisterCommand(string command, string[] args)
    {
        if (args.Length < 2)
        {
            this.Monitor.Log(I18n.Get("command.register.usage"), LogLevel.Warn);
            return;
        }

        BridgeClientCore.SubmitAuth(args[0], args[1]);
    }

    private void OnAuthCommand(string command, string[] args)
    {
        if (args.Length == 0)
        {
            this.Monitor.Log(I18n.Get("command.auth.usage"), LogLevel.Warn);
            return;
        }

        var password = args[0];
        var prof = args.Length > 1 ? args[1] : null;
        BridgeClientCore.SubmitAuth(password, prof);
    }

    private void OnUnfreezeCommand(string command, string[] args)
    {
        if (Game1.player != null)
        {
            Game1.player.forceCanMove();
            this.Monitor.Log(I18n.Get("command.unfreeze.success"), LogLevel.Info);
        }
    }

    public void InvalidateLanguageCaches()
    {
        try
        {
            this.Helper.GameContent.InvalidateCache("Minigames/TitleButtons");
            this.Helper.GameContent.InvalidateCache("Data/Tools");
            this.Helper.GameContent.InvalidateCache(asset => asset.NameWithoutLocale.StartsWith("Strings/"));
        }
        catch (Exception ex)
        {
            this.Monitor.Log($"[i18n] Gagal menginvalidasi cache bahasa: {ex.Message}", LogLevel.Warn);
        }
    }

    private void OnLanguageChanged(LocalizedContentManager.LanguageCode code)
    {
        InvalidateLanguageCaches();
    }

    public static bool IsIndonesianActive()
    {
        return LocalizedContentManager.CurrentLanguageCode == LocalizedContentManager.LanguageCode.mod &&
               (LocalizedContentManager.CurrentModLanguage?.LanguageCode?.Equals("id", StringComparison.OrdinalIgnoreCase) == true ||
                LocalizedContentManager.CurrentModLanguage?.Id?.Equals("Indonesian", StringComparison.OrdinalIgnoreCase) == true);
    }

    private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        // 1. Registrasi Bahasa Indonesia resmi ke engine Stardew Valley 1.6
        if (e.NameWithoutLocale.IsEquivalentTo("Data/AdditionalLanguages"))
        {
            e.Edit(asset =>
            {
                var list = asset.GetData<List<StardewValley.GameData.ModLanguage>>();
                if (list != null && !list.Any(lang => lang.Id.Equals("Indonesian", StringComparison.OrdinalIgnoreCase) || lang.LanguageCode.Equals("id", StringComparison.OrdinalIgnoreCase)))
                {
                    list.Add(new StardewValley.GameData.ModLanguage
                    {
                        Id = "Indonesian",
                        LanguageCode = "id",
                        ButtonTexture = "Mods/IJP.StardewBridge/LanguageButton",
                        UseLatinFont = true,
                        TimeFormat = "[HOURS_24]:[MINUTES]",
                        ClockTimeFormat = "[HOURS_24]:[MINUTES]",
                        ClockDateFormat = "[DAY_OF_WEEK], [DAY_OF_MONTH]",
                        NumberComma = "."
                    });
                    this.Monitor.Log("[i18n] Bahasa Indonesia berhasil didaftarkan ke Data/AdditionalLanguages (Stardew Valley 1.6)", LogLevel.Info);
                }
            });
        }
        // 2. Sediakan aset grafis tombol Bahasa Indonesia (174x78) untuk menu pilihan bahasa
        else if (e.NameWithoutLocale.IsEquivalentTo("Mods/IJP.StardewBridge/LanguageButton"))
        {
            e.LoadFromModFile<Microsoft.Xna.Framework.Graphics.Texture2D>("assets/LanguageButton.png", AssetLoadPriority.Medium);
        }
        // 3. Modifikasi tombol kayu Title Screen (BARU, LANJUT, MABAR, KELUAR, BALIK)
        else if (e.NameWithoutLocale.IsEquivalentTo("Minigames/TitleButtons"))
        {
            if (IsIndonesianActive())
            {
                e.Edit(asset =>
                {
                    var editor = asset.AsImage();
                    var titleButtonsTex = this.Helper.ModContent.Load<Microsoft.Xna.Framework.Graphics.Texture2D>("assets/TitleButtons.png");
                    editor.PatchImage(titleButtonsTex, patchMode: StardewModdingAPI.PatchMode.Overlay);
                }, AssetEditPriority.Late);
            }
        }
        // 4. Injeksi deskripsi Tool kustom (seperti Return Scepter)
        else if (e.NameWithoutLocale.IsEquivalentTo("Data/Tools"))
        {
            if (IsIndonesianActive())
            {
                e.Edit(asset =>
                {
                    var dict = asset.AsDictionary<string, StardewValley.GameData.Tools.ToolData>().Data;
                    if (dict.TryGetValue("ReturnScepter", out var wand))
                    {
                        wand.Description = "Gagang emasnya bergetar dengan potensi kekuatan murni. Angkat tongkat ini ke langit untuk pulang ke rumah kapan saja.";
                    }
                }, AssetEditPriority.Late);
            }
        }
        // 5. Injeksi string permainan komprehensif (UI, StringsFromCSFiles, Locations, Objects, dll.)
        else if (e.NameWithoutLocale.StartsWith("Strings/"))
        {
            if (IsIndonesianActive())
            {
                string subName = e.NameWithoutLocale.Name.Substring("Strings/".Length);
                string relativePath = $"i18n/game/{subName}.id.json";
                string fullPath = Path.Combine(this.Helper.DirectoryPath, relativePath);

                if (File.Exists(fullPath))
                {
                    e.Edit(asset =>
                    {
                        var dict = asset.AsDictionary<string, string>().Data;
                        var translations = this.Helper.ModContent.Load<Dictionary<string, string>>(relativePath);
                        foreach (var kvp in translations)
                        {
                            dict[kvp.Key] = kvp.Value;
                        }
                    }, AssetEditPriority.Late);
                }
            }
        }
    }
}
