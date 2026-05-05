using System.Text.RegularExpressions;
using Wheelhouse.Core.Models;

namespace Wheelhouse.Core.Services;

internal static class DiffParser
{
    internal static IReadOnlyList<DiffHunk> ParseHunks(string rawPatch)
    {
        var hunks = new List<DiffHunk>();
        if (string.IsNullOrEmpty(rawPatch)) return hunks;

        DiffHunk? current = null;
        var lines = new List<DiffLine>();
        string? header = null;
        int oldLine = 0, newLine = 0;

        foreach (var line in rawPatch.Split('\n'))
        {
            if (line.StartsWith("@@"))
            {
                if (current is not null) hunks.Add(current with { Lines = lines.ToList() });
                header = line;
                lines = [];
                ParseHunkHeader(line, out oldLine, out newLine);
                current = new DiffHunk(header, []);
            }
            else if (current is not null)
            {
                if (line.StartsWith('+'))
                    lines.Add(new DiffLine(DiffLineType.Added, line[1..], null, newLine++));
                else if (line.StartsWith('-'))
                    lines.Add(new DiffLine(DiffLineType.Removed, line[1..], oldLine++, null));
                else
                    lines.Add(new DiffLine(DiffLineType.Context, line.Length > 0 ? line[1..] : line, oldLine++, newLine++));
            }
        }

        if (current is not null) hunks.Add(current with { Lines = lines.ToList() });
        return hunks;
    }

    private static void ParseHunkHeader(string header, out int oldStart, out int newStart)
    {
        oldStart = 1; newStart = 1;
        var match = Regex.Match(header, @"@@ -(\d+)(?:,\d+)? \+(\d+)");
        if (match.Success)
        {
            oldStart = int.Parse(match.Groups[1].Value);
            newStart = int.Parse(match.Groups[2].Value);
        }
    }
}
