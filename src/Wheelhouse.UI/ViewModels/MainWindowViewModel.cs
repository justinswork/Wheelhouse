using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Wheelhouse.Core.Services;
using Wheelhouse.UI.Messages;
using Wheelhouse.UI.Services;

namespace Wheelhouse.UI.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase,
    IRecipient<OpenReflogMessage>,
    IRecipient<OpenFileHistoryMessage>,
    IRecipient<OpenBlameMessage>,
    IRecipient<NavigateToCommitMessage>
{
    private readonly IRepositoryService _repositoryService;
    private readonly ISettingsService _settingsService;
    private readonly IThemeService _themeService;
    private readonly ReflogViewModel _reflogViewModel;

    // Panels rendered outside the tab system
    public DiffViewModel DiffViewModel { get; }
    public RepositorySidebarViewModel SidebarViewModel { get; }

    // Dynamic center tab system
    [ObservableProperty] private ObservableCollection<TabDefinition> _tabs = [];
    [ObservableProperty] private TabDefinition? _activeTab;

    // Permanent tab refs for navigation
    private readonly TabDefinition _logTab;

    [ObservableProperty] private string _title = "Wheelhouse";
    [ObservableProperty] private string _statusText = "Ready";
    [ObservableProperty] private bool _isBusy = false;
    [ObservableProperty] private bool _isTerminalVisible = false;
    [ObservableProperty] private AppTheme _currentTheme;

    public MainWindowViewModel(
        IRepositoryService repositoryService,
        ISettingsService settingsService,
        IThemeService themeService,
        WorkingTreeViewModel workingTreeViewModel,
        LogViewModel logViewModel,
        DiffViewModel diffViewModel,
        RepositorySidebarViewModel sidebarViewModel,
        ReflogViewModel reflogViewModel)
    {
        _repositoryService = repositoryService;
        _settingsService = settingsService;
        _themeService = themeService;
        _reflogViewModel = reflogViewModel;

        DiffViewModel = diffViewModel;
        SidebarViewModel = sidebarViewModel;

        var workingTreeTab = new TabDefinition("Working Tree", workingTreeViewModel);
        _logTab = new TabDefinition("Log", logViewModel);
        Tabs = [workingTreeTab, _logTab];
        ActiveTab = workingTreeTab;

        CurrentTheme = _themeService.CurrentTheme;
        _themeService.ThemeChanged += (_, theme) => CurrentTheme = theme;

        WeakReferenceMessenger.Default.Register<OpenReflogMessage>(this);
        WeakReferenceMessenger.Default.Register<OpenFileHistoryMessage>(this);
        WeakReferenceMessenger.Default.Register<OpenBlameMessage>(this);
        WeakReferenceMessenger.Default.Register<NavigateToCommitMessage>(this);
    }

    void IRecipient<OpenReflogMessage>.Receive(OpenReflogMessage _)
    {
        var existing = Tabs.FirstOrDefault(t => t.ViewModel is ReflogViewModel);
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (existing is null)
            {
                existing = new TabDefinition("Reflog", _reflogViewModel, canClose: true, onClose: RemoveTab);
                Tabs.Add(existing);
            }
            ActiveTab = existing;
        });
    }

    void IRecipient<OpenFileHistoryMessage>.Receive(OpenFileHistoryMessage msg)
    {
        var existing = Tabs.FirstOrDefault(t => t.ViewModel is FileHistoryViewModel fh && fh.FilePath == msg.FilePath);
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (existing is null)
            {
                var vm = new FileHistoryViewModel(msg.FilePath, _repositoryService);
                var header = "History: " + System.IO.Path.GetFileName(msg.FilePath);
                existing = new TabDefinition(header, vm, canClose: true, onClose: RemoveTab);
                Tabs.Add(existing);
            }
            ActiveTab = existing;
        });
    }

    void IRecipient<OpenBlameMessage>.Receive(OpenBlameMessage msg)
    {
        var existing = Tabs.FirstOrDefault(t => t.ViewModel is BlameViewModel bv && bv.FilePath == msg.FilePath);
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (existing is null)
            {
                var vm = new BlameViewModel(msg.FilePath, _repositoryService);
                var header = "Blame: " + System.IO.Path.GetFileName(msg.FilePath);
                existing = new TabDefinition(header, vm, canClose: true, onClose: RemoveTab);
                Tabs.Add(existing);
            }
            ActiveTab = existing;
        });
    }

    void IRecipient<NavigateToCommitMessage>.Receive(NavigateToCommitMessage _)
    {
        Application.Current.Dispatcher.Invoke(() => ActiveTab = _logTab);
    }

    private void RemoveTab(TabDefinition tab) =>
        Application.Current.Dispatcher.Invoke(() => Tabs.Remove(tab));

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

        IsBusy = true;
        StatusText = "Opening repository...";
        try
        {
            await Task.Run(() => _repositoryService.Open(dialog.FolderName));

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
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task FetchAsync()
    {
        if (!_repositoryService.IsOpen) return;
        IsBusy = true;
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
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task PullAsync()
    {
        if (!_repositoryService.IsOpen) return;
        IsBusy = true;
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
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task PushAsync()
    {
        if (!_repositoryService.IsOpen) return;
        IsBusy = true;
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
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void CreateBranch() => SidebarViewModel.CreateBranchCommand.Execute(null);

    [RelayCommand]
    private void StashChanges() => SidebarViewModel.StashChangesCommand.Execute(null);

    [RelayCommand]
    private void CreateTag() => SidebarViewModel.CreateTagCommand.Execute(null);

    [RelayCommand]
    private void AddRemote() => SidebarViewModel.AddRemoteCommand.Execute(null);

    [RelayCommand]
    private void OpenReflog() => SidebarViewModel.OpenReflogCommand.Execute(null);
}
