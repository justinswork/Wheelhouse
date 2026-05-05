using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Wheelhouse.Core.Models;
using Wheelhouse.Core.Services;
using Wheelhouse.UI.Messages;

namespace Wheelhouse.UI.ViewModels;

public sealed partial class HunkViewModel : ViewModelBase
{
    private readonly DiffHunk _hunk;
    private readonly string _filePath;
    private readonly bool _isStaged;
    private readonly bool _isNew;
    private readonly bool _isDeleted;
    private readonly IRepositoryService _service;

    public string Header => _hunk.Header;
    public IReadOnlyList<DiffLineViewModel> Lines { get; }

    public bool CanStage   => !_isStaged;
    public bool CanUnstage => _isStaged;
    public bool CanDiscard => !_isStaged;

    public HunkViewModel(DiffHunk hunk, string filePath, bool isStaged, bool isNew, bool isDeleted, IRepositoryService service)
    {
        _hunk = hunk;
        _filePath = filePath;
        _isStaged = isStaged;
        _isNew = isNew;
        _isDeleted = isDeleted;
        _service = service;
        Lines = hunk.Lines.Select(l => new DiffLineViewModel(l)).ToList();
    }

    [RelayCommand]
    private async Task StageHunkAsync()
    {
        try
        {
            await _service.StageHunkAsync(_filePath, _hunk, _isNew);
            WeakReferenceMessenger.Default.Send(new WorkingTreeChangedMessage());
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Stage hunk failed: {ex.Message}", "Stage Hunk", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task UnstageHunkAsync()
    {
        try
        {
            await _service.UnstageHunkAsync(_filePath, _hunk);
            WeakReferenceMessenger.Default.Send(new WorkingTreeChangedMessage());
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Unstage hunk failed: {ex.Message}", "Unstage Hunk", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task DiscardHunkAsync()
    {
        if (MessageBox.Show($"Discard this hunk?\n\nThis will permanently remove these working-tree changes.",
                "Discard Hunk", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;
        try
        {
            await _service.DiscardHunkAsync(_filePath, _hunk);
            WeakReferenceMessenger.Default.Send(new WorkingTreeChangedMessage());
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Discard hunk failed: {ex.Message}", "Discard Hunk", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

public sealed class DiffLineViewModel
{
    private static readonly Brush AddedBrush   = new SolidColorBrush(Color.FromArgb(60, 0x1A, 0x7F, 0x37));
    private static readonly Brush RemovedBrush = new SolidColorBrush(Color.FromArgb(60, 0xCF, 0x22, 0x2E));

    public DiffLineType Type { get; }
    public string Content { get; }
    public string Prefix => Type == DiffLineType.Added ? "+" : Type == DiffLineType.Removed ? "-" : " ";
    public int? OldLineNumber { get; }
    public int? NewLineNumber { get; }
    public string OldLineNo => OldLineNumber?.ToString() ?? string.Empty;
    public string NewLineNo => NewLineNumber?.ToString() ?? string.Empty;
    public Brush Background => Type == DiffLineType.Added ? AddedBrush : Type == DiffLineType.Removed ? RemovedBrush : Brushes.Transparent;

    public DiffLineViewModel(DiffLine line)
    {
        Type = line.Type;
        Content = line.Content;
        OldLineNumber = line.OldLineNumber;
        NewLineNumber = line.NewLineNumber;
    }
}
