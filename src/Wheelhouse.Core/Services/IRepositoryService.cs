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
    Task CommitAsync(string message, bool amend = false, CancellationToken ct = default);

    Task FetchAsync(string? remoteName = null, CancellationToken ct = default);
    Task PullAsync(CancellationToken ct = default);
    Task PushAsync(string? remoteName = null, CancellationToken ct = default);
}
