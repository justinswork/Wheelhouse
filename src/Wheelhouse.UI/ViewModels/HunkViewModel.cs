using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Wheelhouse.Core.Models;
using Wheelhouse.Core.Services;
using Wheelhouse.UI.Messages;
using Wheelhouse.UI.Properties;

namespace Wheelhouse.UI.ViewModels;

public sealed partial class HunkViewModel : ViewModelBase
{
    private readonly DiffHunk _hunk;
    private readonly string _filePath;
    private readonly bool _isStaged;
    private readonly bool _isNew;
    private readonly bool _isDeleted;
    private readonly IRepositoryService _service;
    private readonly bool _isReadOnly;

    public string Header => _hunk.Header;
    public IReadOnlyList<DiffLineViewModel> Lines { get; }
    public IReadOnlyList<SideBySideLine> SideBySideLines { get; }

    [ObservableProperty] private bool _isSideBySide;
    [ObservableProperty] private bool _isWordWrap;
    [ObservableProperty] private bool _hasSelectedLines;

    public bool CanStage   => !_isStaged && !_isReadOnly;
    public bool CanUnstage => _isStaged && !_isReadOnly;
    public bool CanDiscard => !_isStaged && !_isReadOnly;

    public bool IsSelectionEnabled => !_isReadOnly && (CanStage || CanUnstage || CanDiscard);
    public bool CanStageSelected   => CanStage   && HasSelectedLines;
    public bool CanUnstageSelected => CanUnstage && HasSelectedLines;
    public bool CanDiscardSelected => CanDiscard && HasSelectedLines;

