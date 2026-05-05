namespace Wheelhouse.Core.Models;

public sealed record BranchInfo(
    string Name,
    string FriendlyName,
    bool IsCurrentRepositoryHead,
    bool IsRemote,
    string? TrackingRemoteName,
    string? UpstreamBranchCanonicalName,
    int AheadBy,
    int BehindBy,
    CommitInfo? Tip);
