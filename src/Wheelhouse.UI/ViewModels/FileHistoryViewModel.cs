using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Wheelhouse.Core.Models;
using Wheelhouse.Core.Services;
using Wheelhouse.UI.Messages;

namespace Wheelhouse.UI.ViewModels;

public sealed partial class FileHistoryViewModel : ViewModelBase
{
    private readonly IRepositoryService _repositoryService;

    public string FilePath { get; }

    [ObservableProperty] private ObservableCollection<FileHistoryEntryViewModel> _commits = [];
    [ObservableProperty] private FileHistoryEntryViewModel? _selectedCommit;
    [ObservableProperty] private bool _isLoading;

    public FileHistoryViewModel(string filePath, IRepositoryService repositoryService)
    {
        FilePath = filePath;
        _repositoryService = repositoryService;
        LoadAsync().ConfigureAwait(false);
    }

    partial void OnSelectedCommitChanged(FileHistoryEntryViewModel? value)
    {
        if (value is null) return;
        WeakReferenceMessenger.Default.Send(new CommitFileSelectedMessage(value.Commit.Sha, FilePath));
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var commits = await _repositoryService.GetFileHistoryAsync(FilePath);
            Application.Current.Dispatcher.Invoke(() =>
                Commits = new ObservableCollection<FileHistoryEntryViewModel>(
                    commits.Select(c => new FileHistoryEntryViewModel(c))));
        }
        finally { IsLoading = false; }
    }
}

public sealed class FileHistoryEntryViewModel
{
    public CommitInfo Commit { get; }
    public string ShortSha     => Commit.ShortSha;
    public string MessageShort => Commit.MessageShort;
    public string AuthorName   => Commit.AuthorName;
    public string RelativeDate => FormatRelativeDate(Commit.AuthorWhen);

    public FileHistoryEntryViewModel(CommitInfo commit) => Commit = commit;

    private static string FormatRelativeDate(DateTimeOffset when)
    {
        var d = DateTimeOffset.Now - when;
        return d.TotalSeconds < 60  ? "just now"
             : d.TotalMinutes < 60  ? $"{(int)d.TotalMinutes}m ago"
             : d.TotalHours < 24    ? $"{(int)d.TotalHours}h ago"
             : d.TotalDays < 30     ? $"{(int)d.TotalDays}d ago"
             : d.TotalDays < 365    ? $"{(int)(d.TotalDays / 30)}mo ago"
             :                        $"{(int)(d.TotalDays / 365)}y ago";
    }
}
