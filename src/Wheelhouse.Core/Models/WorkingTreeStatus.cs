namespace Wheelhouse.Core.Models;

public sealed record WorkingTreeStatus(
    IReadOnlyList<FileStatusEntry> StagedEntries,
    IReadOnlyList<FileStatusEntry> UnstagedEntries,
    IReadOnlyList<FileStatusEntry> ConflictedEntries,
    IReadOnlyList<FileStatusEntry> UntrackedEntries);
