using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Wheelhouse.Core.Services;
using Wheelhouse.UI.Controls.CommitGraph;
using Wheelhouse.UI.Messages;

namespace Wheelhouse.UI.ViewModels;

public sealed partial class LogViewModel : ViewModelBase,
    IRecipient<RepositoryOpenedMessage>,
    IRecipient<RepositoryClosedMessage>,
    IRecipient<WorkingTreeChangedMessage>
{
    private readonly IRepositoryService _repositoryService;
    private const int PageSize = 500;

    [ObservableProperty] private ObservableCollection<CommitItemViewModel> _commits = [];
    [ObservableProperty] private CommitItemViewModel? _selectedCommit;
    [ObservableProperty] private bool _isLoading = false;
    [ObservableProperty] private bool _hasMore = false;

    public LogViewModel(IRepositoryService repositoryService)
    {
        _repositoryService = repositoryService;
        WeakReferenceMessenger.Default.RegisterAll(this);
    }

    void IRecipient<RepositoryOpenedMessage>.Receive(RepositoryOpenedMessage _) => LoadAsync().ConfigureAwait(false);
    void IRecipient<RepositoryClosedMessage>.Receive(RepositoryClosedMessage _) => Application.Current.Dispatcher.Invoke(Commits.Clear);
    void IRecipient<WorkingTreeChangedMessage>.Receive(WorkingTreeChangedMessage _) => LoadAsync().ConfigureAwait(false);

    partial void OnSelectedCommitChanged(CommitItemViewModel? value)
    {
        if (value is null) return;
        WeakReferenceMessenger.Default.Send(new CommitSelectedMessage(value.Commit));
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (!_repositoryService.IsOpen) return;
        IsLoading = true;
        try
        {
            var rawCommits = await _repositoryService.GetCommitLogAsync(skip: 0, take: PageSize);
            var graphRows = GraphLaneCalculator.Calculate(rawCommits);
            var items = rawCommits
                .Select((c, i) => new CommitItemViewModel(c, graphRows[i]))
                .ToList();

            Application.Current.Dispatcher.Invoke(() =>
            {
                Commits = new ObservableCollection<CommitItemViewModel>(items);
                HasMore = rawCommits.Count == PageSize;
            });
        }
        catch (Exception ex)
        {
            // Swallow for now; could send an error message
            _ = ex;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task LoadMoreAsync()
    {
        if (!_repositoryService.IsOpen || !HasMore) return;
        IsLoading = true;
        try
        {
            var rawCommits = await _repositoryService.GetCommitLogAsync(skip: Commits.Count, take: PageSize);
            var graphRows = GraphLaneCalculator.Calculate(rawCommits);

            Application.Current.Dispatcher.Invoke(() =>
            {
                for (int i = 0; i < rawCommits.Count; i++)
                    Commits.Add(new CommitItemViewModel(rawCommits[i], graphRows[i]));
                HasMore = rawCommits.Count == PageSize;
            });
        }
        finally
        {
            IsLoading = false;
        }
    }
}
