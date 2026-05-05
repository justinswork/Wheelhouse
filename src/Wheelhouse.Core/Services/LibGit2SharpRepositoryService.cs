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

    public Task<WorkingTreeStatus> GetWorkingTreeStatusAsync(CancellationToken ct = default)
    {
        EnsureOpen();
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

        return Task.FromResult(new WorkingTreeStatus(staged, unstaged, conflicted, untracked));
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

    public Task<IReadOnlyList<FileDiff>> GetStagedDiffAsync(CancellationToken ct = default)
    {
        EnsureOpen();
        var diff = _repo!.Diff.Compare<Patch>(_repo.Head.Tip?.Tree, DiffTargets.Index);
        return Task.FromResult<IReadOnlyList<FileDiff>>(MapPatch(diff));
    }

    public Task<IReadOnlyList<FileDiff>> GetUnstagedDiffAsync(CancellationToken ct = default)
    {
        EnsureOpen();
        var diff = _repo!.Diff.Compare<Patch>(_repo.Head.Tip?.Tree, DiffTargets.WorkingDirectory);
        return Task.FromResult<IReadOnlyList<FileDiff>>(MapPatch(diff));
    }

    public Task<FileDiff?> GetFileDiffAsync(string filePath, bool staged, CancellationToken ct = default)
    {
        EnsureOpen();
        var diff = staged
            ? _repo!.Diff.Compare<Patch>(_repo.Head.Tip?.Tree, DiffTargets.Index)
            : _repo!.Diff.Compare<Patch>(_repo.Head.Tip?.Tree, DiffTargets.WorkingDirectory);

        var result = MapPatch(diff).FirstOrDefault(f => f.NewPath == filePath || f.OldPath == filePath);
        return Task.FromResult(result);
    }

    public Task StageAsync(IEnumerable<string> filePaths, CancellationToken ct = default)
    {
        EnsureOpen();
        Commands.Stage(_repo!, filePaths);
        return Task.CompletedTask;
    }

    public Task UnstageAsync(IEnumerable<string> filePaths, CancellationToken ct = default)
    {
        EnsureOpen();
        Commands.Unstage(_repo!, filePaths);
        return Task.CompletedTask;
    }

    public Task StageAllAsync(CancellationToken ct = default)
    {
        EnsureOpen();
        Commands.Stage(_repo!, "*");
        return Task.CompletedTask;
    }

    public Task UnstageAllAsync(CancellationToken ct = default)
    {
        EnsureOpen();
        Commands.Unstage(_repo!, "*");
        return Task.CompletedTask;
    }

    public Task CommitAsync(string message, bool amend = false, CancellationToken ct = default)
    {
        EnsureOpen();
        var signature = _repo!.Config.BuildSignature(DateTimeOffset.Now);
        if (amend)
            _repo.Commit(message, signature, signature, new CommitOptions { AmendPreviousCommit = true });
        else
            _repo.Commit(message, signature, signature);
        return Task.CompletedTask;
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
            ParseHunks(p.Patch))).ToList();

    private static IReadOnlyList<DiffHunk> ParseHunks(string rawPatch)
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
        var match = System.Text.RegularExpressions.Regex.Match(header, @"@@ -(\d+)(?:,\d+)? \+(\d+)");
        if (match.Success)
        {
            oldStart = int.Parse(match.Groups[1].Value);
            newStart = int.Parse(match.Groups[2].Value);
        }
    }
}
