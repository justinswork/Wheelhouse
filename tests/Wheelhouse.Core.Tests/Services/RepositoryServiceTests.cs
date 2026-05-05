using LibGit2Sharp;
using Microsoft.Extensions.Logging.Abstractions;
using Wheelhouse.Core.Models;
using Wheelhouse.Core.Services;
using Xunit;

namespace Wheelhouse.Core.Tests.Services;

public sealed class RepositoryServiceTests : IDisposable
{
    private readonly string _repoPath;
    private readonly Repository _repo;
    private readonly LibGit2SharpRepositoryService _sut;
    private readonly Signature _sig = new("Test User", "test@example.com", DateTimeOffset.Now);

    public RepositoryServiceTests()
    {
        _repoPath = Path.Combine(Path.GetTempPath(), "wh-tests-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_repoPath);
        Repository.Init(_repoPath);
        _repo = new Repository(_repoPath);
        _repo.Config.Set("user.name", "Test User");
        _repo.Config.Set("user.email", "test@example.com");

        _sut = new LibGit2SharpRepositoryService(NullLogger<LibGit2SharpRepositoryService>.Instance);
        _sut.Open(_repoPath);
    }

    public void Dispose()
    {
        _sut.Dispose();
        _repo.Dispose();
        try { Directory.Delete(_repoPath, recursive: true); } catch { }
    }

    // GetWorkingTreeStatusAsync

    [Fact]
    public async Task GetWorkingTreeStatus_CleanRepo_AllListsEmpty()
    {
        DirectCommit("init.txt", "init");

        var status = await _sut.GetWorkingTreeStatusAsync();

        Assert.Empty(status.StagedEntries);
        Assert.Empty(status.UnstagedEntries);
        Assert.Empty(status.UntrackedEntries);
        Assert.Empty(status.ConflictedEntries);
    }

    [Fact]
    public async Task GetWorkingTreeStatus_NewUntrackedFile_AppearsInUntracked()
    {
        DirectCommit("init.txt", "init");
        WriteFile("new.txt");

        var status = await _sut.GetWorkingTreeStatusAsync();

        Assert.Single(status.UntrackedEntries);
        Assert.Equal("new.txt", status.UntrackedEntries[0].FilePath);
        Assert.Equal(FileState.Untracked, status.UntrackedEntries[0].WorkingTreeState);
        Assert.Empty(status.StagedEntries);
    }

    [Fact]
    public async Task GetWorkingTreeStatus_StagedNewFile_AppearsInStagedAsAdded()
    {
        DirectCommit("init.txt", "init");
        WriteFile("staged.txt");
        Commands.Stage(_repo, "staged.txt");

        var status = await _sut.GetWorkingTreeStatusAsync();

        Assert.Single(status.StagedEntries);
        Assert.Equal("staged.txt", status.StagedEntries[0].FilePath);
        Assert.Equal(FileState.Added, status.StagedEntries[0].IndexState);
        Assert.Empty(status.UntrackedEntries);
    }

    [Fact]
    public async Task GetWorkingTreeStatus_ModifiedCommittedFile_AppearsInUnstaged()
    {
        DirectCommit("file.txt", "original content");
        WriteFile("file.txt", "modified content");

        var status = await _sut.GetWorkingTreeStatusAsync();

        Assert.Single(status.UnstagedEntries);
        Assert.Equal("file.txt", status.UnstagedEntries[0].FilePath);
        Assert.Equal(FileState.Modified, status.UnstagedEntries[0].WorkingTreeState);
        Assert.Empty(status.StagedEntries);
    }

    [Fact]
    public async Task GetWorkingTreeStatus_DeletedCommittedFile_AppearsInUnstaged()
    {
        DirectCommit("file.txt", "content");
        File.Delete(Path.Combine(_repoPath, "file.txt"));

        var status = await _sut.GetWorkingTreeStatusAsync();

        Assert.Single(status.UnstagedEntries);
        Assert.Equal(FileState.Deleted, status.UnstagedEntries[0].WorkingTreeState);
    }

    // StageAsync / UnstageAsync

    [Fact]
    public async Task StageAsync_UntrackedFile_MovesToStagedEntries()
    {
        DirectCommit("init.txt", "init");
        WriteFile("new.txt");

        await _sut.StageAsync(["new.txt"]);
        var status = await _sut.GetWorkingTreeStatusAsync();

        Assert.Single(status.StagedEntries);
        Assert.Empty(status.UntrackedEntries);
    }

    [Fact]
    public async Task UnstageAsync_StagedFile_MovesToUntracked()
    {
        DirectCommit("init.txt", "init");
        WriteFile("staged.txt");
        Commands.Stage(_repo, "staged.txt");

        await _sut.UnstageAsync(["staged.txt"]);
        var status = await _sut.GetWorkingTreeStatusAsync();

        Assert.Empty(status.StagedEntries);
        Assert.Single(status.UntrackedEntries);
    }

    [Fact]
    public async Task StageAllAsync_MultipleUntrackedFiles_AllMoveToStaged()
    {
        DirectCommit("init.txt", "init");
        WriteFile("a.txt");
        WriteFile("b.txt");
        WriteFile("c.txt");

        await _sut.StageAllAsync();
        var status = await _sut.GetWorkingTreeStatusAsync();

        Assert.Equal(3, status.StagedEntries.Count);
        Assert.Empty(status.UntrackedEntries);
    }

    // CommitAsync

    [Fact]
    public async Task CommitAsync_StagedFiles_CreatesCommitInLog()
    {
        DirectCommit("init.txt", "init");
        WriteFile("feature.txt");
        await _sut.StageAsync(["feature.txt"]);

        await _sut.CommitAsync("add feature");

        var log = await _sut.GetCommitLogAsync(0, 10);
        Assert.Equal(2, log.Count);
        Assert.Equal("add feature", log[0].MessageShort);
    }

    [Fact]
    public async Task CommitAsync_AfterCommit_WorkingTreeIsClean()
    {
        DirectCommit("init.txt", "init");
        WriteFile("feature.txt");
        await _sut.StageAsync(["feature.txt"]);
        await _sut.CommitAsync("add feature");

        var status = await _sut.GetWorkingTreeStatusAsync();

        Assert.Empty(status.StagedEntries);
        Assert.Empty(status.UnstagedEntries);
        Assert.Empty(status.UntrackedEntries);
    }

    // GetCommitLogAsync

    [Fact]
    public async Task GetCommitLogAsync_EmptyRepo_ReturnsEmpty()
    {
        var log = await _sut.GetCommitLogAsync();

        Assert.Empty(log);
    }

    [Fact]
    public async Task GetCommitLogAsync_MultipleCommits_ReturnsNewestFirst()
    {
        DirectCommit("a.txt", "a");
        DirectCommit("b.txt", "b");
        DirectCommit("c.txt", "c");

        var log = await _sut.GetCommitLogAsync(0, 10);

        Assert.Equal(3, log.Count);
        Assert.Equal("add c.txt", log[0].MessageShort);
        Assert.Equal("add a.txt", log[2].MessageShort);
    }

    [Fact]
    public async Task GetCommitLogAsync_Pagination_SkipsCorrectly()
    {
        DirectCommit("a.txt", "a");
        DirectCommit("b.txt", "b");
        DirectCommit("c.txt", "c");

        var page2 = await _sut.GetCommitLogAsync(skip: 1, take: 1);

        Assert.Single(page2);
        Assert.Equal("add b.txt", page2[0].MessageShort);
    }

    [Fact]
    public async Task GetCommitLogAsync_CommitHasCorrectMetadata()
    {
        DirectCommit("a.txt", "a");

        var log = await _sut.GetCommitLogAsync(0, 1);

        Assert.Single(log);
        Assert.Equal(7, log[0].ShortSha.Length);
        Assert.Equal("Test User", log[0].AuthorName);
        Assert.Equal("test@example.com", log[0].AuthorEmail);
    }

    // GetBranchesAsync

    [Fact]
    public async Task GetBranchesAsync_AfterFirstCommit_ReturnsSingleCurrentBranch()
    {
        DirectCommit("init.txt", "init");

        var branches = await _sut.GetBranchesAsync();

        var local = branches.Where(b => !b.IsRemote).ToList();
        Assert.Single(local);
        Assert.True(local[0].IsCurrentRepositoryHead);
    }

    // GetFileDiffAsync

    [Fact]
    public async Task GetFileDiffAsync_UnstagedModification_ReturnsDiff()
    {
        DirectCommit("file.txt", "line1\nline2\n");
        WriteFile("file.txt", "line1\nline2 modified\n");

        var diff = await _sut.GetFileDiffAsync("file.txt", staged: false);

        Assert.NotNull(diff);
        Assert.False(diff!.IsBinary);
        Assert.NotEmpty(diff.Hunks);
    }

    [Fact]
    public async Task GetFileDiffAsync_StagedNewFile_ReturnsIsNewDiff()
    {
        DirectCommit("init.txt", "init");
        WriteFile("new.txt", "hello\n");
        Commands.Stage(_repo, "new.txt");

        var diff = await _sut.GetFileDiffAsync("new.txt", staged: true);

        Assert.NotNull(diff);
        Assert.True(diff!.IsNew);
    }

    [Fact]
    public async Task GetFileDiffAsync_NonexistentFile_ReturnsNull()
    {
        DirectCommit("init.txt", "init");

        var diff = await _sut.GetFileDiffAsync("doesnotexist.txt", staged: false);

        Assert.Null(diff);
    }

    // helpers

    private void WriteFile(string name, string content = "content\n") =>
        File.WriteAllText(Path.Combine(_repoPath, name), content);

    private void DirectCommit(string fileName, string content)
    {
        WriteFile(fileName, content);
        Commands.Stage(_repo, fileName);
        _repo.Commit($"add {fileName}", _sig, _sig);
    }
}
