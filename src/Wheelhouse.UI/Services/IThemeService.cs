namespace Wheelhouse.UI.Services;

public enum AppTheme { Light, Dark, System }

public interface IThemeService
{
    AppTheme CurrentTheme { get; }
    void Initialize();
    void SetTheme(AppTheme theme);
    event EventHandler<AppTheme> ThemeChanged;
}
