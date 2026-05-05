using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Wheelhouse.Core.Services;
using Wheelhouse.UI.Messages;
using Wheelhouse.UI.Views;

namespace Wheelhouse.UI.ViewModels;

public sealed partial class RepositorySidebarViewModel : ViewModelBase,
    IRecipient<RepositoryOpenedMessage>,
    IRecipient<RepositoryClosedMessage>,
    IRecipient<WorkingTreeChangedMessage>,
    IRecipient<BranchChangedMessage>,
    IRecipient<StashChangedMessage>,
    IRecipient<TagChangedMessage>,
    IRecipient<RemoteChangedMessage>
{
    private readonly IRepositoryService _repositoryService;

    [ObservableProperty] private ObservableCollection<BranchItemViewModel> _localBranches = [];
    [ObservableProperty] private ObservableCollection<BranchItemViewModel> _remoteBranches = [];
    [ObservableProperty] private ObservableCollection<StashItemViewModel> _stashes = [];
    [ObservableProperty] private ObservableCollection<TagItemViewModel> _tags = [];
    [ObservableProperty] private ObservableCollection<RemoteItemViewModel> _remotes = [];
    [ObservableProperty] private ObservableCollection<WorktreeItemViewModel> _worktrees = [];
    [ObservableProperty] private string _repositoryName = string.Empty;
    [ObservableProperty] private string _currentBranchName = string.Empty;
    [ObservableProperty] private bool _hasRepository = false;
    [ObservableProperty] private bool _hasStashes = false;
    [ObservableProperty] private bool _hasTags = false;
    [ObservableProperty] private bool _hasRemotes = false;
    [ObservableProperty] private bool _hasWorktrees = false;

    public RepositorySidebarViewModel(IRepositoryService repositoryService)
    {
        _repositoryService = repositoryService;
        WeakReferenceMessenger.Default.RegisterAll(this);
    }

    void IRecipient<RepositoryOpenedMessage>.Receive(RepositoryOpenedMessage msg)
    {
        RepositoryName = msg.Value.Name;
        HasRepository = true;
        RefreshAsync().ConfigureAwait(false);
    }

    void IRecipient<RepositoryClosedMessage>.Receive(RepositoryClosedMessage _)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            LocalBranches.Clear();
            RemoteBranches.Clear();
            Stashes.Clear();
            Tags.Clear();
            Remotes.Clear();
            Worktrees.Clear();
            RepositoryName = string.Empty;
            CurrentBranchName = string.Empty;
            HasRepository = false;
            HasStashes = false;
            HasTags = false;
            HasRemotes = false;
            HasWorktrees = false;
        });
    }

    void IRecipient<WorkingTreeChangedMessage>.Receive(WorkingTreeChangedMessage _) =>
        RefreshAsync().ConfigureAwait(false);

    void IRecipient<BranchChangedMessage>.Receive(BranchChangedMessage _) =>
        RefreshBranchesAsync().ConfigureAwait(false);

    void IRecipient<StashChangedMessage>.Receive(StashChangedMessage _) =>
        RefreshStashesAsync().ConfigureAwait(false);

    void IRecipient<TagChangedMessage>.Receive(TagChangedMessage _) =>
        RefreshTagsAsync().ConfigureAwait(false);

    void IRecipient<RemoteChangedMessage>.Receive(RemoteChangedMessage _) =>
        RefreshRemotesAsync().ConfigureAwait(false);

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (!_repositoryService.IsOpen) return;

        var branchTask   = _repositoryService.GetBranchesAsync();
        var stashTask    = _repositoryService.GetStashesAsync();
        var tagTask      = _repositoryService.GetTagsAsync();
        var remoteTask   = _repositoryService.GetRemotesAsync();
        var worktreeTask = _repositoryService.GetWorktreesAsync();
        await Task.WhenAll(branchTask, stashTask, tagTask, remoteTask, worktreeTask);

        var branches    = await branchTask;
        var stashList   = await stashTask;
        var tagList     = await tagTask;
        var remoteList  = await remoteTask;
        var worktreeList = await worktreeTask;

        Application.Current.Dispatcher.Invoke(() =>
        {
            var current = branches.FirstOrDefault(b => b.IsCurrentRepositoryHead);
            CurrentBranchName = current?.FriendlyName ?? string.Empty;

            LocalBranches = new ObservableCollection<BranchItemViewModel>(
                branches.Where(b => !b.IsRemote)
                        .Select(b => new BranchItemViewModel(b, _repositoryService)));

            RemoteBranches = new ObservableCollection<BranchItemViewModel>(
                branches.Where(b => b.IsRemote)
                        .Select(b => new BranchItemViewModel(b, _repositoryService)));

            Stashes = new ObservableCollection<StashItemViewModel>(
                stashList.Select(s => new StashItemViewModel(s, _repositoryService)));
            HasStashes = Stashes.Count > 0;

            Tags = new ObservableCollection<TagItemViewModel>(
                tagList.OrderByDescending(t => t.When ?? DateTimeOffset.MinValue)
                       .Select(t => new TagItemViewModel(t, _repositoryService)));
            HasTags = Tags.Count > 0;

            Remotes = new ObservableCollection<RemoteItemViewModel>(
                remoteList.Select(r => new RemoteItemViewModel(r, _repositoryService)));
            HasRemotes = Remotes.Count > 0;

            Worktrees = new ObservableCollection<WorktreeItemViewModel>(
                worktreeList.Select(w => new WorktreeItemViewModel(w, _repositoryService)));
            HasWorktrees = Worktrees.Count > 1; // >1 because main worktree always exists
        });
    }

    private async Task RefreshBranchesAsync()
    {
        if (!_repositoryService.IsOpen) return;
        var branches = await _repositoryService.GetBranchesAsync();
        Application.Current.Dispatcher.Invoke(() =>
        {
            var current = branches.FirstOrDefault(b => b.IsCurrentRepositoryHead);
            CurrentBranchName = current?.FriendlyName ?? string.Empty;
            LocalBranches = new ObservableCollection<BranchItemViewModel>(
                branches.Where(b => !b.IsRemote).Select(b => new BranchItemViewModel(b, _repositoryService)));
            RemoteBranches = new ObservableCollection<BranchItemViewModel>(
                branches.Where(b => b.IsRemote).Select(b => new BranchItemViewModel(b, _repositoryService)));
        });
    }

    private async Task RefreshStashesAsync()
    {
        if (!_repositoryService.IsOpen) return;
        var stashList = await _repositoryService.GetStashesAsync();
        Application.Current.Dispatcher.Invoke(() =>
        {
            Stashes = new ObservableCollection<StashItemViewModel>(
                stashList.Select(s => new StashItemViewModel(s, _repositoryService)));
            HasStashes = Stashes.Count > 0;
        });
    }

    private async Task RefreshTagsAsync()
    {
        if (!_repositoryService.IsOpen) return;
        var tagList = await _repositoryService.GetTagsAsync();
        Application.Current.Dispatcher.Invoke(() =>
        {
            Tags = new ObservableCollection<TagItemViewModel>(
                tagList.OrderByDescending(t => t.When ?? DateTimeOffset.MinValue)
                       .Select(t => new TagItemViewModel(t, _repositoryService)));
            HasTags = Tags.Count > 0;
        });
    }

    private async Task RefreshRemotesAsync()
    {
        if (!_repositoryService.IsOpen) return;
        var remoteList = await _repositoryService.GetRemotesAsync();
        Application.Current.Dispatcher.Invoke(() =>
        {
            Remotes = new ObservableCollection<RemoteItemViewModel>(
                remoteList.Select(r => new RemoteItemViewModel(r, _repositoryService)));
            HasRemotes = Remotes.Count > 0;
        });
    }

    [RelayCommand]
    private void CreateBranch()
    {
        if (!_repositoryService.IsOpen) return;
        var dialog = new CreateBranchDialog { Owner = Application.Current.MainWindow };
        if (dialog.ShowDialog() != true) return;
        CreateBranchCoreAsync(dialog.BranchName, null, dialog.CheckoutImmediately).ConfigureAwait(false);
    }

    [RelayCommand]
    private void CreateTag()
    {
        if (!_repositoryService.IsOpen) return;
        var dialog = new CreateTagDialog { Owner = Application.Current.MainWindow };
        if (dialog.ShowDialog() != true) return;
        CreateTagCoreAsync(dialog.TagName, null, dialog.Message).ConfigureAwait(false);
    }

    [RelayCommand]
    private void AddRemote()
    {
        if (!_repositoryService.IsOpen) return;
        var dialog = new AddRemoteDialog { Owner = Application.Current.MainWindow };
        if (dialog.ShowDialog() != true) return;
        AddRemoteCoreAsync(dialog.RemoteName, dialog.RemoteUrl).ConfigureAwait(false);
    }

    private async Task CreateBranchCoreAsync(string name, string? startPoint, bool checkout)
    {
        try
        {
            await _repositoryService.CreateBranchAsync(name, startPoint, checkout);
            WeakReferenceMessenger.Default.Send(new BranchChangedMessage());
            if (checkout) WeakReferenceMessenger.Default.Send(new WorkingTreeChangedMessage());
        }
        catch (Exception ex)
        {
            Application.Current.Dispatcher.Invoke(() =>
                MessageBox.Show($"Create branch failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error));
        }
    }

    private async Task CreateTagCoreAsync(string name, string? targetSha, string? message)
    {
        try
        {
            await _repositoryService.CreateTagAsync(name, targetSha, message);
            WeakReferenceMessenger.Default.Send(new TagChangedMessage());
        }
        catch (Exception ex)
        {
            Application.Current.Dispatcher.Invoke(() =>
                MessageBox.Show($"Create tag failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error));
        }
    }

    private async Task AddRemoteCoreAsync(string name, string url)
    {
        try
        {
            await _repositoryService.AddRemoteAsync(name, url);
            WeakReferenceMessenger.Default.Send(new RemoteChangedMessage());
        }
        catch (Exception ex)
        {
            Application.Current.Dispatcher.Invoke(() =>
                MessageBox.Show($"Add remote failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error));
        }
    }

    [RelayCommand]
    private void OpenReflog() =>
        WeakReferenceMessenger.Default.Send(new OpenReflogMessage());

    [RelayCommand]
    private void AddWorktree()
    {
        if (!_repositoryService.IsOpen) return;
        var dialog = new AddWorktreeDialog { Owner = Application.Current.MainWindow };
        if (dialog.ShowDialog() != true) return;
        AddWorktreeCoreAsync(dialog.WorktreePath, dialog.Branch, dialog.CreateNewBranch).ConfigureAwait(false);
    }

    private async Task AddWorktreeCoreAsync(string path, string branch, bool createBranch)
    {
        try
        {
            await _repositoryService.AddWorktreeAsync(path, branch, createBranch);
            await RefreshWorktreesAsync();
        }
        catch (Exception ex)
        {
            Application.Current.Dispatcher.Invoke(() =>
                MessageBox.Show($"Add worktree failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error));
        }
    }

    [RelayCommand]
    private async Task PruneWorktreesAsync()
    {
        try
        {
            await _repositoryService.PruneWorktreesAsync();
            await RefreshWorktreesAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Prune worktrees failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task RefreshWorktreesAsync()
    {
        if (!_repositoryService.IsOpen) return;
        var worktreeList = await _repositoryService.GetWorktreesAsync();
        Application.Current.Dispatcher.Invoke(() =>
        {
            Worktrees = new ObservableCollection<WorktreeItemViewModel>(
                worktreeList.Select(w => new WorktreeItemViewModel(w, _repositoryService)));
            HasWorktrees = Worktrees.Count > 1;
        });
    }

    [RelayCommand]
    private async Task StashChangesAsync()
    {
        if (!_repositoryService.IsOpen) return;
        var dialog = new StashDialog { Owner = Application.Current.MainWindow };
        if (dialog.ShowDialog() != true) return;
        try
        {
            await _repositoryService.StashAsync(
                dialog.Message.Length > 0 ? dialog.Message : null,
                dialog.IncludeUntracked);
            WeakReferenceMessenger.Default.Send(new StashChangedMessage());
            WeakReferenceMessenger.Default.Send(new WorkingTreeChangedMessage());
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Stash failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
