using Wheelhouse.Core.Models;

namespace Wheelhouse.Core.Services;

public interface IRepositoryService : IDisposable
{
    bool IsOpen { get; }
    RepositoryInfo? CurrentRepository { get; }

    void Open(string path);
    void Close();

    Task<WorkingTreeStatus> GetWorkingTreeStatusAsync(CancellationToken ct = default);
    Task<IReadOnlyList<BranchInfo>> GetBranchesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<CommitInfo>> GetCommitLogAsync(int skip = 0, int take = 200, CancellationToken ct = default);
    Task<IReadOnlyList<FileDiff>> GetStagedDiffAsync(CancellationToken ct = default);
    Task<IReadOnlyList<FileDiff>> GetUnstagedDiffAsync(CancellationToken ct = default);
    Task<FileDiff?> GetFileDiffAsync(string filePath, bool staged, CancellationToken ct = default);

    Task StageAsync(IEnumerable<string> filePaths, CancellationToken ct = default);
    Task UnstageAsync(IEnumerable<string> filePaths, CancellationToken ct = default);
    Task StageAllAsync(CancellationToken ct = default);
    Task UnstageAllAsync(CancellationToken ct = default);
    Task StageHunkAsync(string filePath, DiffHunk hunk, bool isNew, CancellationToken ct = default);
    Task UnstageHunkAsync(string filePath, DiffHunk hunk, CancellationToken ct = default);
    Task DiscardHunkAsync(string filePath, DiffHunk hunk, CancellationToken ct = default);
    Task StageHunkLinesAsync(string filePath, DiffHunk hunk, bool isNew, IReadOnlySet<int> selectedLineIndices, CancellationToken ct = default);
    Task UnstageHunkLinesAsync(string filePath, DiffHunk hunk, IReadOnlySet<int> selectedLineIndices, CancellationToken ct = default);
    Task DiscardHunkLinesAsync(string filePath, DiffHunk hunk, IReadOnlySet<int> selectedLineIndices, CancellationToken ct = default);

    // Index editor
    Task<string> GetStagedFileContentAsync(string filePath, CancellationToken ct = default);
    Task SetStagedFileContentAsync(string filePath, string content, CancellationToken ct = default);
    Task<string> GetHeadFileContentAsync(string filePath, CancellationToken ct = default);
    Task<IReadOnlyList<DiffHunk>> DiffContentsAsync(string leftContent, string rightContent, CancellationToken ct = default);
    Task CommitAsync(string message, bool amend = false, CancellationToken ct = default);

    Task FetchAsync(string? remoteName = null, CancellationToken ct = default);
    Task PullAsync(CancellationToken ct = default);
    Task PushAsync(string? remoteName = null, CancellationToken ct = default);

    // Branch management
    Task CheckoutBranchAsync(string friendlyName, CancellationToken ct = default);
    Task CreateBranchAsync(string name, string? startPoint = null, bool checkout = false, CancellationToken ct = default);
    Task DeleteBranchAsync(string friendlyName, bool force = false, CancellationToken ct = default);
    Task DeleteRemoteBranchAsync(string remoteName, string branchName, CancellationToken ct = default);
    Task RenameBranchAsync(string currentFriendlyName, string newName, CancellationToken ct = default);

    // Tags
    Task<IReadOnlyList<TagInfo>> GetTagsAsync(CancellationToken ct = default);
    Task CreateTagAsync(string name, string? targetSha = null, string? message = null, CancellationToken ct = default);
    Task DeleteTagAsync(string name, CancellationToken ct = default);
    Task PushTagAsync(string name, string? remoteName = null, CancellationToken ct = default);

    // Remotes
    Task<IReadOnlyList<RemoteInfo>> GetRemotesAsync(CancellationToken ct = default);
    Task AddRemoteAsync(string name, string url, CancellationToken ct = default);
    Task RemoveRemoteAsync(string name, CancellationToken ct = default);
    Task RenameRemoteAsync(string name, string newName, CancellationToken ct = default);
    Task PruneRemoteAsync(string name, CancellationToken ct = default);

    // Stash
    Task<IReadOnlyList<StashInfo>> GetStashesAsync(CancellationToken ct = default);
    Task StashAsync(string? message = null, bool includeUntracked = true, CancellationToken ct = default);
    Task ApplyStashAsync(int index, bool drop = false, CancellationToken ct = default);
    Task DropStashAsync(int index, CancellationToken ct = default);

    // Advanced git operations
    Task MergeAsync(string branchName, CancellationToken ct = default);
    Task RebaseAsync(string onto, CancellationToken ct = default);
    Task AbortRebaseAsync(CancellationToken ct = default);
    Task ContinueRebaseAsync(CancellationToken ct = default);
    Task ResetAsync(string target, ResetMode mode, CancellationToken ct = default);
    Task RevertAsync(string commitSha, CancellationToken ct = default);
    Task CherryPickAsync(string commitSha, CancellationToken ct = default);

    // Reflog / history / blame
    Task<IReadOnlyList<ReflogEntry>> GetReflogAsync(CancellationToken ct = default);
    Task<IReadOnlyList<CommitInfo>> GetFileHistoryAsync(string filePath, CancellationToken ct = default);
    Task<FileDiff?> GetCommitFileDiffAsync(string commitSha, string filePath, CancellationToken ct = default);
    Task<IReadOnlyList<BlameLine>> GetBlameAsync(string filePath, CancellationToken ct = default);

    // Worktrees
    Task<IReadOnlyList<WorktreeInfo>> GetWorktreesAsync(CancellationToken ct = default);
    Task AddWorktreeAsync(string worktreePath, string branch, bool createBranch, CancellationToken ct = default);
    Task RemoveWorktreeAsync(string worktreePath, bool force, CancellationToken ct = default);
    Task PruneWorktreesAsync(CancellationToken ct = default);
}
