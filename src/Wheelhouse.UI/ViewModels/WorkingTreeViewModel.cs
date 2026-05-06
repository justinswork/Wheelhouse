using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Wheelhouse.Core.Services;
using Wheelhouse.UI.Messages;

namespace Wheelhouse.UI.ViewModels;

public sealed partial class WorkingTreeViewModel : ViewModelBase,
    IRecipient<RepositoryOpenedMessage>,
    IRecipient<RepositoryClosedMessage>,
    IRecipient<WorkingTreeChangedMessage>
{
    private readonly IRepositoryService _repositoryService;

    [ObservableProperty] private ObservableCollection<FileStatusItemViewModel> _stagedFiles = [];
    [ObservableProperty] private ObservableCollection<FileStatusItemViewModel> _unstagedFiles = [];
    [ObservableProperty] private FileStatusItemViewModel? _selectedStagedFile;
    [ObservableProperty] private FileStatusItemViewModel? _selectedUnstagedFile;
    [ObservableProperty] private string _commitMessage = string.Empty;
    [ObservableProperty] private bool _amend = false;
    [ObservableProperty] private bool _isLoading = false;
    [ObservableProperty] private bool _isLoaded = false;
    [ObservableProperty] private string? _errorMessage;

    public bool HasNoStagedFiles => IsLoaded && StagedFiles.Count == 0 && ErrorMessage is null;
    public bool HasNoUnstagedFiles => IsLoaded && UnstagedFiles.Count == 0 && ErrorMessage is null;
    public bool HasError => ErrorMessage is not null;

    public bool CanCommit => StagedFiles.Count > 0 && !string.IsNullOrWhiteSpace(CommitMessage);

    public WorkingTreeViewModel(IRepositoryService repositoryService)
    {
        _repositoryService = repositoryService;
        WeakReferenceMessenger.Default.RegisterAll(this);
    }

    void IRecipient<RepositoryOpenedMessage>.Receive(RepositoryOpenedMessage _) => RefreshAsync().ConfigureAwait(false);
    void IRecipient<RepositoryClosedMessage>.Receive(RepositoryClosedMessage _) => ClearAll();
    void IRecipient<WorkingTreeChangedMessage>.Receive(WorkingTreeChangedMessage _) => RefreshAsync().ConfigureAwait(false);

    partial void OnSelectedStagedFileChanged(FileStatusItemViewModel? value)
    {
        if (value is null) return;
        SelectedUnstagedFile = null;
        WeakReferenceMessenger.Default.Send(new FileSelectedForDiffMessage(value.FilePath, isStaged: true));
    }

    partial void OnSelectedUnstagedFileChanged(FileStatusItemViewModel? value)
    {
        if (value is null) return;
        SelectedStagedFile = null;
        WeakReferenceMessenger.Default.Send(new FileSelectedForDiffMessage(value.FilePath, isStaged: false));
    }

    partial void OnCommitMessageChanged(string value) => OnPropertyChanged(nameof(CanCommit));
    partial void OnStagedFilesChanged(ObservableCollection<FileStatusItemViewModel> value) => OnPropertyChanged(nameof(CanCommit));

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (!_repositoryService.IsOpen) return;
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var status = await _repositoryService.GetWorkingTreeStatusAsync();

            Application.Current.Dispatcher.Invoke(() =>
            {
                StagedFiles = new ObservableCollection<FileStatusItemViewModel>(
                    status.StagedEntries.Select(e => new FileStatusItemViewModel(e, isStaged: true)));
                UnstagedFiles = new ObservableCollection<FileStatusItemViewModel>(
                    status.UnstagedEntries
                        .Concat(status.UntrackedEntries)
                        .Concat(status.ConflictedEntries)
                        .Select(e => new FileStatusItemViewModel(e, isStaged: false)));
                IsLoaded = true;
                OnPropertyChanged(nameof(CanCommit));
                OnPropertyChanged(nameof(HasNoStagedFiles));
                OnPropertyChanged(nameof(HasNoUnstagedFiles));
                OnPropertyChanged(nameof(HasError));
            });
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            OnPropertyChanged(nameof(HasError));
            OnPropertyChanged(nameof(HasNoStagedFiles));
            OnPropertyChanged(nameof(HasNoUnstagedFiles));
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task StageFileAsync(FileStatusItemViewModel? item)
    {
        if (item is null || !_repositoryService.IsOpen) return;
        try { await _repositoryService.StageAsync([item.FilePath]); await RefreshAsync(); }
        catch (Exception ex) { ShowError(ex); }
    }

    [RelayCommand]
    private async Task UnstageFileAsync(FileStatusItemViewModel? item)
    {
        if (item is null || !_repositoryService.IsOpen) return;
        try { await _repositoryService.UnstageAsync([item.FilePath]); await RefreshAsync(); }
        catch (Exception ex) { ShowError(ex); }
    }

    [RelayCommand]
    private async Task StageAllAsync()
    {
        if (!_repositoryService.IsOpen) return;
        try { await _repositoryService.StageAllAsync(); await RefreshAsync(); }
        catch (Exception ex) { ShowError(ex); }
    }

    [RelayCommand]
    private async Task UnstageAllAsync()
    {
        if (!_repositoryService.IsOpen) return;
        try { await _repositoryService.UnstageAllAsync(); await RefreshAsync(); }
        catch (Exception ex) { ShowError(ex); }
    }

    private void ShowError(Exception ex)
    {
        ErrorMessage = ex.Message;
        OnPropertyChanged(nameof(HasError));
    }

    [RelayCommand(CanExecute = nameof(CanCommit))]
    private async Task CommitAsync()
    {
        if (!_repositoryService.IsOpen) return;
        try
        {
            await _repositoryService.CommitAsync(CommitMessage, Amend);
            CommitMessage = string.Empty;
            Amend = false;
            await RefreshAsync();
            WeakReferenceMessenger.Default.Send(new WorkingTreeChangedMessage());
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Commit failed: {ex.Message}";
            OnPropertyChanged(nameof(HasError));
        }
    }

    private void ClearAll()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            StagedFiles.Clear();
            UnstagedFiles.Clear();
            CommitMessage = string.Empty;
            IsLoaded = false;
            ErrorMessage = null;
            OnPropertyChanged(nameof(HasNoStagedFiles));
            OnPropertyChanged(nameof(HasNoUnstagedFiles));
            OnPropertyChanged(nameof(HasError));
        });
    }
}
