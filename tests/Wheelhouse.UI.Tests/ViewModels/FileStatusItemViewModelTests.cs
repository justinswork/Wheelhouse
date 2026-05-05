using Wheelhouse.Core.Models;
using Wheelhouse.UI.ViewModels;
using Xunit;

namespace Wheelhouse.UI.Tests.ViewModels;

public class FileStatusItemViewModelTests
{
    private static FileStatusItemViewModel Staged(FileState state) =>
        new(new FileStatusEntry("path/to/file.cs", null, state, FileState.Unmodified), isStaged: true);

    private static FileStatusItemViewModel Unstaged(FileState state) =>
        new(new FileStatusEntry("path/to/file.cs", null, FileState.Unmodified, state), isStaged: false);

    // StateLabel

    [Theory]
    [InlineData(FileState.Added, "A")]
    [InlineData(FileState.Modified, "M")]
    [InlineData(FileState.Deleted, "D")]
    [InlineData(FileState.Renamed, "R")]
    [InlineData(FileState.Untracked, "?")]
    [InlineData(FileState.Conflicted, "!")]
    public void StateLabel_StagedFile_ReturnsExpectedLabel(FileState state, string expected)
    {
        var vm = Staged(state);
        Assert.Equal(expected, vm.StateLabel);
    }

    [Theory]
    [InlineData(FileState.Modified, "M")]
    [InlineData(FileState.Deleted, "D")]
    [InlineData(FileState.Untracked, "?")]
    public void StateLabel_UnstagedFile_ReturnsExpectedLabel(FileState state, string expected)
    {
        var vm = Unstaged(state);
        Assert.Equal(expected, vm.StateLabel);
    }

    [Fact]
    public void StateLabel_UnknownState_ReturnsSpace()
    {
        var vm = Staged(FileState.Unmodified);
        Assert.Equal(" ", vm.StateLabel);
    }

    // State routing

    [Fact]
    public void State_WhenIsStaged_UsesIndexState()
    {
        var entry = new FileStatusEntry("f.cs", null, FileState.Added, FileState.Unmodified);
        var vm = new FileStatusItemViewModel(entry, isStaged: true);

        Assert.Equal(FileState.Added, vm.State);
    }

    [Fact]
    public void State_WhenIsUnstaged_UsesWorkingTreeState()
    {
        var entry = new FileStatusEntry("f.cs", null, FileState.Unmodified, FileState.Modified);
        var vm = new FileStatusItemViewModel(entry, isStaged: false);

        Assert.Equal(FileState.Modified, vm.State);
    }

    // Path helpers

    [Fact]
    public void FileName_ExtractsFileNameFromPath()
    {
        var entry = new FileStatusEntry("src/foo/Bar.cs", null, FileState.Modified, FileState.Unmodified);
        var vm = new FileStatusItemViewModel(entry, isStaged: true);

        Assert.Equal("Bar.cs", vm.FileName);
    }

    [Fact]
    public void Directory_ExtractsDirectoryFromPath()
    {
        var entry = new FileStatusEntry("src/foo/Bar.cs", null, FileState.Modified, FileState.Unmodified);
        var vm = new FileStatusItemViewModel(entry, isStaged: true);

        Assert.Equal("src\\foo", vm.Directory);
    }

    [Fact]
    public void FilePath_ReturnsFull()
    {
        var entry = new FileStatusEntry("src/foo/Bar.cs", null, FileState.Added, FileState.Unmodified);
        var vm = new FileStatusItemViewModel(entry, isStaged: true);

        Assert.Equal("src/foo/Bar.cs", vm.FilePath);
    }

    // StateColor

    [Theory]
    [InlineData(FileState.Added, "BrushAdded")]
    [InlineData(FileState.Modified, "BrushModified")]
    [InlineData(FileState.Deleted, "BrushRemoved")]
    [InlineData(FileState.Renamed, "BrushModified")]
    [InlineData(FileState.Untracked, "BrushUntracked")]
    [InlineData(FileState.Conflicted, "BrushConflicted")]
    public void StateColor_ReturnsCorrectResourceKey(FileState state, string expectedKey)
    {
        var vm = Staged(state);
        Assert.Equal(expectedKey, vm.StateColor);
    }
}
