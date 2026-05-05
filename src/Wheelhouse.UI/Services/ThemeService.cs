using Microsoft.Win32;
using System.Windows;
using Wheelhouse.Core.Services;

namespace Wheelhouse.UI.Services;

public sealed class ThemeService : IThemeService
{
    private readonly ISettingsService _settings;

    public AppTheme CurrentTheme { get; private set; } = AppTheme.System;
    public event EventHandler<AppTheme>? ThemeChanged;

    public ThemeService(ISettingsService settings)
    {
        _settings = settings;
    }

    public void Initialize()
    {
        var stored = _settings.Current.Theme;
        var theme = Enum.TryParse<AppTheme>(stored, out var parsed) ? parsed : AppTheme.System;
        SetTheme(theme);
    }

    public void SetTheme(AppTheme theme)
    {
        CurrentTheme = theme;
        var resolved = theme == AppTheme.System ? DetectSystemTheme() : theme;
        ApplyThemeResources(resolved);
        ThemeChanged?.Invoke(this, theme);
    }

    private static AppTheme DetectSystemTheme()
    {
        try
        {
            var value = Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "AppsUseLightTheme", 1);
            return value is 0 ? AppTheme.Dark : AppTheme.Light;
        }
        catch
        {
            return AppTheme.Light;
        }
    }

    private static void ApplyThemeResources(AppTheme theme)
    {
        var dict = Application.Current.Resources.MergedDictionaries;
        var themeSource = theme == AppTheme.Dark
            ? new Uri("Themes/Dark.xaml", UriKind.Relative)
            : new Uri("Themes/Light.xaml", UriKind.Relative);

        var existing = dict.FirstOrDefault(d => d.Source?.OriginalString.Contains("Themes/Light") == true
                                             || d.Source?.OriginalString.Contains("Themes/Dark") == true);
        if (existing is not null) dict.Remove(existing);
        dict.Add(new ResourceDictionary { Source = themeSource });
    }
}
