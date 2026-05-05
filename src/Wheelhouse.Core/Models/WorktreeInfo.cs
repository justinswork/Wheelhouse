namespace Wheelhouse.Core.Models;

public sealed record WorktreeInfo(string Path, string? Branch, string? HeadSha, bool IsMain, bool IsLocked);
