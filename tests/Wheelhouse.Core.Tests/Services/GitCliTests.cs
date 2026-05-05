using LibGit2Sharp;
using Wheelhouse.Core.Models;
using Wheelhouse.Core.Services;
using Xunit;

namespace Wheelhouse.Core.Tests.Services;

public sealed class GitCliTests : IDisposable
{
    private readonly string _repoPath;
    private readonly Repository _repo;
    private readonly Signature _sig = new("Test User", "test@example.com", DateTimeOffset.Now);

    public GitCliTests()
    {
        _repoPath = Path.Combine(Path.GetTempPath(), "wh-cli-tests-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_repoPath);
        Repository.Init(_repoPath);
        _repo = new Repository(_repoPath);
        _repo.Config.Set("user.name", "Test User");
        _repo.Config.Set("user.email", "test@example.com");
    }

    public void Dispose()
    {
        _repo.Dispose();
        try { Directory.Delete(_repoPath, recursive: true); } catch { }
    }

    // GetStatusAsync

    [Fact]
    public async Task GetStatus_CleanRepo_AllEmpty()
    {
        DirectCommit("init.txt", "init");

        var status = await GitCli.GetStatusAsync(_repoPath);

        Assert.Empty(status.StagedEntries);
        Assert.Empty(status.UnstagedEntries);
        Assert.Empty(status.UntrackedEntries);
    }

    [Fact]
    public async Task GetStatus_UntrackedFile_AppearsInUntracked()
    {
        DirectCommit("init.txt", "init");
        Write("new.txt");

        var status = await GitCli.GetStatusAsync(_repoPath);

        Assert.Single(status.UntrackedEntries);
        Assert.Equal("new.txt", status.UntrackedEntries[0].FilePath);
        Assert.Equal(FileState.Untracked, status.UntrackedEntries[0].WorkingTreeState);
    }

    [Fact]
    public async Task GetStatus_StagedNewFile_AppearsInStagedAsAdded()
    {
        DirectCommit("init.txt", "init");
        Write("staged.txt");
        Commands.Stage(_repo, "staged.txt");

        var status = await GitCli.GetStatusAsync(_repoPath);

        Assert.Single(status.StagedEntries);
        Assert.Equal("staged.txt", status.StagedEntries[0].FilePath);
        Assert.Equal(FileState.Added, status.StagedEntries[0].IndexState);
    }

    [Fact]
    public async Task GetStatus_ModifiedFile_AppearsInUnstaged()
    {
        DirectCommit("file.txt", "original");
        Write("file.txt", "modified");

        var status = await GitCli.GetStatusAsync(_repoPath);

        Assert.Single(status.UnstagedEntries);
        Assert.Equal(FileState.Modified, status.UnstagedEntries[0].WorkingTreeState);
    }

    [Fact]
    public async Task GetStatus_DeletedFile_AppearsInUnstaged()
    {
        DirectCommit("file.txt", "content");
        File.Delete(Path.Combine(_repoPath, "file.txt"));

        var status = await GitCli.GetStatusAsync(_repoPath);

        Assert.Single(status.UnstagedEntries);
        Assert.Equal(FileState.Deleted, status.UnstagedEntries[0].WorkingTreeState);
    }

    [Fact]
    public async Task GetStatus_StagedModification_AppearsInStaged()
    {
        DirectCommit("file.txt", "original");
        Write("file.txt", "modified");
        Commands.Stage(_repo, "file.txt");

        var status = await GitCli.GetStatusAsync(_repoPath);

        Assert.Single(status.StagedEntries);
        Assert.Equal(FileState.Modified, status.StagedEntries[0].IndexState);
        Assert.Empty(status.UnstagedEntries);
    }

    // StageAsync / UnstageAsync

    [Fact]
    public async Task StageAsync_UntrackedFile_AppearsInStaged()
    {
        DirectCommit("init.txt", "init");
        Write("new.txt");

        await GitCli.StageAsync(_repoPath, ["new.txt"]);
        var status = await GitCli.GetStatusAsync(_repoPath);

        Assert.Single(status.StagedEntries);
        Assert.Empty(status.UntrackedEntries);
    }

    [Fact]
    public async Task UnstageAsync_StagedFile_MovesToUntracked()
    {
        DirectCommit("init.txt", "init");
        Write("staged.txt");
        Commands.Stage(_repo, "staged.txt");

        await GitCli.UnstageAsync(_repoPath, ["staged.txt"]);
        var status = await GitCli.GetStatusAsync(_repoPath);

        Assert.Empty(status.StagedEntries);
        Assert.Single(status.UntrackedEntries);
    }

    [Fact]
    public async Task StageAllAsync_MultipleFiles_AllStaged()
    {
        DirectCommit("init.txt", "init");
        Write("a.txt");
        Write("b.txt");

        await GitCli.StageAllAsync(_repoPath);
        var status = await GitCli.GetStatusAsync(_repoPath);

        Assert.Equal(2, status.StagedEntries.Count);
        Assert.Empty(status.UntrackedEntries);
    }

    // CommitAsync

    [Fact]
    public async Task CommitAsync_StagedFile_CreatesCommit()
    {
        DirectCommit("init.txt", "init");
        Write("feature.txt");
        Commands.Stage(_repo, "feature.txt");

        await GitCli.CommitAsync(_repoPath, "add feature", amend: false);

        // Verify via libgit2 that commit exists
        var commits = _repo.Commits.QueryBy(new CommitFilter()).Take(2).ToList();
        Assert.Equal(2, commits.Count);
        Assert.Equal("add feature", commits[0].MessageShort);
    }

    // GetFileDiffAsync

    [Fact]
    public async Task GetFileDiffAsync_ModifiedFile_ReturnsDiff()
    {
        DirectCommit("file.txt", "line1\nline2\n");
        Write("file.txt", "line1\nline2 modified\n");

        var diff = await GitCli.GetFileDiffAsync(_repoPath, "file.txt", staged: false);

        Assert.NotNull(diff);
        Assert.False(diff!.IsBinary);
        Assert.NotEmpty(diff.Hunks);
    }

    [Fact]
    public async Task GetFileDiffAsync_NoChanges_ReturnsNull()
    {
        DirectCommit("file.txt", "content");

        var diff = await GitCli.GetFileDiffAsync(_repoPath, "file.txt", staged: false);

        Assert.Null(diff);
    }

    // helpers

    private void Write(string name, string content = "content\n") =>
        File.WriteAllText(Path.Combine(_repoPath, name), content);

    private void DirectCommit(string fileName, string content)
    {
        Write(fileName, content);
        Commands.Stage(_repo, fileName);
        _repo.Commit($"add {fileName}", _sig, _sig);
    }
}
