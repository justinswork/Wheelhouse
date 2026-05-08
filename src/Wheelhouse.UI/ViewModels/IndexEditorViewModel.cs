using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ICSharpCode.AvalonEdit.Document;
using Wheelhouse.Core.Models;
using Wheelhouse.Core.Services;
using Wheelhouse.UI.Messages;
using Wheelhouse.UI.Properties;

namespace Wheelhouse.UI.ViewModels;

/// <summary>
/// Source of a hunk region — used to color-code highlights in the Index pane
/// (Index has changes both vs HEAD and vs Working Tree).
/// </summary>
public enum HunkSource { HeadIndex, IndexWt }

/// <summary>
/// A contiguous range of changed lines (1-based, inclusive) in one of the panes.
/// </summary>
public sealed record HunkRegion(int Start, int End, HunkSource Source);

public sealed partial class IndexEditorViewModel : ViewModelBase
{
    private readonly IRepositoryService _service;
    private readonly CancellationTokenSource _disposeCts = new();
    private CancellationTokenSource? _diffCts;

    private string _headContent = "";
    private string _wtContent = "";
    private IReadOnlyList<DiffHunk> _headIndexHunks = [];
    private IReadOnlyList<DiffHunk> _indexWtHunks = [];

    public string FilePath { get; }
    public string FileName => System.IO.Path.GetFileName(FilePath);

    public TextDocument HeadDocument { get; } = new();
    public TextDocument IndexDocument { get; } = new();
    public TextDocument WtDocument { get; } = new();

    [ObservableProperty] private bool _isModified;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private int _currentIndexCaretLine = 1;
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private bool _hasNoDifferences;
    [ObservableProperty] private bool _isHeadEmpty;
    [ObservableProperty] private bool _isWtEmpty;

    /// <summary>Hunk regions per pane (1-based inclusive line ranges). Used by the view's renderer.</summary>
    public IReadOnlyList<HunkRegion> HeadHunkRegions { get; private set; } = [];
    public IReadOnlyList<HunkRegion> IndexHunkRegions { get; private set; } = [];
    public IReadOnlyList<HunkRegion> WtHunkRegions { get; private set; } = [];

    /// <summary>Fires when diffs are recomputed; the view re-invalidates renderer layers.</summary>
    public event Action? DiffUpdated;

    /// <summary>Asks the view to scroll the Index pane to a particular 1-based line.</summary>
    public event Action<int>? NavigateIndexToLine;

    public bool CanTakeLeft => FindHunkContainingNewLine(_headIndexHunks, CurrentIndexCaretLine) is not null;
    public bool CanTakeRight => FindHunkContainingOldLine(_indexWtHunks, CurrentIndexCaretLine) is not null;

    public IndexEditorViewModel(string filePath, IRepositoryService service)
    {
        FilePath = filePath;
        _service = service;
        IndexDocument.TextChanged += OnIndexDocumentTextChanged;
        _ = LoadAsync(_disposeCts.Token);
    }

    partial void OnCurrentIndexCaretLineChanged(int value)
    {
        OnPropertyChanged(nameof(CanTakeLeft));
        OnPropertyChanged(nameof(CanTakeRight));
    }

    // ───────── Loading ─────────

    private async Task LoadAsync(CancellationToken ct)
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var headTask = _service.GetHeadFileContentAsync(FilePath, ct);
            var indexTask = _service.GetStagedFileContentAsync(FilePath, ct);
            var wtFullPath = System.IO.Path.Combine(
                _service.CurrentRepository!.Path,
                FilePath.Replace('/', System.IO.Path.DirectorySeparatorChar));
            var wtTask = System.IO.File.Exists(wtFullPath)
                ? System.IO.File.ReadAllTextAsync(wtFullPath, ct)
                : Task.FromResult("");

            await Task.WhenAll(headTask, indexTask, wtTask);
            ct.ThrowIfCancellationRequested();