    public HunkViewModel(DiffHunk hunk, string filePath, bool isStaged, bool isNew, bool isDeleted, IRepositoryService service, bool isReadOnly = false, bool isSideBySide = false, bool isWordWrap = false)
    {
        _hunk = hunk;
        _filePath = filePath;
        _isStaged = isStaged;
        _isNew = isNew;
        _isDeleted = isDeleted;
        _service = service;
        _isReadOnly = isReadOnly;
        _isSideBySide = isSideBySide;
        _isWordWrap = isWordWrap;
        Lines = hunk.Lines.Select(l => new DiffLineViewModel(l)).ToList();
        SideBySideLines = BuildSideBySide(Lines);

        foreach (var line in Lines)
            line.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(DiffLineViewModel.IsSelected))
                    HasSelectedLines = Lines.Any(l => l.IsSelected);
            };
    }

    partial void OnHasSelectedLinesChanged(bool value)
    {
        OnPropertyChanged(nameof(CanStageSelected));
        OnPropertyChanged(nameof(CanUnstageSelected));
        OnPropertyChanged(nameof(CanDiscardSelected));
    }

    private static IReadOnlyList<SideBySideLine> BuildSideBySide(IReadOnlyList<DiffLineViewModel> lines)
    {
        var result = new List<SideBySideLine>();
        var removed = new List<DiffLineViewModel>();
        var added   = new List<DiffLineViewModel>();

        void Flush()
        {
            int count = Math.Max(removed.Count, added.Count);
            for (int i = 0; i < count; i++)
                result.Add(new SideBySideLine(
                    i < removed.Count ? removed[i] : null,
                    i < added.Count   ? added[i]   : null));
            removed.Clear();
            added.Clear();
        }

        foreach (var line in lines)
        {
            switch (line.Type)
            {
                case DiffLineType.Removed:
                    if (added.Count > 0) Flush();
                    removed.Add(line);
                    break;
                case DiffLineType.Added:
                    added.Add(line);
                    break;
                default:
                    Flush();
                    result.Add(new SideBySideLine(line, line));
                    break;
            }
        }
        Flush();
        return result;
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
            MessageBox.Show($"Stage hunk failed: {ex.Message}", Strings.Diff_StageHunk, MessageBoxButton.OK, MessageBoxImage.Error);
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
            MessageBox.Show($"Unstage hunk failed: {ex.Message}", Strings.Diff_UnstageHunk, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task DiscardHunkAsync()
    {
        if (MessageBox.Show(Strings.Dialog_DiscardHunk_Message,
                Strings.Dialog_DiscardHunk_Title, MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;
        try
        {
            await _service.DiscardHunkAsync(_filePath, _hunk);
            WeakReferenceMessenger.Default.Send(new WorkingTreeChangedMessage());
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Discard hunk failed: {ex.Message}", Strings.Diff_DiscardHunk, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task StageSelectedLinesAsync()
    {
        var indices = GetSelectedIndices();
        try
        {
            await _service.StageHunkLinesAsync(_filePath, _hunk, _isNew, indices);
            ClearLineSelection();
            WeakReferenceMessenger.Default.Send(new WorkingTreeChangedMessage());
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Stage lines failed: {ex.Message}", Strings.Diff_StageLines, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task UnstageSelectedLinesAsync()
    {
        var indices = GetSelectedIndices();
        try
        {
            await _service.UnstageHunkLinesAsync(_filePath, _hunk, indices);
            ClearLineSelection();
            WeakReferenceMessenger.Default.Send(new WorkingTreeChangedMessage());
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Unstage lines failed: {ex.Message}", Strings.Diff_UnstageLines, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task DiscardSelectedLinesAsync()
    {
        if (MessageBox.Show(Strings.Dialog_DiscardLines_Message,
                Strings.Dialog_DiscardHunk_Title, MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;
        var indices = GetSelectedIndices();
        try
        {
            await _service.DiscardHunkLinesAsync(_filePath, _hunk, indices);
            ClearLineSelection();
            WeakReferenceMessenger.Default.Send(new WorkingTreeChangedMessage());
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Discard lines failed: {ex.Message}", Strings.Diff_DiscardLines, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private IReadOnlySet<int> GetSelectedIndices() =>
        Lines.Select((l, i) => (l, i))
             .Where(x => x.l.IsSelected)
             .Select(x => x.i)
             .ToHashSet();

    private void ClearLineSelection()
    {
        foreach (var line in Lines)
            line.IsSelected = false;
    }
}

public sealed class SideBySideLine
{
    private static readonly Brush AddedBrush   = new SolidColorBrush(Color.FromArgb(60, 0x1A, 0x7F, 0x37));
    private static readonly Brush RemovedBrush = new SolidColorBrush(Color.FromArgb(60, 0xCF, 0x22, 0x2E));

    public string OldLineNo    { get; }
    public string NewLineNo    { get; }
    public string OldContent   { get; }
    public string NewContent   { get; }
    public string OldPrefix    { get; }
    public string NewPrefix    { get; }
    public Brush  OldBackground { get; }
    public Brush  NewBackground { get; }

    public SideBySideLine(DiffLineViewModel? oldLine, DiffLineViewModel? newLine)
    {
        OldLineNo     = oldLine?.OldLineNo  ?? string.Empty;
        NewLineNo     = newLine?.NewLineNo  ?? string.Empty;
        OldContent    = oldLine?.Content    ?? string.Empty;
        NewContent    = newLine?.Content    ?? string.Empty;
        OldPrefix     = oldLine is null ? string.Empty : oldLine.Type == DiffLineType.Removed ? "-" : " ";
        NewPrefix     = newLine is null ? string.Empty : newLine.Type == DiffLineType.Added   ? "+" : " ";
        OldBackground = oldLine?.Type == DiffLineType.Removed ? RemovedBrush : Brushes.Transparent;
        NewBackground = newLine?.Type == DiffLineType.Added   ? AddedBrush   : Brushes.Transparent;
    }
}

public sealed partial class DiffLineViewModel : ObservableObject
{
    private static readonly Brush AddedBrush   = new SolidColorBrush(Color.FromArgb(60, 0x1A, 0x7F, 0x37));
    private static readonly Brush RemovedBrush = new SolidColorBrush(Color.FromArgb(60, 0xCF, 0x22, 0x2E));

    [ObservableProperty] private bool _isSelected;

    public DiffLineType Type { get; }
    public string Content { get; }
    public string Prefix => Type == DiffLineType.Added ? "+" : Type == DiffLineType.Removed ? "-" : " ";
    public int? OldLineNumber { get; }
    public int? NewLineNumber { get; }
    public string OldLineNo => OldLineNumber?.ToString() ?? string.Empty;
    public string NewLineNo => NewLineNumber?.ToString() ?? string.Empty;
    public Brush Background => Type == DiffLineType.Added ? AddedBrush : Type == DiffLineType.Removed ? RemovedBrush : Brushes.Transparent;

    public bool IsSelectable => Type == DiffLineType.Added || Type == DiffLineType.Removed;
    // Hidden (takes layout space) for context lines so the checkbox column stays aligned
    public Visibility SelectableVisibility => IsSelectable ? Visibility.Visible : Visibility.Hidden;

    public DiffLineViewModel(DiffLine line)
    {
        Type = line.Type;
        Content = line.Content;
        OldLineNumber = line.OldLineNumber;
        NewLineNumber = line.NewLineNumber;
    }
}
