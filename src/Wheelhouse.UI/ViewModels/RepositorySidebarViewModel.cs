using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Wheelhouse.Core.Services;
using Wheelhouse.UI.Messages;

namespace Wheelhouse.UI.ViewModels;

public sealed partial class RepositorySidebarViewModel : ViewModelBase,
    IRecipient<RepositoryOpenedMessage>,
    IRecipient<RepositoryClosedMessage>,
    IRecipient<WorkingTreeChangedMessage>
{
    private readonly IRepositoryService _repositoryService;

    [ObservableProperty] private ObservableCollection<BranchItemViewModel> _localBranches = [];
    [ObservableProperty] private ObservableCollection<BranchItemViewModel> _remoteBranches = [];
    [ObservableProperty] private string _repositoryName = string.Empty;
    [ObservableProperty] private string _currentBranchName = string.Empty;
    [ObservableProperty] private bool _hasRepository = false;

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
            RepositoryName = string.Empty;
            CurrentBranchName = string.Empty;
            HasRepository = false;
        });
    }

    void IRecipient<WorkingTreeChangedMessage>.Receive(WorkingTreeChangedMessage _) =>
        RefreshAsync().ConfigureAwait(false);

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (!_repositoryService.IsOpen) return;
        var branches = await _repositoryService.GetBranchesAsync();

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
        });
    }
}
