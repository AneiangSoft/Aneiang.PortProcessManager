using System.Globalization;

namespace PortProcessManager.Localization;

public static class LanguageManager
{
    public const string CultureZhCn = "zh-CN";
    public const string CultureEnUs = "en-US";

    public static string NormalizeCultureName(string? cultureName)
    {
        if (string.IsNullOrWhiteSpace(cultureName)) return CultureZhCn;

        // 允许传入 zh / en
        if (cultureName.Equals("zh", System.StringComparison.OrdinalIgnoreCase)) return CultureZhCn;
        if (cultureName.Equals("en", System.StringComparison.OrdinalIgnoreCase)) return CultureEnUs;

        // 允许传入 zh-CN/en-US
        _ = CultureInfo.GetCultureInfo(cultureName);
        return cultureName;
    }

    public static void ApplyAndPersist(string cultureName)
    {
        var normalized = NormalizeCultureName(cultureName);
        LocalizationManager.Instance.SetCulture(normalized);
        LanguageSettings.SaveCultureName(normalized);
    }
}
