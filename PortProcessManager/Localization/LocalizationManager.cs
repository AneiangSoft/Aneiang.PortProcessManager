using System;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using PortProcessManager.Resources;

namespace PortProcessManager.Localization;

/// <summary>
/// 提供可动态刷新的资源访问器：XAML 通过 Binding 绑定到这里，切换语言时触发 PropertyChanged("Item[]") 即可刷新所有字符串。
/// </summary>
public sealed class LocalizationManager : INotifyPropertyChanged
{
    public static LocalizationManager Instance { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    private LocalizationManager() { }

    public string this[string key]
    {
        get
        {
            // Labels 为 resx 自动生成的强类型资源类
            // 这里用 ResourceManager 以支持动态 key
            return Labels.ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? $"!{key}!";
        }
    }

    public CultureInfo CurrentUICulture => CultureInfo.CurrentUICulture;

    public void SetCulture(string cultureName)
    {
        var culture = CultureInfo.GetCultureInfo(cultureName);
        if (Equals(CultureInfo.CurrentUICulture, culture)) return;

        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;

        // 强类型资源类也有 Culture 属性（internal set），这里同步一下，避免某些访问路径不刷新
        Labels.Culture = culture;

        OnPropertyChanged("Item[]");
        OnPropertyChanged(nameof(CurrentUICulture));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
