using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows;
using Wheelhouse.Core.Services;
using Wheelhouse.UI.Services;

namespace Wheelhouse.UI.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    private readonly IRepositoryService _repositoryService;
    private readonly ISettingsService _settingsService;
    private readonly IThemeService _themeService;

    [ObservableProperty] private string _title = "Wheelhouse";
    [ObservableProperty] private string _statusText = "Ready";
    [ObservableProperty] private bool _isTerminalVisible = false;
    [ObservableProperty] private AppTheme _currentTheme;

    public MainWindowViewModel(
        IRepositoryService repositoryService,
        ISettingsService settingsService,
        IThemeService themeService)
    {
        _repositoryService = repositoryService;
        _settingsService = settingsService;
        _themeService = themeService;

        CurrentTheme = _themeService.CurrentTheme;
        _themeService.ThemeChanged += (_, theme) => CurrentTheme = theme;
    }

    [RelayCommand]
    private void ToggleTerminal() => IsTerminalVisible = !IsTerminalVisible;

    [RelayCommand]
    private void SetTheme(string themeName)
    {
        if (Enum.TryParse<AppTheme>(themeName, out var theme))
        {
            _themeService.SetTheme(theme);
            _settingsService.Update(s => s.Theme = themeName);
        }
    }

    [RelayCommand]
    private async Task OpenRepository()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select a Git repository folder"
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            StatusText = "Opening repository...";
            _repositoryService.Open(dialog.FolderName);
            Title = $"{_repositoryService.CurrentRepository!.Name} — Wheelhouse";
            StatusText = "Ready";
        }
        catch (Exception ex)
        {
            StatusText = "Failed to open repository";
            MessageBox.Show($"Could not open repository:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
