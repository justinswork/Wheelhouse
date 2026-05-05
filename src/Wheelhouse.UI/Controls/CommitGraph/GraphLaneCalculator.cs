using System.Windows.Media;
using Wheelhouse.Core.Models;

namespace Wheelhouse.UI.Controls.CommitGraph;

public static class GraphLaneCalculator
{
    private static readonly Color[] LaneColors =
    [
        Color.FromRgb(0x1F, 0x6F, 0xEB),
        Color.FromRgb(0x3F, 0xB9, 0x50),
        Color.FromRgb(0xF8, 0x51, 0x49),
        Color.FromRgb(0xD2, 0x99, 0x22),
        Color.FromRgb(0xBC, 0x8C, 0xFF),
        Color.FromRgb(0xFF, 0x7B, 0x72),
        Color.FromRgb(0x58, 0xA6, 0xFF),
        Color.FromRgb(0x56, 0xD3, 0x64),
    ];

    public static IReadOnlyList<GraphRow> Calculate(IReadOnlyList<CommitInfo> commits)
    {
        var rows = new List<GraphRow>(commits.Count);

        // lane -> sha of the next expected commit in that lane (null = free)
        var lanes = new List<string?>();
        // sha -> color index
        var colorMap = new Dictionary<string, int>();
        int nextColor = 0;

        foreach (var commit in commits)
        {
            // Find which lane this commit belongs to
            int lane = lanes.IndexOf(commit.Sha);
            if (lane == -1)
            {
                // New branch head — claim first free lane or create one
                lane = lanes.IndexOf(null);
                if (lane == -1) { lane = lanes.Count; lanes.Add(null); }
                lanes[lane] = commit.Sha;
            }

            if (!colorMap.ContainsKey(commit.Sha))
                colorMap[commit.Sha] = nextColor++ % LaneColors.Length;

            var color = LaneColors[colorMap[commit.Sha]];
            var connections = new List<LaneConnection>();

            // Pass-through: all occupied lanes other than this one stay
            for (int i = 0; i < lanes.Count; i++)
            {
                if (i == lane || lanes[i] == null) continue;
                if (colorMap.TryGetValue(lanes[i]!, out var ci))
                    connections.Add(new LaneConnection(i, i, ConnectionType.Pass, LaneColors[ci]));
            }

            // Wire up parents
            var parents = commit.ParentShas;
            if (parents.Count == 0)
            {
                lanes[lane] = null;
            }
            else
            {
                // First parent continues this lane
                var firstParent = parents[0];
                lanes[lane] = firstParent;
                if (!colorMap.ContainsKey(firstParent))
                    colorMap[firstParent] = colorMap[commit.Sha];
                connections.Add(new LaneConnection(lane, lane, ConnectionType.Start, color));

                // Additional parents (merge sources) go to new lanes
                for (int p = 1; p < parents.Count; p++)
                {
                    var parentSha = parents[p];
                    int existingLane = lanes.IndexOf(parentSha);
                    if (existingLane >= 0)
                    {
                        // already tracked — draw merge line
                        if (!colorMap.TryGetValue(parentSha, out var pc)) pc = nextColor++ % LaneColors.Length;
                        connections.Add(new LaneConnection(lane, existingLane, ConnectionType.Merge, LaneColors[pc]));
                    }
                    else
                    {
                        int newLane = lanes.IndexOf(null);
                        if (newLane == -1) { newLane = lanes.Count; lanes.Add(null); }
                        lanes[newLane] = parentSha;
                        if (!colorMap.ContainsKey(parentSha))
                            colorMap[parentSha] = nextColor++ % LaneColors.Length;
                        connections.Add(new LaneConnection(lane, newLane, ConnectionType.Fork, LaneColors[colorMap[parentSha]]));
                    }
                }
            }

            // Compact: trim trailing nulls
            while (lanes.Count > 0 && lanes[^1] == null)
                lanes.RemoveAt(lanes.Count - 1);

            rows.Add(new GraphRow(lane, color, connections, Math.Max(lanes.Count, lane + 1)));
        }

        return rows;
    }
}
