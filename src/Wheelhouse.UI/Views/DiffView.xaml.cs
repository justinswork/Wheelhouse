using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Rendering;
using Wheelhouse.UI.ViewModels;

namespace Wheelhouse.UI.Views;

public partial class DiffView : UserControl
{
    private DiffViewModel? _viewModel;

    public DiffView()
    {
        InitializeComponent();
        DiffEditor.TextArea.TextView.BackgroundRenderers.Add(new DiffLineBackgroundRenderer(DiffEditor));
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel is not null)
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

        _viewModel = DataContext as DiffViewModel;

        if (_viewModel is not null)
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        UpdateText();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DiffViewModel.RawDiffText))
            Dispatcher.Invoke(UpdateText);
    }

    private void UpdateText()
    {
        DiffEditor.Text = _viewModel?.RawDiffText ?? string.Empty;
        DiffEditor.ScrollToHome();
    }
}

internal sealed class DiffLineBackgroundRenderer : IBackgroundRenderer
{
    private readonly TextEditor _editor;

    private static readonly SolidColorBrush AddedBrush    = new(Color.FromArgb(60, 0x1A, 0x7F, 0x37));
    private static readonly SolidColorBrush RemovedBrush  = new(Color.FromArgb(60, 0xCF, 0x22, 0x2E));
    private static readonly SolidColorBrush HeaderBrush   = new(Color.FromArgb(40, 0x09, 0x69, 0xDA));

    public KnownLayer Layer => KnownLayer.Background;

    public DiffLineBackgroundRenderer(TextEditor editor) => _editor = editor;

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        textView.EnsureVisualLines();
        foreach (var line in textView.VisualLines)
        {
            var docLine = line.FirstDocumentLine;
            if (docLine.Length == 0) continue;

            var text = _editor.Document.GetText(docLine.Offset, Math.Min(1, docLine.Length));
            SolidColorBrush? brush = text switch
            {
                "+" => AddedBrush,
                "-" => RemovedBrush,
                "@" => HeaderBrush,
                _   => null
            };

            if (brush is null) continue;

            var rect = BackgroundGeometryBuilder.GetRectsFromVisualSegment(
                textView, line, 0, docLine.Length);
            foreach (var r in rect)
                drawingContext.DrawRectangle(brush, null, r);
        }
    }
}
