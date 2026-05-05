using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Wheelhouse.Core.Models;
using Wheelhouse.Core.Services;
using Wheelhouse.UI.Messages;

namespace Wheelhouse.UI.ViewModels;

public sealed partial class BlameViewModel : ViewModelBase
{
    private readonly IRepositoryService _repositoryService;

    public string FilePath { get; }

    [ObservableProperty] private ObservableCollection<BlameLineViewModel> _lines = [];
    [ObservableProperty] private bool _isLoading;

    public BlameViewModel(string filePath, IRepositoryService repositoryService)
    {
        FilePath = filePath;
        _repositoryService = repositoryService;
        LoadAsync().ConfigureAwait(false);
    }

    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var blameLines = await _repositoryService.GetBlameAsync(FilePath);
            var oldest  = blameLines.Count > 0 ? blameLines.Min(l => l.When) : DateTimeOffset.Now;
            var newest  = blameLines.Count > 0 ? blameLines.Max(l => l.When) : DateTimeOffset.Now;
            var spanMs  = Math.Max(1, (newest - oldest).TotalMilliseconds);

            Application.Current.Dispatcher.Invoke(() =>
                Lines = new ObservableCollection<BlameLineViewModel>(
                    blameLines.Select(l => new BlameLineViewModel(l, oldest, spanMs))));
        }
        finally { IsLoading = false; }
    }
}

public sealed partial class BlameLineViewModel
{
    public BlameLine Line { get; }
    public string ShortSha    => Line.ShortSha;
    public string AuthorName  => Line.AuthorName;
    public string Content     => Line.Content;
    public int LineNumber     => Line.LineNumber;
    public string RelativeDate => FormatRelativeDate(Line.When);
    public Brush AgeBrush { get; }

    public BlameLineViewModel(BlameLine line, DateTimeOffset oldest, double spanMs)
    {
        Line = line;
        // Heat-map: older = more red, newer = more blue
        var ratio = spanMs < 1 ? 1.0 : (line.When - oldest).TotalMilliseconds / spanMs;
        var r = (byte)(0xFF * (1.0 - ratio));
        var b = (byte)(0x60 + (int)(0x9F * ratio));
        AgeBrush = new SolidColorBrush(Color.FromArgb(0x30, r, 0x50, b));
    }

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void NavigateToCommit() =>
        WeakReferenceMessenger.Default.Send(new NavigateToCommitMessage(Line.CommitSha));

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
