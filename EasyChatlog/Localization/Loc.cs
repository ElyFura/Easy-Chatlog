using System;

namespace EasyChatlog.Localization;

/// <summary>
/// Holds the resolved <see cref="Strings"/> table for the currently selected language.
/// Static because the UI code reads it on every ImGui frame.
/// </summary>
public static class Loc
{
    /// <summary>Strings for the active language. Never null.</summary>
    public static Strings S { get; private set; } = Strings.English;

    /// <summary>The setting as stored in the configuration (may be <see cref="Language.Auto"/>).</summary>
    public static Language Selected { get; private set; } = Language.Auto;

    /// <summary>Dalamud's UI language code, used to resolve <see cref="Language.Auto"/>.</summary>
    private static string dalamudLanguage = "en";

    /// <summary>Raised after <see cref="S"/> changed, so callers can refresh cached text.</summary>
    public static event Action? Changed;

    /// <summary>Apply the configured language. <paramref name="uiLanguage"/> is Dalamud's code, e.g. "de".</summary>
    public static void Apply(Language selected, string? uiLanguage = null)
    {
        if (uiLanguage != null)
            dalamudLanguage = uiLanguage;

        Selected = selected;

        var resolved = selected switch
        {
            Language.English => Strings.English,
            Language.German  => Strings.German,
            _ => dalamudLanguage.StartsWith("de", StringComparison.OrdinalIgnoreCase)
                ? Strings.German
                : Strings.English,
        };

        if (ReferenceEquals(resolved, S)) return;

        S = resolved;
        Changed?.Invoke();
    }

    /// <summary>Re-resolve after Dalamud's own UI language changed. No-op unless the setting is Auto.</summary>
    public static void OnDalamudLanguageChanged(string uiLanguage) => Apply(Selected, uiLanguage);
}
