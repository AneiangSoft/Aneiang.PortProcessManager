using System;
using System.IO;

namespace PortProcessManager.Localization;

/// <summary>
/// 极简语言设置持久化：写入到 %LocalAppData%\PortProcessManager\settings.txt
/// </summary>
public static class LanguageSettings
{
    private static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PortProcessManager");

    private static readonly string SettingsFile = Path.Combine(SettingsDir, "settings.txt");

    private const string Key = "UICulture";

    public static string? LoadCultureName()
    {
        try
        {
            if (!File.Exists(SettingsFile)) return null;
            foreach (var line in File.ReadAllLines(SettingsFile))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("#") || trimmed.Length == 0) continue;
                var parts = trimmed.Split('=', 2, StringSplitOptions.TrimEntries);
                if (parts.Length == 2 && string.Equals(parts[0], Key, StringComparison.OrdinalIgnoreCase))
                    return parts[1];
            }
        }
        catch
        {
            // ignore
        }

        return null;
    }

    public static void SaveCultureName(string cultureName)
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            File.WriteAllText(SettingsFile, $"{Key}={cultureName}{Environment.NewLine}");
        }
        catch
        {
            // ignore
        }
    }
}
