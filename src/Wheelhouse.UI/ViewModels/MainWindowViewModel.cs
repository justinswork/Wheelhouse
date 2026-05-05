using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System.Windows;
using Wheelhouse.Core.Services;
using Wheelhouse.UI.Messages;
using Wheelhouse.UI.Services;

namespace Wheelhouse.UI.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    private readonly IRepositoryService _repositoryService;
    private readonly ISettingsService _settingsService;
    private readonly IThemeService _themeService;

    public WorkingTreeViewModel WorkingTreeViewModel { get; }
    public LogViewModel LogViewModel { get; }
    public DiffViewModel DiffViewModel { get; }
    public RepositorySidebarViewModel SidebarViewModel { get; }

    [ObservableProperty] private string _title = "Wheelhouse";
    [ObservableProperty] private string _statusText = "Ready";
    [ObservableProperty] private bool _isTerminalVisible = false;
    [ObservableProperty] private AppTheme _currentTheme;

    public MainWindowViewModel(
        IRepositoryService repositoryService,
        ISettingsService settingsService,
        IThemeService themeService,
        WorkingTreeViewModel workingTreeViewModel,
        LogViewModel logViewModel,
        DiffViewModel diffViewModel,
        RepositorySidebarViewModel sidebarViewModel)
    {
        _repositoryService = repositoryService;
        _settingsService = settingsService;
        _themeService = themeService;

        WorkingTreeViewModel = workingTreeViewModel;
        LogViewModel = logViewModel;
        DiffViewModel = diffViewModel;
        SidebarViewModel = sidebarViewModel;

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

            var repoInfo = _repositoryService.CurrentRepository!;
            Title = $"{repoInfo.Name} — Wheelhouse";
            StatusText = "Ready";

            _settingsService.Update(s =>
            {
                s.RecentRepositories.Remove(dialog.FolderName);
                s.RecentRepositories.Insert(0, dialog.FolderName);
                while (s.RecentRepositories.Count > s.MaxRecentRepositories)
                    s.RecentRepositories.RemoveAt(s.RecentRepositories.Count - 1);
            });

            WeakReferenceMessenger.Default.Send(new RepositoryOpenedMessage(repoInfo));
        }
        catch (Exception ex)
        {
            StatusText = "Failed to open repository";
            MessageBox.Show($"Could not open repository:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task FetchAsync()
    {
        if (!_repositoryService.IsOpen) return;
        StatusText = "Fetching...";
        try
        {
            await _repositoryService.FetchAsync();
            StatusText = "Fetch complete";
            WeakReferenceMessenger.Default.Send(new WorkingTreeChangedMessage());
        }
        catch (Exception ex)
        {
            StatusText = $"Fetch failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task PullAsync()
    {
        if (!_repositoryService.IsOpen) return;
        StatusText = "Pulling...";
        try
        {
            await _repositoryService.PullAsync();
            StatusText = "Pull complete";
            WeakReferenceMessenger.Default.Send(new WorkingTreeChangedMessage());
        }
        catch (Exception ex)
        {
            StatusText = $"Pull failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task PushAsync()
    {
        if (!_repositoryService.IsOpen) return;
        StatusText = "Pushing...";
        try
        {
            await _repositoryService.PushAsync();
            StatusText = "Push complete";
        }
        catch (Exception ex)
        {
            StatusText = $"Push failed: {ex.Message}";
        }
    }
}
