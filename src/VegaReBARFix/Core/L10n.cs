namespace VegaReBARFix.Core;

using System.Globalization;
using Microsoft.Win32;

/// <summary>
/// UI language: "en" or "ru". Priority: the choice saved in the registry,
/// then the Windows display language (Russian/Cyrillic systems get Russian),
/// then English.
/// </summary>
public static class L10n
{
    public const string AppKey = @"Software\VegaReBARFix";

    public static string Lang { get; private set; } = "en";

    public static void Initialize()
    {
        var saved = Registry.CurrentUser.OpenSubKey(AppKey)?.GetValue("Language") as string;
        if (saved is "en" or "ru")
        {
            Lang = saved;
            return;
        }
        try
        {
            Lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ru" ? "ru" : "en";
        }
        catch
        {
            Lang = "en";
        }
    }

    /// <summary>Switches the language and remembers the choice in HKCU.</summary>
    public static void Set(string lang)
    {
        Lang = lang == "ru" ? "ru" : "en";
        using var k = Registry.CurrentUser.CreateSubKey(AppKey);
        k.SetValue("Language", Lang, RegistryValueKind.String);
    }

    /// <summary>Picks a string by current language: T("English", "Русский").</summary>
    public static string T(string en, string ru) => Lang == "ru" ? ru : en;
}
