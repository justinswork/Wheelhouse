using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Wheelhouse.Core.Models;
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
            var items = await Task.Run(() =>
            {
                var rows = GraphLaneCalculator.Calculate(rawCommits);
                return rawCommits.Select((c, i) => new CommitItemViewModel(c, rows[i])).ToList();
            }).ConfigureAwait(false);

            Application.Current.Dispatcher.Invoke(() =>
            {
                Commits = new ObservableCollection<CommitItemViewModel>(items);
                HasMore = rawCommits.Count == PageSize;
            });
        }
        catch (Exception ex)
        {
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
            var newItems = await Task.Run(() =>
            {
                var rows = GraphLaneCalculator.Calculate(rawCommits);
                return rawCommits.Select((c, i) => new CommitItemViewModel(c, rows[i])).ToList();
            }).ConfigureAwait(false);

            Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (var item in newItems)
                    Commits.Add(item);
                HasMore = rawCommits.Count == PageSize;
            });
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task CherryPickAsync(CommitItemViewModel? item)
    {
        if (item is null || !_repositoryService.IsOpen) return;
        try
        {
            await _repositoryService.CherryPickAsync(item.Commit.Sha);
            WeakReferenceMessenger.Default.Send(new WorkingTreeChangedMessage());
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Cherry-pick failed: {ex.Message}", "Cherry-pick", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task RevertAsync(CommitItemViewModel? item)
    {
        if (item is null || !_repositoryService.IsOpen) return;
        if (MessageBox.Show($"Revert '{item.MessageShort}'?\n\nThis creates a new commit that undoes the changes.", "Revert Commit", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;
        try
        {
            await _repositoryService.RevertAsync(item.Commit.Sha);
            WeakReferenceMessenger.Default.Send(new WorkingTreeChangedMessage());
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Revert failed: {ex.Message}", "Revert", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task ResetToHereAsync(CommitItemViewModel? item)
    {
        if (item is null || !_repositoryService.IsOpen) return;

        var dialog = new Wheelhouse.UI.Views.ResetDialog(item.MessageShort) { Owner = Application.Current.MainWindow };
        if (dialog.ShowDialog() != true) return;

        try
        {
            await _repositoryService.ResetAsync(item.Commit.Sha, dialog.SelectedMode);
            WeakReferenceMessenger.Default.Send(new WorkingTreeChangedMessage());
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Reset failed: {ex.Message}", "Reset", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
