using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Wheelhouse.Core.Models;
using Wheelhouse.Core.Services;
using Wheelhouse.UI.Messages;

namespace Wheelhouse.UI.ViewModels;

public sealed partial class DiffViewModel : ViewModelBase,
    IRecipient<FileSelectedForDiffMessage>,
    IRecipient<CommitSelectedMessage>,
    IRecipient<RepositoryClosedMessage>
{
    private readonly IRepositoryService _repositoryService;

    [ObservableProperty] private string _rawDiffText = string.Empty;
    [ObservableProperty] private string _diffHeader = string.Empty;
    [ObservableProperty] private bool _isLoading = false;
    [ObservableProperty] private bool _isBinary = false;
    [ObservableProperty] private bool _isEmpty = true;

    public DiffViewModel(IRepositoryService repositoryService)
    {
        _repositoryService = repositoryService;
        WeakReferenceMessenger.Default.RegisterAll(this);
    }

    async void IRecipient<FileSelectedForDiffMessage>.Receive(FileSelectedForDiffMessage msg)
    {
        if (!_repositoryService.IsOpen) return;
        IsLoading = true;
        IsEmpty = false;
        try
        {
            var diff = await _repositoryService.GetFileDiffAsync(msg.FilePath, msg.IsStaged);
            if (diff is null)
            {
                RawDiffText = string.Empty;
                DiffHeader = msg.FilePath;
                IsEmpty = true;
                return;
            }
            IsBinary = diff.IsBinary;
            DiffHeader = BuildHeader(diff);
            RawDiffText = IsBinary ? "[Binary file — diff not shown]" : BuildDiffText(diff);
        }
        catch (Exception ex)
        {
            RawDiffText = $"Error loading diff: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    async void IRecipient<CommitSelectedMessage>.Receive(CommitSelectedMessage msg)
    {
        // TODO Phase 2: load commit diff for all changed files
        DiffHeader = msg.Commit.MessageShort;
        RawDiffText = string.Empty;
        IsEmpty = true;
        await Task.CompletedTask;
    }

    void IRecipient<RepositoryClosedMessage>.Receive(RepositoryClosedMessage _)
    {
        RawDiffText = string.Empty;
        DiffHeader = string.Empty;
        IsEmpty = true;
    }

    private static string BuildHeader(FileDiff diff)
    {
        if (diff.IsRenamed) return $"{diff.OldPath} → {diff.NewPath}";
        if (diff.IsNew) return $"{diff.NewPath} (new file)";
        if (diff.IsDeleted) return $"{diff.OldPath} (deleted)";
        return diff.NewPath;
    }

    private static string BuildDiffText(FileDiff diff)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var hunk in diff.Hunks)
        {
            sb.AppendLine(hunk.Header);
            foreach (var line in hunk.Lines)
            {
                var prefix = line.Type switch
                {
                    DiffLineType.Added   => "+",
                    DiffLineType.Removed => "-",
                    _                    => " "
                };
                sb.AppendLine(prefix + line.Content);
            }
        }
        return sb.ToString();
    }
}
