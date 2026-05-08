using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Rendering;
using Wheelhouse.UI.ViewModels;

namespace Wheelhouse.UI.Views;

public partial class IndexEditorView : UserControl
{
    private IndexEditorViewModel? _vm;
    private ScrollViewer? _headSv, _indexSv, _wtSv;
    private bool _syncing;
    private bool _scrollViewersAttached;

    public IndexEditorView()
    {
        InitializeComponent();
    }

    // ───────── Lifecycle ─────────

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // The TextEditor template may not be applied yet. Defer until after layout
        // so that the embedded ScrollViewer is reachable.
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            AttachScrollViewers();
            IndexEditor.Focus();
        });

        // Caret tracking — update VM so Take Left/Right CanExecute reflects current position.
        IndexEditor.TextArea.Caret.PositionChanged += OnIndexCaretChanged;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        DetachScrollViewers();
        IndexEditor.TextArea.Caret.PositionChanged -= OnIndexCaretChanged;
        if (_vm is not null)
        {
            _vm.DiffUpdated -= OnDiffUpdated;
            _vm.NavigateIndexToLine -= OnNavigateIndexToLine;
            _vm = null;
        }
        ClearOurRenderers();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_vm is not null)
        {
            _vm.DiffUpdated -= OnDiffUpdated;
            _vm.NavigateIndexToLine -= OnNavigateIndexToLine;
        }

        _vm = e.NewValue as IndexEditorViewModel;

        if (_vm is not null)
        {
            _vm.DiffUpdated += OnDiffUpdated;
            _vm.NavigateIndexToLine += OnNavigateIndexToLine;
            SetupRenderers(_vm);
            // Initial caret line snapshot
            _vm.CurrentIndexCaretLine = IndexEditor.TextArea.Caret.Line;
        }
        else
        {
            ClearOurRenderers();
        }
    }

    private void ClearOurRenderers()
    {
        foreach (var ed in new[] { HeadEditor, IndexEditor, WtEditor })
        {
            var renderers = ed.TextArea.TextView.BackgroundRenderers;
            for (int i = renderers.Count - 1; i >= 0; i--)
                if (renderers[i] is HunkRegionRenderer)
                    renderers.RemoveAt(i);
        }
    }

    private void OnIndexCaretChanged(object? sender, EventArgs e)
    {
        if (_vm is not null)
            _vm.CurrentIndexCaretLine = IndexEditor.TextArea.Caret.Line;
    }

    // ───────── Renderers ─────────

    private void SetupRenderers(IndexEditorViewModel vm)
    {
        // Subtle background + solid left bar gives clear hunk boundaries without obscuring text.
        var headBg  = Frozen(Color.FromArgb(0x28, 0xF8, 0x51, 0x49)); // red, ~16% alpha
        var headBar = Frozen(Color.FromArgb(0xFF, 0xD7, 0x3A, 0x49));
        var indexBg = Frozen(Color.FromArgb(0x28, 0xE0, 0xB1, 0x33)); // amber
        var indexBar = Frozen(Color.FromArgb(0xFF, 0xCF, 0x80, 0x32));
        var wtBg  = Frozen(Color.FromArgb(0x28, 0x4E, 0xC9, 0x4E)); // green
        var wtBar = Frozen(Color.FromArgb(0xFF, 0x23, 0x86, 0x36));

        SwapRenderer(HeadEditor, new HunkRegionRenderer(() => vm.HeadHunkRegions, headBg, headBar));
        SwapRenderer(IndexEditor, new HunkRegionRenderer(() => vm.IndexHunkRegions, indexBg, indexBar));
        SwapRenderer(WtEditor, new HunkRegionRenderer(() => vm.WtHunkRegions, wtBg, wtBar));
    }

    private static Brush Frozen(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }

    private static void SwapRenderer(TextEditor editor, IBackgroundRenderer renderer)
    {
        var renderers = editor.TextArea.TextView.BackgroundRenderers;
        // Remove only existing HunkRegionRenderer instances (preserve any built-in renderers)
        for (int i = renderers.Count - 1; i >= 0; i--)
            if (renderers[i] is HunkRegionRenderer)
                renderers.RemoveAt(i);
        renderers.Add(renderer);
    }

    private void OnDiffUpdated()
    {
        Dispatcher.Invoke(() =>
        {
            HeadEditor.TextArea.TextView.InvalidateLayer(KnownLayer.Background);
            IndexEditor.TextArea.TextView.InvalidateLayer(KnownLayer.Background);
            WtEditor.TextArea.TextView.InvalidateLayer(KnownLayer.Background);
        });
    }

    // ───────── Navigation ─────────

    private void OnNavigateIndexToLine(int lineNumber)
    {
        Dispatcher.Invoke(() =>
        {
            if (lineNumber < 1 || lineNumber > IndexEditor.Document.LineCount) return;
            IndexEditor.TextArea.Caret.Line = lineNumber;
            IndexEditor.TextArea.Caret.Column = 1;
            IndexEditor.ScrollToLine(lineNumber);
            IndexEditor.Focus();
        });
    }

    // ───────── Scroll synchronization ─────────

    private void AttachScrollViewers()
    {
        if (_scrollViewersAttached) return;
        _headSv = FindScrollViewer(HeadEditor);
        _indexSv = FindScrollViewer(IndexEditor);
        _wtSv = FindScrollViewer(WtEditor);

        if (_headSv is not null) _headSv.ScrollChanged += OnHeadScroll;
        if (_indexSv is not null) _indexSv.ScrollChanged += OnIndexScroll;
        if (_wtSv is not null) _wtSv.ScrollChanged += OnWtScroll;
        _scrollViewersAttached = true;
    }

    private void DetachScrollViewers()
    {
        if (!_scrollViewersAttached) return;
        if (_headSv is not null) _headSv.ScrollChanged -= OnHeadScroll;
        if (_indexSv is not null) _indexSv.ScrollChanged -= OnIndexScroll;
        if (_wtSv is not null) _wtSv.ScrollChanged -= OnWtScroll;
        _scrollViewersAttached = false;
    }

    private void OnIndexScroll(object sender, ScrollChangedEventArgs e) => SyncFromSource(SyncSource.Index, e);
    private void OnHeadScroll(object sender, ScrollChangedEventArgs e)  => SyncFromSource(SyncSource.Head,  e);
    private void OnWtScroll(object sender, ScrollChangedEventArgs e)    => SyncFromSource(SyncSource.Wt,    e);

    private enum SyncSource { Head, Index, Wt }

    private void SyncFromSource(SyncSource src, ScrollChangedEventArgs e)
    {
        if (_syncing || _vm is null) return;
        if (e.VerticalChange == 0 && e.HorizontalChange == 0) return;

        _syncing = true;
        try
        {
            var (sourceEditor, sourceSv) = src switch
            {
                SyncSource.Head  => (HeadEditor,  _headSv!),
                SyncSource.Index => (IndexEditor, _indexSv!),
                _                => (WtEditor,    _wtSv!),
            };

            // Compute fractional source line (e.g. 12.34 means "34% into line 12")
            double srcFracLine = GetFractionalLineFromOffset(sourceEditor, sourceSv.VerticalOffset);
            int srcLine = (int)Math.Floor(srcFracLine);
            double withinLine = srcFracLine - srcLine;

            // Map to Index line first, then from Index to other panes.
            int indexLine = src switch
            {
                SyncSource.Head  => _vm.MapHeadLineToIndexLine(srcLine),
                SyncSource.Index => srcLine,
                _                => _vm.MapWtLineToIndexLine(srcLine),
            };
            int headLine = src == SyncSource.Index ? _vm.MapIndexLineToHeadLine(indexLine)
                          : src == SyncSource.Head ? srcLine
                          : _vm.MapIndexLineToHeadLine(indexLine);
            int wtLine = src == SyncSource.Index ? _vm.MapIndexLineToWtLine(indexLine)
                        : src == SyncSource.Wt   ? srcLine
                        : _vm.MapIndexLineToWtLine(indexLine);

            if (e.VerticalChange != 0)
            {
                if (src != SyncSource.Index) ScrollEditorToFractionalLine(IndexEditor, _indexSv, indexLine + withinLine);
                if (src != SyncSource.Head)  ScrollEditorToFractionalLine(HeadEditor,  _headSv,  headLine  + withinLine);
                if (src != SyncSource.Wt)    ScrollEditorToFractionalLine(WtEditor,    _wtSv,    wtLine    + withinLine);
            }

            // Horizontal sync — direct copy, no line mapping needed.
            if (e.HorizontalChange != 0)
            {
                double h = sourceSv.HorizontalOffset;
                if (src != SyncSource.Index && _indexSv is not null) _indexSv.ScrollToHorizontalOffset(h);
                if (src != SyncSource.Head  && _headSv  is not null) _headSv.ScrollToHorizontalOffset(h);
                if (src != SyncSource.Wt    && _wtSv    is not null) _wtSv.ScrollToHorizontalOffset(h);
            }
        }
        finally
        {
            _syncing = false;
        }
    }

    private static double GetFractionalLineFromOffset(TextEditor editor, double verticalOffset)
    {
        var visualLine = editor.TextArea.TextView.GetVisualLineFromVisualTop(verticalOffset);
        if (visualLine is null)
        {
            // We're past the last visual line — clamp to end of document.
            return Math.Max(1, editor.Document.LineCount);
        }
        int lineNum = visualLine.FirstDocumentLine?.LineNumber ?? 1;
        double height = Math.Max(1, visualLine.Height);
        double withinLine = (verticalOffset - visualLine.VisualTop) / height;
        return lineNum + Math.Clamp(withinLine, 0, 0.999);
    }

    private static void ScrollEditorToFractionalLine(TextEditor editor, ScrollViewer? sv, double fractionalLine)
    {
        if (sv is null) return;
        int lineCount = editor.Document.LineCount;
        if (lineCount == 0) return;

        int line = (int)Math.Floor(fractionalLine);
        line = Math.Clamp(line, 1, lineCount);
        double withinLine = Math.Clamp(fractionalLine - line, 0, 0.999);

        var docLine = editor.Document.GetLineByNumber(line);
        var visualLine = editor.TextArea.TextView.GetOrConstructVisualLine(docLine);
        double offset = visualLine.VisualTop + withinLine * Math.Max(1, visualLine.Height);

        // Clamp to the scrollviewer's range to avoid invalid offsets.
        offset = Math.Clamp(offset, 0, Math.Max(0, sv.ExtentHeight - sv.ViewportHeight));
        sv.ScrollToVerticalOffset(offset);
    }

    private static ScrollViewer? FindScrollViewer(DependencyObject parent)
    {
        // Prefer the named template part if present (TextEditor's template names it PART_ScrollViewer)
        if (parent is Control ctl && ctl.Template is { } tmpl)
        {
            var named = tmpl.FindName("PART_ScrollViewer", ctl) as ScrollViewer
                     ?? tmpl.FindName("scrollViewer", ctl) as ScrollViewer;
            if (named is not null) return named;
        }
        // Fall back to a depth-first walk
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is ScrollViewer sv) return sv;
            var deeper = FindScrollViewer(child);
            if (deeper is not null) return deeper;
        }
        return null;
    }

    // ───────── Keyboard / button click handlers ─────────

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_vm is null) return;

        // F3 / Shift+F3: navigate changes
        if (e.Key == Key.F3)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                _vm.GoToPrevChangeCommand.Execute(null);
            else
                _vm.GoToNextChangeCommand.Execute(null);
            e.Handled = true;
            return;
        }

        // Alt+Left / Alt+Right: take left / take right
        if ((Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
        {
            if (e.SystemKey == Key.Left)
            {
                if (_vm.CanTakeLeft) _vm.TakeLeftCommand.Execute(IndexEditor.TextArea.Caret.Line);
                e.Handled = true;
                return;
            }
            if (e.SystemKey == Key.Right)
            {
                if (_vm.CanTakeRight) _vm.TakeRightCommand.Execute(IndexEditor.TextArea.Caret.Line);
                e.Handled = true;
                return;
            }
        }

        // Ctrl+S: apply
        if (e.Key == Key.S && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            if (_vm.IsModified) _vm.ApplyToIndexCommand.Execute(null);
            e.Handled = true;
            return;
        }
    }

    private void OnTakeLeftClick(object sender, RoutedEventArgs e)
    {
        if (_vm is null || !_vm.CanTakeLeft) return;
        _vm.TakeLeftCommand.Execute(IndexEditor.TextArea.Caret.Line);
        IndexEditor.Focus();
    }

    private void OnTakeRightClick(object sender, RoutedEventArgs e)
    {
        if (_vm is null || !_vm.CanTakeRight) return;
        _vm.TakeRightCommand.Execute(IndexEditor.TextArea.Caret.Line);
        IndexEditor.Focus();
    }

    private void OnPrevChangeClick(object sender, RoutedEventArgs e) =>
        _vm?.GoToPrevChangeCommand.Execute(null);

    private void OnNextChangeClick(object sender, RoutedEventArgs e) =>
        _vm?.GoToNextChangeCommand.Execute(null);

    // ───────── Hunk-region renderer ─────────

    /// <summary>
    /// Draws each contiguous hunk region as a single rectangle (subtle background) plus
    /// a solid left bar — gives clear visual hunk boundaries without obscuring the text.
    /// </summary>
    private sealed class HunkRegionRenderer : IBackgroundRenderer
    {
        private const double LeftBarWidth = 4.0;
        private readonly Func<IReadOnlyList<HunkRegion>> _getRegions;
        private readonly Brush _bgBrush;
        private readonly Brush _barBrush;

        public HunkRegionRenderer(Func<IReadOnlyList<HunkRegion>> getRegions, Brush bgBrush, Brush barBrush)
        {
            _getRegions = getRegions;
            _bgBrush = bgBrush;
            _barBrush = barBrush;
        }

        public KnownLayer Layer => KnownLayer.Background;

        public void Draw(TextView textView, DrawingContext drawingContext)
        {
            var regions = _getRegions();
            if (regions.Count == 0) return;
            int lineCount = textView.Document?.LineCount ?? 0;
            if (lineCount == 0) return;

            foreach (var region in regions)
            {
                int start = Math.Max(1, region.Start);
                int end = Math.Min(region.End, lineCount);
                if (start > end) continue;

                // Find the visible rectangle covering this region by walking lines.
                double? top = null;
                double bottom = 0;
                for (int n = start; n <= end; n++)
                {
                    var docLine = textView.Document!.GetLineByNumber(n);
                    foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, docLine))
                    {
                        if (top is null) top = rect.Top;
                        bottom = rect.Bottom;
                    }
                }

                if (top is null) continue;
                double height = bottom - top.Value;
                if (height <= 0) continue;

                drawingContext.DrawRectangle(_bgBrush, null,
                    new Rect(0, top.Value, textView.ActualWidth, height));
                drawingContext.DrawRectangle(_barBrush, null,
                    new Rect(0, top.Value, LeftBarWidth, height));
            }
        }
    }
}
