namespace Wheelhouse.Core.Models;

public enum DiffLineType { Context, Added, Removed, Header }

public sealed record DiffLine(DiffLineType Type, string Content, int? OldLineNumber, int? NewLineNumber);

public sealed record DiffHunk(string Header, IReadOnlyList<DiffLine> Lines);

public sealed record FileDiff(
    string OldPath,
    string NewPath,
    bool IsBinary,
    bool IsNew,
    bool IsDeleted,
    bool IsRenamed,
    int LinesAdded,
    int LinesDeleted,
    IReadOnlyList<DiffHunk> Hunks);
