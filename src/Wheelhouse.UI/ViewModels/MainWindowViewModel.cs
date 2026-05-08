using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Wheelhouse.Core.Services;
using Wheelhouse.UI.Messages;
using Wheelhouse.UI.Properties;
using Wheelhouse.UI.Services;
using Wheelhouse.UI.Views;

namespace Wheelhouse.UI.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase,
    IRecipient<OpenReflogMessage>,
    IRecipient<OpenFileHistoryMessage>,
    IRecipient<OpenBlameMessage>,
    IRecipient<NavigateToCommitMessage>,
    IRecipient<OpenPullRequestsMessage>,
    IRecipient<UpdateAvailableMessage>
{
    private readonly IRepositoryService _repositoryService;
    private readonly ISettingsService _settingsService;
    private readonly IThemeService _themeService;
    private readonly IHostingService _hostingService;
    private readonly ReflogViewModel _reflogViewModel;
    private readonly PullRequestsViewModel _pullRequestsViewModel;

    public TerminalPaneViewModel TerminalPaneViewModel { get; }

    // Panels rendered outside the tab system
    public DiffViewModel DiffViewModel { get; }
    public RepositorySidebarViewModel SidebarViewModel { get; }

    // Dynamic center tab system
    [ObservableProperty] private ObservableCollection<TabDefinition> _tabs = [];
    [ObservableProperty] private TabDefinition? _activeTab;

    // Permanent tab refs for navigation
    private readonly TabDefinition _logTab;

    [ObservableProperty] private string _title = "Wheelhouse";
    [ObservableProperty] private string _statusText = Strings.Status_Ready;
    [ObservableProperty] private bool _isBusy = false;
    [ObservableProperty] private bool _isTerminalVisible = false;
    [ObservableProperty] private AppTheme _currentTheme;
    [ObservableProperty] private string? _updateAvailableText;
    public UpdateAvailableMessage? PendingUpdate { get; private set; }

    public MainWindowViewModel(
        IRepositoryService repositoryService,
        ISettingsService settingsService,
        IThemeService themeService,
        IHostingService hostingService,
        WorkingTreeViewModel workingTreeViewModel,
        LogViewModel logViewModel,
        DiffViewModel diffViewModel,
        RepositorySidebarViewModel sidebarViewModel,
        ReflogViewModel reflogViewModel,
        PullRequestsViewModel pullRequestsViewModel,
        TerminalPaneViewModel terminalPaneViewModel)
    {
        _repositoryService = repositoryService;
        _settingsService = settingsService;
        _themeService = themeService;
        _hostingService = hostingService;
        _reflogViewModel = reflogViewModel;
        _pullRequestsViewModel = pullRequestsViewModel;
        TerminalPaneViewModel = terminalPaneViewModel;

        DiffViewModel = diffViewModel;
        SidebarViewModel = sidebarViewModel;

        var workingTreeTab = new TabDefinition(Strings.Tab_WorkingTree, workingTreeViewModel);
        _logTab = new TabDefinition(Strings.Tab_Log, logViewModel);
        Tabs = [workingTreeTab, _logTab];
        ActiveTab = workingTreeTab;

        CurrentTheme = _themeService.CurrentTheme;
        _themeService.ThemeChanged += (_, theme) => CurrentTheme = theme;

        WeakReferenceMessenger.Default.Register<OpenReflogMessage>(this);
        WeakReferenceMessenger.Default.Register<OpenFileHistoryMessage>(this);
        WeakReferenceMessenger.Default.Register<OpenBlameMessage>(this);
        WeakReferenceMessenger.Default.Register<NavigateToCommitMessage>(this);
        WeakReferenceMessenger.Default.Register<OpenPullRequestsMessage>(this);
        WeakReferenceMessenger.Default.Register<UpdateAvailableMessage>(this);
    }

    void IRecipient<OpenReflogMessage>.Receive(OpenReflogMessage _)
    {
        var existing = Tabs.FirstOrDefault(t => t.ViewModel is ReflogViewModel);
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (existing is null)
            {
                existing = new TabDefinition(Strings.Tab_Reflog, _reflogViewModel, canClose: true, onClose: RemoveTab);
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
                var header = string.Format(Strings.Tab_HistoryPrefix, System.IO.Path.GetFileName(msg.FilePath));
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
                var header = string.Format(Strings.Tab_BlamePrefix, System.IO.Path.GetFileName(msg.FilePath));
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

    void IRecipient<OpenPullRequestsMessage>.Receive(OpenPullRequestsMessage _)
    {
        var existing = Tabs.FirstOrDefault(t => t.ViewModel is PullRequestsViewModel);
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (existing is null)
            {
                existing = new TabDefinition(Strings.Tab_PullRequests, _pullRequestsViewModel, canClose: true, onClose: RemoveTab);
                Tabs.Add(existing);
            }
            ActiveTab = existing;
        });
    }

    void IRecipient<UpdateAvailableMessage>.Receive(UpdateAvailableMessage msg) =>
        Application.Current.Dispatcher.Invoke(() =>
        {
            PendingUpdate = msg;
            UpdateAvailableText = string.Format(Strings.Update_BannerFormat, msg.Version);
        });

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

    internal async Task RestoreLastRepositoryAsync()
    {
        var path = _settingsService.Current.RecentRepositories.FirstOrDefault();
        if (path is null || !System.IO.Directory.Exists(path)) return;
        await OpenRepositoryPathAsync(path, silent: true);
    }

    [RelayCommand]
    private async Task OpenRepository()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select a Git repository folder"
        };
        if (dialog.ShowDialog() != true) return;
        await OpenRepositoryPathAsync(dialog.FolderName);
    }

    private async Task OpenRepositoryPathAsync(string path, bool silent = false)
    {
        IsBusy = true;
        StatusText = Strings.Status_Opening;
        try
        {
            await Task.Run(() => _repositoryService.Open(path));

            var repoInfo = _repositoryService.CurrentRepository!;
            Title = $"{repoInfo.Name} — Wheelhouse";
            StatusText = Strings.Status_Ready;

            _settingsService.Update(s =>
            {
                s.RecentRepositories.Remove(path);
                s.RecentRepositories.Insert(0, path);
                while (s.RecentRepositories.Count > s.MaxRecentRepositories)
                    s.RecentRepositories.RemoveAt(s.RecentRepositories.Count - 1);
            });

            WeakReferenceMessenger.Default.Send(new RepositoryOpenedMessage(repoInfo));
        }
        catch (Exception ex)
        {
            StatusText = Strings.Status_Ready;
            if (!silent)
                MessageBox.Show(string.Format(Strings.Error_OpenRepo, ex.Message), Strings.Dialog_Error,
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
        StatusText = Strings.Status_Fetching;
        try
        {
            await _repositoryService.FetchAsync();
            StatusText = Strings.Status_FetchComplete;
            WeakReferenceMessenger.Default.Send(new WorkingTreeChangedMessage());
        }
        catch (Exception ex)
        {
            StatusText = string.Format(Strings.Status_FetchFailed, ex.Message);
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
        StatusText = Strings.Status_Pulling;
        try
        {
            await _repositoryService.PullAsync();
            StatusText = Strings.Status_PullComplete;
            WeakReferenceMessenger.Default.Send(new WorkingTreeChangedMessage());
        }
        catch (Exception ex)
        {
            StatusText = string.Format(Strings.Status_PullFailed, ex.Message);
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
        StatusText = Strings.Status_Pushing;
        try
        {
            await _repositoryService.PushAsync();
            StatusText = Strings.Status_PushComplete;
        }
        catch (Exception ex)
        {
            StatusText = string.Format(Strings.Status_PushFailed, ex.Message);
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

    [RelayCommand]
    private void OpenPullRequests() =>
        WeakReferenceMessenger.Default.Send(new OpenPullRequestsMessage());

    [RelayCommand]
    private void OpenAccountSettings()
    {
        var dialog = new AccountSettingsDialog(_hostingService) { Owner = Application.Current.MainWindow };
        dialog.ShowDialog();
    }
}
