using StardewModdingAPI;

namespace StardewBridge.Client;

/// <summary>
/// Helper terpusat untuk mengakses translasi SMAPI (i18n) dengan dukungan token dinamis.
/// </summary>
public static class I18n
{
    private static ITranslationHelper? _translations;

    public static void Initialize(ITranslationHelper translations)
    {
        _translations = translations;
    }

    /// <summary>
    /// Mengambil string translasi berdasarkan kunci.
    /// </summary>
    public static string Get(string key)
    {
        if (_translations == null) return key;
        Translation translation = _translations.Get(key);
        return translation.HasValue() ? translation.ToString() : key;
    }

    /// <summary>
    /// Mengambil string translasi berdasarkan kunci dengan argumen token dinamis.
    /// Contoh: I18n.Get("hud.profession.assigned", new { profession = "Miner" })
    /// </summary>
    public static string Get(string key, object tokens)
    {
        if (_translations == null) return key;
        Translation translation = _translations.Get(key, tokens);
        return translation.HasValue() ? translation.ToString() : key;
    }
}
