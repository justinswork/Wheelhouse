namespace Wheelhouse.Core.Models;

public enum FileState
{
    Unmodified,
    Added,
    Modified,
    Deleted,
    Renamed,
    Copied,
    Untracked,
    Conflicted,
    Ignored
}

public sealed record FileStatusEntry(
    string FilePath,
    string? OldFilePath,
    FileState IndexState,
    FileState WorkingTreeState);
