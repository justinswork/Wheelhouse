using LibGit2Sharp;
using Microsoft.Extensions.Logging;
using Wheelhouse.Core.Models;

namespace Wheelhouse.Core.Services;

public sealed class LibGit2SharpRepositoryService : IRepositoryService
{
    private readonly ILogger<LibGit2SharpRepositoryService> _logger;
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
            return GetStatusViaLibGit2();
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
        var branches = _repo!.Branches
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
            .ToList();

        return Task.FromResult<IReadOnlyList<BranchInfo>>(branches);
    }

    public Task<IReadOnlyList<CommitInfo>> GetCommitLogAsync(int skip = 0, int take = 200, CancellationToken ct = default)
    {
        EnsureOpen();
        var commits = _repo!.Commits
            .QueryBy(new CommitFilter { SortBy = CommitSortStrategies.Topological | CommitSortStrategies.Time })
            .Skip(skip)
            .Take(take)
            .Select(MapCommit)
            .ToList();

        return Task.FromResult<IReadOnlyList<CommitInfo>>(commits);
    }

    public async Task<IReadOnlyList<FileDiff>> GetStagedDiffAsync(CancellationToken ct = default)
    {
        EnsureOpen();
        try
        {
            var diff = _repo!.Diff.Compare<Patch>(_repo.Head.Tip?.Tree, DiffTargets.Index);
            return MapPatch(diff);
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
        var diff = _repo!.Diff.Compare<Patch>(_repo.Head.Tip?.Tree, DiffTargets.WorkingDirectory);
        return Task.FromResult<IReadOnlyList<FileDiff>>(MapPatch(diff));
    }

    public async Task<FileDiff?> GetFileDiffAsync(string filePath, bool staged, CancellationToken ct = default)
    {
        EnsureOpen();
        try
        {
            var diff = staged
                ? _repo!.Diff.Compare<Patch>(_repo.Head.Tip?.Tree, DiffTargets.Index)
                : _repo!.Diff.Compare<Patch>(_repo.Head.Tip?.Tree, DiffTargets.WorkingDirectory);

            return MapPatch(diff).FirstOrDefault(f => f.NewPath == filePath || f.OldPath == filePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "libgit2 diff failed for {Path}, falling back to git.exe", filePath);
            return await GitCli.GetFileDiffAsync(CurrentRepository!.Path, filePath, staged, ct);
        }
    }

    public async Task StageAsync(IEnumerable<string> filePaths, CancellationToken ct = default)
    {
        EnsureOpen();
        var paths = filePaths.ToList();
        try
        {
            Commands.Stage(_repo!, paths);
        }
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
        try
        {
            Commands.Unstage(_repo!, paths);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "libgit2 unstage failed, falling back to git.exe");
            await GitCli.UnstageAsync(CurrentRepository!.Path, paths, ct);
        }
    }

    public async Task StageAllAsync(CancellationToken ct = default)
    {
        EnsureOpen();
        try
        {
            Commands.Stage(_repo!, "*");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "libgit2 stage-all failed, falling back to git.exe");
            await GitCli.StageAllAsync(CurrentRepository!.Path, ct);
        }
    }

    public async Task UnstageAllAsync(CancellationToken ct = default)
    {
        EnsureOpen();
        try
        {
            Commands.Unstage(_repo!, "*");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "libgit2 unstage-all failed, falling back to git.exe");
            await GitCli.UnstageAllAsync(CurrentRepository!.Path, ct);
        }
    }

    public async Task CommitAsync(string message, bool amend = false, CancellationToken ct = default)
    {
        EnsureOpen();
        try
        {
            var signature = _repo!.Config.BuildSignature(DateTimeOffset.Now);
            if (amend)
                _repo.Commit(message, signature, signature, new CommitOptions { AmendPreviousCommit = true });
            else
                _repo.Commit(message, signature, signature);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "libgit2 commit failed, falling back to git.exe");
            await GitCli.CommitAsync(CurrentRepository!.Path, message, amend, ct);
        }
    }

    public Task FetchAsync(string? remoteName = null, CancellationToken ct = default)
    {
        EnsureOpen();
        var remotes = remoteName is not null
            ? [_repo!.Network.Remotes[remoteName]]
            : _repo!.Network.Remotes.AsEnumerable();
        foreach (var remote in remotes)
            Commands.Fetch(_repo!, remote.Name, [], null, null);
        return Task.CompletedTask;
    }

    public Task PullAsync(CancellationToken ct = default)
    {
        EnsureOpen();
        var signature = _repo!.Config.BuildSignature(DateTimeOffset.Now);
        Commands.Pull(_repo!, signature, null);
        return Task.CompletedTask;
    }

    public Task PushAsync(string? remoteName = null, CancellationToken ct = default)
    {
        EnsureOpen();
        var remote = remoteName is not null
            ? _repo!.Network.Remotes[remoteName]
            : _repo!.Network.Remotes.FirstOrDefault();
        if (remote is null) return Task.CompletedTask;
        _repo!.Network.Push(_repo.Head, new PushOptions());
        return Task.CompletedTask;
    }

    public void Dispose() => Close();

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
