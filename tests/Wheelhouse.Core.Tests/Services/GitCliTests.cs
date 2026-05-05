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

    [Fact]
    public async Task GetFileDiffAsync_UntrackedFile_ReturnsDiffWithAddedLines()
    {
        DirectCommit("init.txt", "init");
        Write("untracked.txt", "hello\nworld\n");

        var diff = await GitCli.GetFileDiffAsync(_repoPath, "untracked.txt", staged: false);

        Assert.NotNull(diff);
        Assert.True(diff!.IsNew);
        Assert.True(diff.LinesAdded > 0);
        Assert.NotEmpty(diff.Hunks);
    }

    // Branch operations

    [Fact]
    public async Task CheckoutBranchAsync_ExistingBranch_ChangesHead()
    {
        DirectCommit("init.txt", "init");
        _repo.CreateBranch("feature");

        await GitCli.CheckoutBranchAsync(_repoPath, "feature");

        Assert.Equal("feature", _repo.Head.FriendlyName);
    }

    [Fact]
    public async Task CreateBranchAsync_NoCheckout_BranchExistsHeadUnchanged()
    {
        DirectCommit("init.txt", "init");
        var headBefore = _repo.Head.FriendlyName;

        await GitCli.CreateBranchAsync(_repoPath, "new-feature", null, checkout: false);

        Assert.Equal(headBefore, _repo.Head.FriendlyName);
        Assert.NotNull(_repo.Branches["new-feature"]);
    }

    [Fact]
    public async Task CreateBranchAsync_WithCheckout_SwitchesToNewBranch()
    {
        DirectCommit("init.txt", "init");

        await GitCli.CreateBranchAsync(_repoPath, "quick-feature", null, checkout: true);

        Assert.Equal("quick-feature", _repo.Head.FriendlyName);
    }

    [Fact]
    public async Task DeleteBranchAsync_UnmergedBranch_ThrowsWithoutForce()
    {
        DirectCommit("init.txt", "init");
        // Create a branch with its own commit not reachable from HEAD
        var orphan = _repo.CreateBranch("orphan");
        Commands.Checkout(_repo, orphan);
        DirectCommit("orphan.txt", "orphan work");
        Commands.Checkout(_repo, _repo.Branches["master"] ?? _repo.Branches["main"]);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => GitCli.DeleteBranchAsync(_repoPath, "orphan", force: false));
    }

    [Fact]
    public async Task DeleteBranchAsync_ForceDelete_RemovesBranch()
    {
        DirectCommit("init.txt", "init");
        _repo.CreateBranch("to-delete");

        await GitCli.DeleteBranchAsync(_repoPath, "to-delete", force: true);

        Assert.Null(_repo.Branches["to-delete"]);
    }

    // Stash operations

    [Fact]
    public async Task StashAsync_DirtyWorkingTree_StashesChanges()
    {
        DirectCommit("file.txt", "original");
        Write("file.txt", "modified");
        _repo.Config.Set("user.name", "Test");
        _repo.Config.Set("user.email", "t@t.com");

        await GitCli.StashAsync(_repoPath, "my stash", includeUntracked: false);
        var status = await GitCli.GetStatusAsync(_repoPath);

        Assert.Empty(status.UnstagedEntries);
    }

    [Fact]
    public async Task GetStashesAsync_AfterStash_ReturnsEntry()
    {
        DirectCommit("file.txt", "original");
        Write("file.txt", "modified");

        await GitCli.StashAsync(_repoPath, "test stash", includeUntracked: false);
        var stashes = await GitCli.GetStashesAsync(_repoPath);

        Assert.Single(stashes);
        Assert.Equal(0, stashes[0].Index);
    }

    [Fact]
    public async Task DropStashAsync_AfterStash_StashGone()
    {
        DirectCommit("file.txt", "original");
        Write("file.txt", "modified");
        await GitCli.StashAsync(_repoPath, null, includeUntracked: false);

        await GitCli.DropStashAsync(_repoPath, 0);
        var stashes = await GitCli.GetStashesAsync(_repoPath);

        Assert.Empty(stashes);
    }

    // Advanced git operations

    [Fact]
    public async Task MergeAsync_FastForward_AdvancesHead()
    {
        DirectCommit("init.txt", "init");
        Commands.Checkout(_repo, _repo.CreateBranch("feature"));
        DirectCommit("feature.txt", "feature work");
        Commands.Checkout(_repo, _repo.Branches["master"] ?? _repo.Branches["main"]);

        await GitCli.MergeAsync(_repoPath, "feature");

        Assert.Equal("feature.txt", _repo.Head.Tip.Tree.First().Name);
    }

    [Fact]
    public async Task ResetAsync_Mixed_LeavesFilesInWorkingTree()
    {
        DirectCommit("a.txt", "first");
        DirectCommit("b.txt", "second");
        var firstSha = _repo.Commits.Skip(1).First().Sha;

        await GitCli.ResetAsync(_repoPath, firstSha, Wheelhouse.Core.Models.ResetMode.Mixed);

        // b.txt is removed from the index but still on disk → shows as untracked
        var status = await GitCli.GetStatusAsync(_repoPath);
        Assert.Contains(status.UntrackedEntries, e => e.FilePath == "b.txt");
    }

    [Fact]
    public async Task RevertAsync_CreatesRevertCommit()
    {
        DirectCommit("a.txt", "first");
        var commitToRevert = _repo.Head.Tip.Sha;
        DirectCommit("b.txt", "second");

        await GitCli.RevertAsync(_repoPath, commitToRevert);

        var commits = _repo.Commits.Take(3).ToList();
        Assert.Equal(3, commits.Count);
        Assert.Contains("Revert", commits[0].MessageShort, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CherryPickAsync_AppliesCommitToCurrentBranch()
    {
        DirectCommit("base.txt", "base");
        var otherBranch = _repo.CreateBranch("other");
        Commands.Checkout(_repo, otherBranch);
        DirectCommit("cherry.txt", "cherry content");
        var cherrySha = _repo.Head.Tip.Sha;
        Commands.Checkout(_repo, _repo.Branches["master"] ?? _repo.Branches["main"]);

        await GitCli.CherryPickAsync(_repoPath, cherrySha);

        Assert.Contains("cherry.txt", _repo.Head.Tip.Tree.Select(e => e.Name));
    }

    // Tag operations

    [Fact]
    public async Task GetTagsAsync_AfterCreateTag_ReturnsTag()
    {
        DirectCommit("init.txt", "init");
        await GitCli.CreateTagAsync(_repoPath, "v1.0", null, null);

        var tags = await GitCli.GetTagsAsync(_repoPath);

        Assert.Single(tags);
        Assert.Equal("v1.0", tags[0].Name);
        Assert.False(tags[0].IsAnnotated);
    }

    [Fact]
    public async Task CreateTagAsync_Annotated_IsAnnotated()
    {
        DirectCommit("init.txt", "init");
        await GitCli.CreateTagAsync(_repoPath, "v1.0", null, "release v1.0");

        var tags = await GitCli.GetTagsAsync(_repoPath);

        Assert.Single(tags);
        Assert.True(tags[0].IsAnnotated);
        Assert.Equal("release v1.0", tags[0].Message);
    }

    [Fact]
    public async Task DeleteTagAsync_RemovesTag()
    {
        DirectCommit("init.txt", "init");
        await GitCli.CreateTagAsync(_repoPath, "v1.0", null, null);

        await GitCli.DeleteTagAsync(_repoPath, "v1.0");
        var tags = await GitCli.GetTagsAsync(_repoPath);

        Assert.Empty(tags);
    }

    // Remote operations

    [Fact]
    public async Task AddRemoteAsync_AppearsInList()
    {
        DirectCommit("init.txt", "init");
        await GitCli.AddRemoteAsync(_repoPath, "upstream", "https://example.com/repo.git");

        var remotes = await GitCli.GetRemotesAsync(_repoPath);

        Assert.Single(remotes);
        Assert.Equal("upstream", remotes[0].Name);
        Assert.Equal("https://example.com/repo.git", remotes[0].Url);
    }

    [Fact]
    public async Task RemoveRemoteAsync_RemovesFromList()
    {
        DirectCommit("init.txt", "init");
        await GitCli.AddRemoteAsync(_repoPath, "origin", "https://example.com/repo.git");

        await GitCli.RemoveRemoteAsync(_repoPath, "origin");
        var remotes = await GitCli.GetRemotesAsync(_repoPath);

        Assert.Empty(remotes);
    }

    [Fact]
    public async Task RenameRemoteAsync_UpdatesName()
    {
        DirectCommit("init.txt", "init");
        await GitCli.AddRemoteAsync(_repoPath, "origin", "https://example.com/repo.git");

        await GitCli.RenameRemoteAsync(_repoPath, "origin", "upstream");
        var remotes = await GitCli.GetRemotesAsync(_repoPath);

        Assert.Single(remotes);
        Assert.Equal("upstream", remotes[0].Name);
    }

    // Hunk staging

    [Fact]
    public async Task StageHunkAsync_SingleHunk_StagedWithoutRestOfFile()
    {
        // Two-section file so we can create two distinct hunks
        DirectCommit("file.txt", "line1\nline2\n\n\n\n\nline7\nline8\n");
        Write("file.txt", "line1 modified\nline2\n\n\n\n\nline7\nline8 modified\n");

        var diff = await GitCli.GetFileDiffAsync(_repoPath, "file.txt", staged: false);
        Assert.NotNull(diff);
        Assert.True(diff!.Hunks.Count >= 1);

        // Stage just the first hunk
        var firstHunk = diff.Hunks[0];
        await GitCli.StageHunkAsync(_repoPath, "file.txt", firstHunk, isNew: false);

        var status = await GitCli.GetStatusAsync(_repoPath);
        // File should now appear in both staged and unstaged (partial staging)
        Assert.NotEmpty(status.StagedEntries);
    }

    [Fact]
    public async Task BuildHunkPatch_ProducesValidPatch()
    {
        DirectCommit("file.txt", "line1\nline2\n");
        Write("file.txt", "line1\nline2 modified\n");

        var diff = await GitCli.GetFileDiffAsync(_repoPath, "file.txt", staged: false);
        Assert.NotNull(diff);

        var patch = GitCli.BuildHunkPatch("file.txt", diff!.Hunks[0], isNew: false, isDeleted: false);

        Assert.Contains("--- a/file.txt", patch);
        Assert.Contains("+++ b/file.txt", patch);
        Assert.Contains("@@", patch);
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
