using LibGit2Sharp;
using Microsoft.Extensions.Logging;
using Wheelhouse.Core.Models;

namespace Wheelhouse.Core.Services;

public sealed class LibGit2SharpRepositoryService : IRepositoryService
{
    private readonly ILogger<LibGit2SharpRepositoryService> _logger;
    // libgit2 is not thread-safe; this gate serializes all access to _repo across concurrent Task.Run calls
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Repository? _repo;

    public bool IsOpen => _repo is not null;
    public RepositoryInfo? CurrentRepository { get; private set; }

    public LibGit2SharpRepositoryService(ILogger<LibGit2SharpRepositoryService> logger)
    {
        _logger = logger;
    }

    public void Open(string path)
    {
        Close();
        _repo = new Repository(path);
        var remote = _repo.Network.Remotes.FirstOrDefault()?.Url;
        CurrentRepository = new RepositoryInfo(
            Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar)),
            path,
            remote,
            _repo.Info.IsBare);
        _logger.LogInformation("Opened repository at {Path}", path);
    }

    public void Close()
    {
        _repo?.Dispose();
        _repo = null;
        CurrentRepository = null;
    }

    public async Task<WorkingTreeStatus> GetWorkingTreeStatusAsync(CancellationToken ct = default)
    {
        EnsureOpen();
        try
        {
            return await Lib(() => GetStatusViaLibGit2(), ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "libgit2 status failed, falling back to git.exe");
            return await GitCli.GetStatusAsync(CurrentRepository!.Path, ct);
        }
    }

    public Task<IReadOnlyList<BranchInfo>> GetBranchesAsync(CancellationToken ct = default)
    {
        EnsureOpen();
        return Lib<IReadOnlyList<BranchInfo>>(() =>
            _repo!.Branches
                .Select(b => new BranchInfo(
                    b.CanonicalName,
                    b.FriendlyName,
                    b.IsCurrentRepositoryHead,
                    b.IsRemote,
                    b.RemoteName,
                    b.UpstreamBranchCanonicalName,
                    b.TrackingDetails.AheadBy ?? 0,
                    b.TrackingDetails.BehindBy ?? 0,
                    b.Tip is null ? null : MapCommit(b.Tip)))
                .ToList(),
            ct);
    }

    public Task<IReadOnlyList<CommitInfo>> GetCommitLogAsync(int skip = 0, int take = 200, CancellationToken ct = default)
    {
        EnsureOpen();
        return Lib<IReadOnlyList<CommitInfo>>(() =>
            _repo!.Commits
                .QueryBy(new CommitFilter { SortBy = CommitSortStrategies.Topological | CommitSortStrategies.Time })
                .Skip(skip)
                .Take(take)
                .Select(MapCommit)
                .ToList(),
            ct);
    }

    public async Task<IReadOnlyList<FileDiff>> GetStagedDiffAsync(CancellationToken ct = default)
    {
        EnsureOpen();
        try
        {
            return await Lib(() =>
            {
                var diff = _repo!.Diff.Compare<Patch>(_repo.Head.Tip?.Tree, DiffTargets.Index);
                return MapPatch(diff);
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "libgit2 staged diff failed, falling back to git.exe");
            var status = await GitCli.GetStatusAsync(CurrentRepository!.Path, ct);
            var diffs = new List<FileDiff>();
            foreach (var entry in status.StagedEntries)
            {
                var d = await GitCli.GetFileDiffAsync(CurrentRepository.Path, entry.FilePath, staged: true, ct);
                if (d is not null) diffs.Add(d);
            }
            return diffs;
        }
    }

    public Task<IReadOnlyList<FileDiff>> GetUnstagedDiffAsync(CancellationToken ct = default)
    {
        EnsureOpen();
        return Lib<IReadOnlyList<FileDiff>>(() =>
        {
            var diff = _repo!.Diff.Compare<Patch>(_repo.Head.Tip?.Tree, DiffTargets.WorkingDirectory);
            return MapPatch(diff);
        }, ct);
    }

    public async Task<FileDiff?> GetFileDiffAsync(string filePath, bool staged, CancellationToken ct = default)
    {
        EnsureOpen();
        IReadOnlyList<FileDiff>? mapped = null;
        try
        {
            mapped = await Lib(() =>
            {
                var diff = staged
                    ? _repo!.Diff.Compare<Patch>(_repo.Head.Tip?.Tree, DiffTargets.Index)
                    : _repo!.Diff.Compare<Patch>(_repo.Head.Tip?.Tree, DiffTargets.WorkingDirectory);
                return MapPatch(diff);
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "libgit2 diff failed for {Path}, falling back to git.exe", filePath);
            return await GitCli.GetFileDiffAsync(CurrentRepository!.Path, filePath, staged, ct);
        }

        var result = mapped.FirstOrDefault(f => f.NewPath == filePath || f.OldPath == filePath);
        if (result is null && !staged)
            return await GitCli.GetFileDiffAsync(CurrentRepository!.Path, filePath, staged: false, ct);
        return result;
    }

    public async Task StageAsync(IEnumerable<string> filePaths, CancellationToken ct = default)
    {
        EnsureOpen();
        var paths = filePaths.ToList();
        try { await Lib(() => Commands.Stage(_repo!, paths), ct); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "libgit2 stage failed, falling back to git.exe");
            await GitCli.StageAsync(CurrentRepository!.Path, paths, ct);
        }
    }

    public async Task UnstageAsync(IEnumerable<string> filePaths, CancellationToken ct = default)
    {
        EnsureOpen();
        var paths = filePaths.ToList();
        try { await Lib(() => Commands.Unstage(_repo!, paths), ct); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "libgit2 unstage failed, falling back to git.exe");
            await GitCli.UnstageAsync(CurrentRepository!.Path, paths, ct);
        }
    }

    public async Task StageAllAsync(CancellationToken ct = default)
    {
        EnsureOpen();
        try { await Lib(() => Commands.Stage(_repo!, "*"), ct); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "libgit2 stage-all failed, falling back to git.exe");
            await GitCli.StageAllAsync(CurrentRepository!.Path, ct);
        }
    }

    public async Task UnstageAllAsync(CancellationToken ct = default)
    {
        EnsureOpen();
        try { await Lib(() => Commands.Unstage(_repo!, "*"), ct); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "libgit2 unstage-all failed, falling back to git.exe");
            await GitCli.UnstageAllAsync(CurrentRepository!.Path, ct);
        }
    }

    public Task StageHunkAsync(string filePath, DiffHunk hunk, bool isNew, CancellationToken ct = default)
    {
        EnsureOpen();
        return GitCli.StageHunkAsync(CurrentRepository!.Path, filePath, hunk, isNew, ct);
    }

    public Task UnstageHunkAsync(string filePath, DiffHunk hunk, CancellationToken ct = default)
    {
        EnsureOpen();
        return GitCli.UnstageHunkAsync(CurrentRepository!.Path, filePath, hunk, ct);
    }

    public Task DiscardHunkAsync(string filePath, DiffHunk hunk, CancellationToken ct = default)
    {
        EnsureOpen();
        return GitCli.DiscardHunkAsync(CurrentRepository!.Path, filePath, hunk, ct);
    }

    public async Task CommitAsync(string message, bool amend = false, CancellationToken ct = default)
    {
        EnsureOpen();
        try
        {
            await Lib(() =>
            {
                var signature = _repo!.Config.BuildSignature(DateTimeOffset.Now);
                if (amend)
                    _repo.Commit(message, signature, signature, new CommitOptions { AmendPreviousCommit = true });
                else
                    _repo.Commit(message, signature, signature);
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "libgit2 commit failed, falling back to git.exe");
            await GitCli.CommitAsync(CurrentRepository!.Path, message, amend, ct);
        }
    }

    public async Task CheckoutBranchAsync(string friendlyName, CancellationToken ct = default)
    {
        EnsureOpen();
        try
        {
            await Lib(() =>
            {
                var branch = _repo!.Branches[friendlyName]
                    ?? throw new InvalidOperationException($"Branch '{friendlyName}' not found.");
                Commands.Checkout(_repo!, branch);
            }, ct);
        }
        catch (Exception ex) when (ex is not InvalidOperationException { Message: var m } || !m.StartsWith("Branch"))
        {
            _logger.LogWarning(ex, "libgit2 checkout failed, falling back to git.exe");
            await GitCli.CheckoutBranchAsync(CurrentRepository!.Path, friendlyName, ct);
        }
    }

    public async Task CreateBranchAsync(string name, string? startPoint = null, bool checkout = false, CancellationToken ct = default)
    {
        EnsureOpen();
        try
        {
            await Lib(() =>
            {
                Commit? startCommit = startPoint is null
                    ? _repo!.Head.Tip
                    : (_repo!.Lookup<Commit>(startPoint) ?? _repo!.Branches[startPoint]?.Tip);
                if (startCommit is null) throw new InvalidOperationException($"Start point '{startPoint}' not found.");
                var newBranch = _repo!.CreateBranch(name, startCommit);
                if (checkout) Commands.Checkout(_repo!, newBranch);
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "libgit2 create-branch failed, falling back to git.exe");
            await GitCli.CreateBranchAsync(CurrentRepository!.Path, name, startPoint, checkout, ct);
        }
    }

    public Task DeleteRemoteBranchAsync(string remoteName, string branchName, CancellationToken ct = default)
    {
        EnsureOpen();
        return GitCli.DeleteRemoteBranchAsync(CurrentRepository!.Path, remoteName, branchName, ct);
    }

    public async Task DeleteBranchAsync(string friendlyName, bool force = false, CancellationToken ct = default)
    {
        EnsureOpen();
        try
        {
            await Lib(() =>
            {
                var branch = _repo!.Branches[friendlyName]
                    ?? throw new InvalidOperationException($"Branch '{friendlyName}' not found.");
                _repo!.Branches.Remove(branch);
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "libgit2 delete-branch failed, falling back to git.exe");
            await GitCli.DeleteBranchAsync(CurrentRepository!.Path, friendlyName, force, ct);
        }
    }

    public async Task RenameBranchAsync(string currentFriendlyName, string newName, CancellationToken ct = default)
    {
        EnsureOpen();
        try
        {
            await Lib(() =>
            {
                var branch = _repo!.Branches[currentFriendlyName]
                    ?? throw new InvalidOperationException($"Branch '{currentFriendlyName}' not found.");
                _repo!.Branches.Rename(branch, newName);
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "libgit2 rename-branch failed, falling back to git.exe");
            await GitCli.RenameBranchAsync(CurrentRepository!.Path, currentFriendlyName, newName, ct);
        }
    }

    public Task<IReadOnlyList<TagInfo>> GetTagsAsync(CancellationToken ct = default)
    {
        EnsureOpen();
        try
        {
            return Lib<IReadOnlyList<TagInfo>>(() =>
                _repo!.Tags.Select(t =>
                {
                    var annotated = t.PeeledTarget is Commit;
                    var sha = t.PeeledTarget?.Sha ?? t.Target?.Sha ?? string.Empty;
                    DateTimeOffset? when = null;
                    string? msg = null;
                    if (t.Annotation is { } ann)
                    {
                        when = ann.Tagger.When;
                        msg = ann.Message?.Trim();
                    }
                    return new TagInfo(t.FriendlyName, sha, annotated, when, string.IsNullOrEmpty(msg) ? null : msg);
                }).ToList(),
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "libgit2 get-tags failed, falling back to git.exe");
            return GitCli.GetTagsAsync(CurrentRepository!.Path, ct);
        }
    }

    public async Task CreateTagAsync(string name, string? targetSha = null, string? message = null, CancellationToken ct = default)
    {
        EnsureOpen();
        try
        {
            await Lib(() =>
            {
                var target = targetSha is not null
                    ? _repo!.Lookup<Commit>(targetSha) ?? throw new InvalidOperationException($"Commit '{targetSha}' not found.")
                    : _repo!.Head.Tip;
                var sig = _repo!.Config.BuildSignature(DateTimeOffset.Now);
                if (message is not null)
                    _repo!.Tags.Add(name, target, sig, message);
                else
                    _repo!.Tags.Add(name, target);
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "libgit2 create-tag failed, falling back to git.exe");
            await GitCli.CreateTagAsync(CurrentRepository!.Path, name, targetSha, message, ct);
        }
    }

    public async Task DeleteTagAsync(string name, CancellationToken ct = default)
    {
        EnsureOpen();
        try { await Lib(() => _repo!.Tags.Remove(name), ct); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "libgit2 delete-tag failed, falling back to git.exe");
            await GitCli.DeleteTagAsync(CurrentRepository!.Path, name, ct);
        }
    }

    public Task PushTagAsync(string name, string? remoteName = null, CancellationToken ct = default)
    {
        EnsureOpen();
        return GitCli.PushTagAsync(CurrentRepository!.Path, name, remoteName, ct);
    }

    public Task<IReadOnlyList<RemoteInfo>> GetRemotesAsync(CancellationToken ct = default)
    {
        EnsureOpen();
        try
        {
            return Lib<IReadOnlyList<RemoteInfo>>(() =>
                _repo!.Network.Remotes.Select(r => new RemoteInfo(r.Name, r.Url)).ToList(),
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "libgit2 get-remotes failed, falling back to git.exe");
            return GitCli.GetRemotesAsync(CurrentRepository!.Path, ct);
        }
    }

    public async Task AddRemoteAsync(string name, string url, CancellationToken ct = default)
    {
        EnsureOpen();
        try { await Lib(() => _repo!.Network.Remotes.Add(name, url), ct); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "libgit2 add-remote failed, falling back to git.exe");
            await GitCli.AddRemoteAsync(CurrentRepository!.Path, name, url, ct);
        }
    }

    public async Task RemoveRemoteAsync(string name, CancellationToken ct = default)
    {
        EnsureOpen();
        try { await Lib(() => _repo!.Network.Remotes.Remove(name), ct); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "libgit2 remove-remote failed, falling back to git.exe");
            await GitCli.RemoveRemoteAsync(CurrentRepository!.Path, name, ct);
        }
    }

    public async Task RenameRemoteAsync(string name, string newName, CancellationToken ct = default)
    {
        EnsureOpen();
        try { await Lib(() => _repo!.Network.Remotes.Rename(name, newName), ct); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "libgit2 rename-remote failed, falling back to git.exe");
            await GitCli.RenameRemoteAsync(CurrentRepository!.Path, name, newName, ct);
        }
    }

    public Task PruneRemoteAsync(string name, CancellationToken ct = default)
    {
        EnsureOpen();
        return GitCli.PruneRemoteAsync(CurrentRepository!.Path, name, ct);
    }

    public Task<IReadOnlyList<StashInfo>> GetStashesAsync(CancellationToken ct = default)
    {
        EnsureOpen();
        return GitCli.GetStashesAsync(CurrentRepository!.Path, ct);
    }

    public Task StashAsync(string? message = null, bool includeUntracked = true, CancellationToken ct = default)
    {
        EnsureOpen();
        return GitCli.StashAsync(CurrentRepository!.Path, message, includeUntracked, ct);
    }

    public async Task ApplyStashAsync(int index, bool drop = false, CancellationToken ct = default)
    {
        EnsureOpen();
        if (drop)
            await GitCli.PopStashAsync(CurrentRepository!.Path, index, ct);
        else
            await GitCli.ApplyStashAsync(CurrentRepository!.Path, index, ct);
    }

    public Task DropStashAsync(int index, CancellationToken ct = default)
    {
        EnsureOpen();
        return GitCli.DropStashAsync(CurrentRepository!.Path, index, ct);
    }

    public async Task MergeAsync(string branchName, CancellationToken ct = default)
    {
        EnsureOpen();
        try
        {
            await Lib(() =>
            {
                var branch = _repo!.Branches[branchName]
                    ?? throw new InvalidOperationException($"Branch '{branchName}' not found.");
                var sig = _repo!.Config.BuildSignature(DateTimeOffset.Now);
                var result = _repo!.Merge(branch, sig, new MergeOptions());
                if (result.Status == MergeStatus.Conflicts)
                    throw new InvalidOperationException("Merge resulted in conflicts. Resolve conflicts and commit.");
            }, ct);
        }
        catch (Exception ex) when (ex.Message != "Merge resulted in conflicts. Resolve conflicts and commit.")
        {
            _logger.LogWarning(ex, "libgit2 merge failed, falling back to git.exe");
            await GitCli.MergeAsync(CurrentRepository!.Path, branchName, ct);
        }
    }

    public async Task ResetAsync(string target, Models.ResetMode mode, CancellationToken ct = default)
    {
        EnsureOpen();
        try
        {
            await Lib(() =>
            {
                var commit = _repo!.Lookup<Commit>(target)
                    ?? throw new InvalidOperationException($"Commit '{target}' not found.");
                _repo!.Reset((LibGit2Sharp.ResetMode)(int)mode, commit);
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "libgit2 reset failed, falling back to git.exe");
            await GitCli.ResetAsync(CurrentRepository!.Path, target, mode, ct);
        }
    }

    public Task RevertAsync(string commitSha, CancellationToken ct = default)
    {
        EnsureOpen();
        return GitCli.RevertAsync(CurrentRepository!.Path, commitSha, ct);
    }

    public Task CherryPickAsync(string commitSha, CancellationToken ct = default)
    {
        EnsureOpen();
        return GitCli.CherryPickAsync(CurrentRepository!.Path, commitSha, ct);
    }

    public Task FetchAsync(string? remoteName = null, CancellationToken ct = default)
    {
        EnsureOpen();
        return Lib(() =>
        {
            var remotes = remoteName is not null
                ? (IEnumerable<Remote>)[_repo!.Network.Remotes[remoteName]]
                : _repo!.Network.Remotes.AsEnumerable();
            foreach (var remote in remotes)
                Commands.Fetch(_repo!, remote.Name, [], null, null);
        }, ct);
    }

    public Task PullAsync(CancellationToken ct = default)
    {
        EnsureOpen();
        return Lib(() =>
        {
            var signature = _repo!.Config.BuildSignature(DateTimeOffset.Now);
            Commands.Pull(_repo!, signature, null);
        }, ct);
    }

    public Task PushAsync(string? remoteName = null, CancellationToken ct = default)
    {
        EnsureOpen();
        return Lib(() =>
        {
            var remote = remoteName is not null
                ? _repo!.Network.Remotes[remoteName]
                : _repo!.Network.Remotes.FirstOrDefault();
            if (remote is null) return;
            _repo!.Network.Push(_repo.Head, new PushOptions());
        }, ct);
    }

    public void Dispose() => Close();

    // Serializes all libgit2 access — libgit2 is not thread-safe and multiple ViewModels
    // fire concurrent async refreshes when a repository opens.
    private async Task<T> Lib<T>(Func<T> work, CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try { return await Task.Run(work, ct).ConfigureAwait(false); }
        finally { _gate.Release(); }
    }

    private async Task Lib(Action work, CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try { await Task.Run(work, ct).ConfigureAwait(false); }
        finally { _gate.Release(); }
    }

    private void EnsureOpen()
    {
        if (_repo is null) throw new InvalidOperationException("No repository is open.");
    }

    private WorkingTreeStatus GetStatusViaLibGit2()
    {
        var status = _repo!.RetrieveStatus(new StatusOptions());

        var staged = status
            .Where(e => e.State.HasFlag(FileStatus.NewInIndex)
                     || e.State.HasFlag(FileStatus.ModifiedInIndex)
                     || e.State.HasFlag(FileStatus.DeletedFromIndex)
                     || e.State.HasFlag(FileStatus.RenamedInIndex))
            .Select(e => MapEntry(e, staged: true))
            .ToList();

        var unstaged = status
            .Where(e => e.State.HasFlag(FileStatus.ModifiedInWorkdir)
                     || e.State.HasFlag(FileStatus.DeletedFromWorkdir))
            .Select(e => MapEntry(e, staged: false))
            .ToList();

        var conflicted = status
            .Where(e => e.State.HasFlag(FileStatus.Conflicted))
            .Select(e => MapEntry(e, staged: false))
            .ToList();

        var untracked = status
            .Where(e => e.State.HasFlag(FileStatus.NewInWorkdir))
            .Select(e => MapEntry(e, staged: false))
            .ToList();

        return new WorkingTreeStatus(staged, unstaged, conflicted, untracked);
    }

    private static CommitInfo MapCommit(Commit c) => new(
        c.Sha,
        c.Sha[..7],
        c.MessageShort,
        c.Message,
        c.Author.Name,
        c.Author.Email,
        c.Author.When,
        c.Committer.Name,
        c.Committer.When,
        c.Parents.Select(p => p.Sha).ToList());

    private static FileStatusEntry MapEntry(StatusEntry e, bool staged)
    {
        var state = MapState(e.State, staged);
        return new FileStatusEntry(e.FilePath, e.HeadToIndexRenameDetails?.OldFilePath, staged ? state : FileState.Unmodified, staged ? FileState.Unmodified : state);
    }

    private static FileState MapState(FileStatus s, bool staged) => s switch
    {
        var f when staged && f.HasFlag(FileStatus.NewInIndex) => FileState.Added,
        var f when staged && f.HasFlag(FileStatus.ModifiedInIndex) => FileState.Modified,
        var f when staged && f.HasFlag(FileStatus.DeletedFromIndex) => FileState.Deleted,
        var f when staged && f.HasFlag(FileStatus.RenamedInIndex) => FileState.Renamed,
        var f when !staged && f.HasFlag(FileStatus.ModifiedInWorkdir) => FileState.Modified,
        var f when !staged && f.HasFlag(FileStatus.DeletedFromWorkdir) => FileState.Deleted,
        var f when f.HasFlag(FileStatus.NewInWorkdir) => FileState.Untracked,
        var f when f.HasFlag(FileStatus.Conflicted) => FileState.Conflicted,
        _ => FileState.Unmodified
    };

    private static IReadOnlyList<FileDiff> MapPatch(Patch patch) =>
        patch.Select(p => new FileDiff(
            p.OldPath,
            p.Path,
            p.IsBinaryComparison,
            p.Status == ChangeKind.Added,
            p.Status == ChangeKind.Deleted,
            p.Status == ChangeKind.Renamed,
            p.LinesAdded,
            p.LinesDeleted,
            DiffParser.ParseHunks(p.Patch))).ToList();
}