            _headContent = await headTask;
            var indexContent = await indexTask;
            _wtContent = await wtTask;

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                IndexDocument.TextChanged -= OnIndexDocumentTextChanged;
                HeadDocument.Text = _headContent;
                IndexDocument.Text = indexContent;
                WtDocument.Text = _wtContent;
                IsModified = false;
                IsHeadEmpty = string.IsNullOrEmpty(_headContent);
                IsWtEmpty = string.IsNullOrEmpty(_wtContent);
                IndexDocument.TextChanged += OnIndexDocumentTextChanged;
            });

            await RecomputeDiffAsync(ct);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not load: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    // ───────── Diff recomputation (debounced) ─────────

    private async void OnIndexDocumentTextChanged(object? sender, EventArgs e)
    {
        IsModified = true;
        _diffCts?.Cancel();
        _diffCts?.Dispose();
        _diffCts = new CancellationTokenSource();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(_diffCts.Token, _disposeCts.Token);
        var ct = linked.Token;
        try
        {
            await Task.Delay(400, ct);
            await RecomputeDiffAsync(ct);
        }
        catch (OperationCanceledException) { }
    }

    private async Task RecomputeDiffAsync(CancellationToken ct)
    {
        string indexContent = Application.Current.Dispatcher.CheckAccess()
            ? IndexDocument.Text
            : await Application.Current.Dispatcher.InvokeAsync(
                () => IndexDocument.Text, DispatcherPriority.Normal, ct);

        var headIndexTask = _service.DiffContentsAsync(_headContent, indexContent, ct);
        var indexWtTask = _service.DiffContentsAsync(indexContent, _wtContent, ct);
        await Task.WhenAll(headIndexTask, indexWtTask);
        ct.ThrowIfCancellationRequested();

        _headIndexHunks = await headIndexTask;
        _indexWtHunks = await indexWtTask;

        UpdateHunkRegions();
        UpdateStatus();
        DiffUpdated?.Invoke();
        OnPropertyChanged(nameof(CanTakeLeft));
        OnPropertyChanged(nameof(CanTakeRight));
    }

    private void UpdateHunkRegions()
    {
        var head = new List<HunkRegion>();
        var index = new List<HunkRegion>();
        var wt = new List<HunkRegion>();

        foreach (var hunk in _headIndexHunks)
        {
            // HEAD pane: lines that exist in HEAD but were removed (or replaced) in Index
            var removed = hunk.Lines.Where(l => l.Type == DiffLineType.Removed && l.OldLineNumber.HasValue).ToList();
            if (removed.Count > 0)
                head.Add(new HunkRegion(
                    removed.Min(l => l.OldLineNumber!.Value),
                    removed.Max(l => l.OldLineNumber!.Value),
                    HunkSource.HeadIndex));

            // Index pane: lines added (or replacing) vs HEAD
            var added = hunk.Lines.Where(l => l.Type == DiffLineType.Added && l.NewLineNumber.HasValue).ToList();
            if (added.Count > 0)
                index.Add(new HunkRegion(
                    added.Min(l => l.NewLineNumber!.Value),
                    added.Max(l => l.NewLineNumber!.Value),
                    HunkSource.HeadIndex));
        }

        foreach (var hunk in _indexWtHunks)
        {
            // Index pane: lines that exist in Index but not in WT
            var removed = hunk.Lines.Where(l => l.Type == DiffLineType.Removed && l.OldLineNumber.HasValue).ToList();
            if (removed.Count > 0)
                index.Add(new HunkRegion(
                    removed.Min(l => l.OldLineNumber!.Value),
                    removed.Max(l => l.OldLineNumber!.Value),
                    HunkSource.IndexWt));

            // WT pane: lines that exist in WT but not in Index
            var added = hunk.Lines.Where(l => l.Type == DiffLineType.Added && l.NewLineNumber.HasValue).ToList();
            if (added.Count > 0)
                wt.Add(new HunkRegion(
                    added.Min(l => l.NewLineNumber!.Value),
                    added.Max(l => l.NewLineNumber!.Value),
                    HunkSource.IndexWt));
        }

        HeadHunkRegions = head;
        IndexHunkRegions = index;
        WtHunkRegions = wt;
    }

    private void UpdateStatus()
    {
        int headCount = _headIndexHunks.Count;
        int wtCount = _indexWtHunks.Count;
        HasNoDifferences = headCount == 0 && wtCount == 0;
        StatusText = HasNoDifferences
            ? Strings.IndexEditor_Status_NoChanges
            : string.Format(Strings.IndexEditor_Status_Format, headCount, wtCount);
    }

    // ───────── Take Left / Take Right ─────────

    [RelayCommand]
    private void TakeLeft(int indexCaretLine)
    {
        var hunk = FindHunkContainingNewLine(_headIndexHunks, indexCaretLine);
        if (hunk is null) return;

        ParseHunkRange(hunk.Header, out _, out _, out int newStart, out int newCount);

        // Replacement = HEAD's version of this hunk (Context + Removed lines, in source order).
        // The DiffParser strips the +/-/space prefix; we re-introduce the document's line ending.
        var replacement = ReconstructHunkContent(
            hunk.Lines.Where(l => l.Type != DiffLineType.Added),
            DetectDocumentLineEnding(IndexDocument));

        ReplaceIndexLines(newStart, newCount, replacement);
    }

    [RelayCommand]
    private void TakeRight(int indexCaretLine)
    {
        var hunk = FindHunkContainingOldLine(_indexWtHunks, indexCaretLine);
        if (hunk is null) return;

        ParseHunkRange(hunk.Header, out int oldStart, out int oldCount, out _, out _);

        // Replacement = WT's version (Context + Added lines).
        var replacement = ReconstructHunkContent(
            hunk.Lines.Where(l => l.Type != DiffLineType.Removed),
            DetectDocumentLineEnding(IndexDocument));

        ReplaceIndexLines(oldStart, oldCount, replacement);
    }

    /// <summary>
    /// Joins diff Content strings using the document's preferred line ending. Each Content has
    /// its source-file trailing CR stripped (CRLF safety) before re-joining.
    /// </summary>
    private static string ReconstructHunkContent(IEnumerable<DiffLine> lines, string lineEnding)
    {
        var contents = lines.Select(l =>
            l.Content.EndsWith('\r') ? l.Content[..^1] : l.Content).ToList();
        if (contents.Count == 0) return "";
        return string.Join(lineEnding, contents) + lineEnding;
    }

    private static string DetectDocumentLineEnding(TextDocument doc)
    {
        if (doc.LineCount == 0) return Environment.NewLine;
        // Look at the first line that actually has a delimiter
        for (int i = 1; i <= doc.LineCount; i++)
        {
            var line = doc.GetLineByNumber(i);
            if (line.DelimiterLength > 0)
                return doc.GetText(line.Offset + line.Length, line.DelimiterLength);
        }
        return Environment.NewLine;
    }

    /// <summary>
    /// Replaces a contiguous range of Index document lines (1-based) with new text. Handles
    /// the edge case where the document doesn't end with a newline.
    /// </summary>
    private void ReplaceIndexLines(int startLine, int lineCount, string replacement)
    {
        var doc = IndexDocument;
        // Hunks with oldCount/newCount = 0 represent pure insertions/deletions at boundaries.
        // For "delete N lines starting at lineX" we have lineCount > 0.
        // For "insert before lineX with no removal" we'd have lineCount = 0; in our usage
        // (Take Left/Right always operates on a hunk that has at least one Index-side line),
        // lineCount is always >= 1.
        if (lineCount <= 0) return;
        if (startLine < 1) startLine = 1;
        int endLine = startLine + lineCount - 1;
        if (endLine > doc.LineCount) endLine = doc.LineCount;
        if (endLine < startLine) return;

        var startOffset = doc.GetLineByNumber(startLine).Offset;
        var endDocLine = doc.GetLineByNumber(endLine);
        var endOffset = endDocLine.Offset + endDocLine.TotalLength;

        // If we're replacing through the last line and the doc has no trailing newline,
        // strip the trailing newline from the replacement so we don't add a phantom blank line.
        if (endLine == doc.LineCount && endDocLine.DelimiterLength == 0)
        {
            var ending = DetectDocumentLineEnding(doc);
            if (replacement.EndsWith(ending))
                replacement = replacement[..^ending.Length];
        }

        doc.BeginUpdate();
        try
        {
            doc.Replace(startOffset, endOffset - startOffset, replacement);
        }
        finally
        {
            doc.EndUpdate();
        }
    }

    // ───────── Navigation ─────────

    [RelayCommand]
    private void GoToPrevChange()
    {
        var lines = HunkStartLinesInIndex().OrderByDescending(x => x).ToList();
        if (lines.Count == 0) return;
        var target = lines.FirstOrDefault(l => l < CurrentIndexCaretLine);
        if (target == 0) target = lines[0]; // wrap to last
        NavigateIndexToLine?.Invoke(target);
    }

    [RelayCommand]
    private void GoToNextChange()
    {
        var lines = HunkStartLinesInIndex().OrderBy(x => x).ToList();
        if (lines.Count == 0) return;
        var target = lines.FirstOrDefault(l => l > CurrentIndexCaretLine);
        if (target == 0) target = lines[0]; // wrap to first
        NavigateIndexToLine?.Invoke(target);
    }

    private IEnumerable<int> HunkStartLinesInIndex()
    {
        var seen = new HashSet<int>();
        foreach (var region in IndexHunkRegions)
            if (seen.Add(region.Start))
                yield return region.Start;
    }

    // ───────── Apply / Reload ─────────

    [RelayCommand]
    private async Task ApplyToIndexAsync()
    {
        if (!IsModified) return;
        IsLoading = true;
        ErrorMessage = null;
        var content = IndexDocument.Text;
        try
        {
            await _service.SetStagedFileContentAsync(FilePath, content, _disposeCts.Token);
            IsModified = false;
            WeakReferenceMessenger.Default.Send(new WorkingTreeChangedMessage());
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Apply failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ReloadAsync()
    {
        if (IsModified)
        {
            var result = MessageBox.Show(
                Strings.IndexEditor_Confirm_Discard_Message,
                Strings.IndexEditor_Confirm_Discard_Title,
                MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;
        }
        await LoadAsync(_disposeCts.Token);
    }

    // ───────── Hunk lookup / line mapping (for view) ─────────

    /// <summary>Maps an Index line number to the corresponding HEAD line number (for scroll sync).</summary>
    public int MapIndexLineToHeadLine(int indexLine) =>
        MapLine(indexLine, _headIndexHunks, sourceIsNewSide: true);

    /// <summary>Maps an Index line number to the corresponding Working Tree line number.</summary>
    public int MapIndexLineToWtLine(int indexLine) =>
        MapLine(indexLine, _indexWtHunks, sourceIsNewSide: false);

    /// <summary>Maps a HEAD line number to the corresponding Index line number.</summary>
    public int MapHeadLineToIndexLine(int headLine) =>
        MapLine(headLine, _headIndexHunks, sourceIsNewSide: false);

    /// <summary>Maps a Working Tree line number to the corresponding Index line number.</summary>
    public int MapWtLineToIndexLine(int wtLine) =>
        MapLine(wtLine, _indexWtHunks, sourceIsNewSide: true);

    /// <summary>
    /// Generic line-number mapping through diff hunks.
    /// <para>
    /// <c>sourceIsNewSide=true</c>: source line is on the "new" (right) side of the diff;
    /// returns the corresponding "old" (left) side line.
    /// <c>sourceIsNewSide=false</c>: source is on "old" side; returns "new" side.
    /// </para>
    /// </summary>
    private static int MapLine(int sourceLine, IReadOnlyList<DiffHunk> hunks, bool sourceIsNewSide)
    {
        int delta = 0;
        foreach (var hunk in hunks)
        {
            ParseHunkRange(hunk.Header, out int oldStart, out int oldCount, out int newStart, out int newCount);
            int srcStart = sourceIsNewSide ? newStart : oldStart;
            int srcCount = sourceIsNewSide ? newCount : oldCount;
            int tgtCount = sourceIsNewSide ? oldCount : newCount;

            if (sourceLine < srcStart)
                return Math.Max(1, sourceLine + delta);

            if (sourceLine < srcStart + srcCount)
            {
                // Walk hunk lines to find exact match
                int curOld = oldStart, curNew = newStart;
                foreach (var line in hunk.Lines)
                {
                    int curSrc = sourceIsNewSide ? curNew : curOld;
                    int curTgt = sourceIsNewSide ? curOld : curNew;
                    if (curSrc == sourceLine)
                        return Math.Max(1, curTgt);
                    switch (line.Type)
                    {
                        case DiffLineType.Context: curOld++; curNew++; break;
                        case DiffLineType.Removed: curOld++; break;
                        case DiffLineType.Added:   curNew++; break;
                    }
                }
                // Fall-through: clamp to hunk end on target side
                return Math.Max(1, sourceIsNewSide ? curOld : curNew);
            }

            // Past this hunk
            delta += tgtCount - srcCount;
        }
        return Math.Max(1, sourceLine + delta);
    }

    private static void ParseHunkRange(string header, out int oldStart, out int oldCount, out int newStart, out int newCount)
    {
        oldStart = oldCount = newStart = newCount = 0;
        var match = Regex.Match(header, @"@@ -(\d+)(?:,(\d+))? \+(\d+)(?:,(\d+))?");
        if (!match.Success) return;
        oldStart = int.Parse(match.Groups[1].Value);
        oldCount = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 1;
        newStart = int.Parse(match.Groups[3].Value);
        newCount = match.Groups[4].Success ? int.Parse(match.Groups[4].Value) : 1;
    }

    private static DiffHunk? FindHunkContainingNewLine(IReadOnlyList<DiffHunk> hunks, int line)
    {
        foreach (var hunk in hunks)
        {
            ParseHunkRange(hunk.Header, out _, out _, out int newStart, out int newCount);
            if (newCount == 0) continue;
            if (line >= newStart && line < newStart + newCount)
                return hunk;
        }
        return null;
    }

    private static DiffHunk? FindHunkContainingOldLine(IReadOnlyList<DiffHunk> hunks, int line)
    {
        foreach (var hunk in hunks)
        {
            ParseHunkRange(hunk.Header, out int oldStart, out int oldCount, out _, out _);
            if (oldCount == 0) continue;
            if (line >= oldStart && line < oldStart + oldCount)
                return hunk;
        }
        return null;
    }
}
