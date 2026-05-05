using System.Windows.Media;

namespace Wheelhouse.UI.Controls.CommitGraph;

public enum ConnectionType { Pass, Start, End, Merge, Fork }

public sealed record LaneConnection(int FromLane, int ToLane, ConnectionType Type, Color Color);

public sealed record GraphRow(
    int Lane,
    Color Color,
    IReadOnlyList<LaneConnection> Connections,
    int TotalLanes);
