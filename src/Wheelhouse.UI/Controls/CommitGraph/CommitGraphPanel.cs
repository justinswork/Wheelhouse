using System.Windows;
using System.Windows.Media;

namespace Wheelhouse.UI.Controls.CommitGraph;

public sealed class CommitGraphPanel : FrameworkElement
{
    public static readonly DependencyProperty RowProperty =
        DependencyProperty.Register(nameof(Row), typeof(GraphRow), typeof(CommitGraphPanel),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty RowHeightProperty =
        DependencyProperty.Register(nameof(RowHeight), typeof(double), typeof(CommitGraphPanel),
            new FrameworkPropertyMetadata(24.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public GraphRow? Row
    {
        get => (GraphRow?)GetValue(RowProperty);
        set => SetValue(RowProperty, value);
    }

    public double RowHeight
    {
        get => (double)GetValue(RowHeightProperty);
        set => SetValue(RowHeightProperty, value);
    }

    private const double LaneWidth = 16.0;
    private const double NodeRadius = 5.0;
    private const double LineThickness = 2.0;

    protected override Size MeasureOverride(Size availableSize)
    {
        var lanes = Row?.TotalLanes ?? 1;
        return new Size(lanes * LaneWidth + LaneWidth, RowHeight);
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var row = Row;
        if (row is null) return;

        var midY = RowHeight / 2.0;
        var nodeX = row.Lane * LaneWidth + LaneWidth / 2.0;

        // Draw connections
        foreach (var conn in row.Connections)
        {
            var fromX = conn.FromLane * LaneWidth + LaneWidth / 2.0;
            var toX = conn.ToLane * LaneWidth + LaneWidth / 2.0;
            var pen = new Pen(new SolidColorBrush(conn.Color), LineThickness) { LineJoin = PenLineJoin.Round };

            switch (conn.Type)
            {
                case ConnectionType.Pass:
                    dc.DrawLine(pen, new Point(fromX, 0), new Point(fromX, RowHeight));
                    break;
                case ConnectionType.Start:
                    dc.DrawLine(pen, new Point(fromX, midY), new Point(fromX, RowHeight));
                    break;
                case ConnectionType.Merge:
                    var mergeGeo = new StreamGeometry();
                    using (var ctx = mergeGeo.Open())
                    {
                        ctx.BeginFigure(new Point(nodeX, midY), false, false);
                        ctx.BezierTo(new Point(nodeX, midY + 8), new Point(toX, midY - 8), new Point(toX, 0), true, true);
                    }
                    dc.DrawGeometry(null, pen, mergeGeo);
                    break;
                case ConnectionType.Fork:
                    var forkGeo = new StreamGeometry();
                    using (var ctx = forkGeo.Open())
                    {
                        ctx.BeginFigure(new Point(nodeX, midY), false, false);
                        ctx.BezierTo(new Point(nodeX, midY + 8), new Point(toX, midY - 8), new Point(toX, RowHeight), true, true);
                    }
                    dc.DrawGeometry(null, pen, forkGeo);
                    break;
            }
        }

        // Draw node circle
        var nodeBrush = new SolidColorBrush(row.Color);
        var nodePen = new Pen(new SolidColorBrush(Colors.White), 1.5);
        dc.DrawEllipse(nodeBrush, nodePen, new Point(nodeX, midY), NodeRadius, NodeRadius);
    }
}
