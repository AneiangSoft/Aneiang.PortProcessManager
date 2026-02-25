using System.Globalization;
using System.Windows;
using PortProcessManager.Localization;

namespace PortProcessManager;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 默认：跟随系统；如果用户曾手动选择过，则以用户设置为准
        var saved = LanguageSettings.LoadCultureName();
        if (!string.IsNullOrWhiteSpace(saved))
        {
            try
            {
                LocalizationManager.Instance.SetCulture(saved);
            }
            catch (CultureNotFoundException)
            {
                // ignore
            }
        }
    }
}
