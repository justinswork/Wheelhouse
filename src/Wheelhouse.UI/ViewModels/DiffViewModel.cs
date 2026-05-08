using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Wheelhouse.Core.Models;
using Wheelhouse.Core.Services;
using Wheelhouse.UI.Messages;

namespace Wheelhouse.UI.ViewModels;

public sealed partial class DiffViewModel : ViewModelBase,
    IRecipient<FileSelectedForDiffMessage>,
    IRecipient<CommitSelectedMessage>,
    IRecipient<RepositoryClosedMessage>,
    IRecipient<WorkingTreeChangedMessage>,
    IRecipient<CommitFileSelectedMessage>
{
    private readonly IRepositoryService _repositoryService;
    private readonly ISettingsService _settingsService;
    private string _currentFilePath = string.Empty;
    private bool _currentIsStaged;
    private CancellationTokenSource _loadCts = new();

    [ObservableProperty] private ObservableCollection<HunkViewModel> _hunks = [];
    [ObservableProperty] private string _diffHeader = string.Empty;
    [ObservableProperty] private bool _isLoading = false;
    [ObservableProperty] private bool _isBinary = false;
    [ObservableProperty] private bool _isEmpty = true;
    [ObservableProperty] private bool _isSideBySide;
    [ObservableProperty] private bool _isWordWrap;

    public DiffViewModel(IRepositoryService repositoryService, ISettingsService settingsService)
    {
        _repositoryService = repositoryService;
        _settingsService = settingsService;
        _isSideBySide = settingsService.Current.DiffSideBySide;
        _isWordWrap = settingsService.Current.DiffWordWrap;
        WeakReferenceMessenger.Default.RegisterAll(this);
    }

    partial void OnIsSideBySideChanged(bool value)
    {
        _settingsService.Update(s => s.DiffSideBySide = value);
        foreach (var hunk in Hunks) hunk.IsSideBySide = value;
    }

    partial void OnIsWordWrapChanged(bool value)
    {
        _settingsService.Update(s => s.DiffWordWrap = value);
        foreach (var hunk in Hunks) hunk.IsWordWrap = value;
    }

    async void IRecipient<FileSelectedForDiffMessage>.Receive(FileSelectedForDiffMessage msg)
    {
        _currentFilePath = msg.FilePath;
        _currentIsStaged = msg.IsStaged;
        await LoadDiffAsync(msg.FilePath, msg.IsStaged);
    }

    async void IRecipient<WorkingTreeChangedMessage>.Receive(WorkingTreeChangedMessage _)
    {
        if (!string.IsNullOrEmpty(_currentFilePath) && _repositoryService.IsOpen)
            await LoadDiffAsync(_currentFilePath, _currentIsStaged, silent: true);
    }

    async void IRecipient<CommitSelectedMessage>.Receive(CommitSelectedMessage msg)
    {
        _currentFilePath = string.Empty;
        Application.Current.Dispatcher.Invoke(() =>
        {
            DiffHeader = msg.Commit.MessageShort;
            Hunks = [];
            IsEmpty = true;
            IsBinary = false;
        });
        await Task.CompletedTask;
    }

    async void IRecipient<CommitFileSelectedMessage>.Receive(CommitFileSelectedMessage msg)
    {
        _currentFilePath = string.Empty;
        await LoadCommitFileDiffAsync(msg.CommitSha, msg.FilePath);
    }

    void IRecipient<RepositoryClosedMessage>.Receive(RepositoryClosedMessage _)
    {
        _currentFilePath = string.Empty;
        Application.Current.Dispatcher.Invoke(() =>
        {
            Hunks = [];
            DiffHeader = string.Empty;
            IsEmpty = true;
            IsBinary = false;
        });
    }

    private async Task LoadDiffAsync(string filePath, bool isStaged, bool silent = false)
    {
        // Cancel any in-flight load so only the latest request completes.
        var cts = new CancellationTokenSource();
        Interlocked.Exchange(ref _loadCts, cts).Cancel();

        if (!_repositoryService.IsOpen) return;
        if (!silent) { IsLoading = true; IsEmpty = false; }
        try
        {
            var diff = await _repositoryService.GetFileDiffAsync(filePath, isStaged, cts.Token);
            if (cts.IsCancellationRequested) return;

            if (diff is null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Hunks = [];
                    DiffHeader = filePath;
                    IsEmpty = true;
                    IsBinary = false;
                });
                return;
            }

            var hunks = diff.IsBinary
                ? []
                : diff.Hunks
                    .Select(h => new HunkViewModel(h, filePath, isStaged, diff.IsNew, diff.IsDeleted, _repositoryService, isSideBySide: IsSideBySide, isWordWrap: IsWordWrap))
                    .ToList();

            Application.Current.Dispatcher.Invoke(() =>
            {
                IsBinary = diff.IsBinary;
                DiffHeader = BuildHeader(diff);
                Hunks = new ObservableCollection<HunkViewModel>(hunks);
                IsEmpty = hunks.Count == 0 && !diff.IsBinary;
            });
        }
        catch (OperationCanceledException) { return; }
        catch (Exception ex)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Hunks = [];
                DiffHeader = $"Error: {ex.Message}";
                IsEmpty = true;
            });
        }
        finally
        {
            if (!cts.IsCancellationRequested && !silent)
                IsLoading = false;
        }
    }

    private async Task LoadCommitFileDiffAsync(string commitSha, string filePath)
    {
        if (!_repositoryService.IsOpen) return;
        IsLoading = true;
        IsEmpty = false;
        try
        {
            var diff = await _repositoryService.GetCommitFileDiffAsync(commitSha, filePath);
            if (diff is null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Hunks = [];
                    DiffHeader = filePath;
                    IsEmpty = true;
                    IsBinary = false;
                });
                return;
            }

            var hunks = diff.IsBinary
                ? []
                : diff.Hunks
                    .Select(h => new HunkViewModel(h, filePath, false, diff.IsNew, diff.IsDeleted, _repositoryService, isReadOnly: true, isSideBySide: IsSideBySide, isWordWrap: IsWordWrap))
                    .ToList();

            Application.Current.Dispatcher.Invoke(() =>
            {
                IsBinary = diff.IsBinary;
                DiffHeader = BuildHeader(diff);
                Hunks = new ObservableCollection<HunkViewModel>(hunks);
                IsEmpty = hunks.Count == 0 && !diff.IsBinary;
            });
        }
        catch (Exception ex)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Hunks = [];
                DiffHeader = $"Error: {ex.Message}";
                IsEmpty = true;
            });
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static string BuildHeader(FileDiff diff)
    {
        if (diff.IsRenamed) return $"{diff.OldPath} → {diff.NewPath}";
        if (diff.IsNew)     return $"{diff.NewPath} (new file)";
        if (diff.IsDeleted) return $"{diff.OldPath} (deleted)";
        return diff.NewPath;
    }
}
