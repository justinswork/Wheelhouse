using System.Windows.Media;
using Wheelhouse.Core.Models;
using Wheelhouse.UI.Controls.CommitGraph;
using Xunit;

namespace Wheelhouse.UI.Tests.Controls;

public class GraphLaneCalculatorTests
{
    private static CommitInfo Make(string sha, params string[] parents) =>
        new(sha, sha[..Math.Min(7, sha.Length)], sha, sha, "author", "a@b.com",
            DateTimeOffset.Now, "committer", DateTimeOffset.Now,
            parents.ToList());

    [Fact]
    public void Calculate_EmptyInput_ReturnsEmpty()
    {
        var result = GraphLaneCalculator.Calculate([]);

        Assert.Empty(result);
    }

    [Fact]
    public void Calculate_SingleCommitNoParents_LaneZeroNoConnections()
    {
        var commits = new[] { Make("aaa1111") };

        var result = GraphLaneCalculator.Calculate(commits);

        Assert.Single(result);
        Assert.Equal(0, result[0].Lane);
        Assert.Empty(result[0].Connections);
    }

    [Fact]
    public void Calculate_LinearHistory_AllInLaneZero()
    {
        var commits = new[]
        {
            Make("ccc1111", "bbb1111"),
            Make("bbb1111", "aaa1111"),
            Make("aaa1111"),
        };

        var result = GraphLaneCalculator.Calculate(commits);

        Assert.Equal(3, result.Count);
        Assert.All(result, r => Assert.Equal(0, r.Lane));
    }

    [Fact]
    public void Calculate_LinearHistory_EachHasStartConnection()
    {
        var commits = new[]
        {
            Make("ccc1111", "bbb1111"),
            Make("bbb1111", "aaa1111"),
            Make("aaa1111"),
        };

        var result = GraphLaneCalculator.Calculate(commits);

        // All commits with parents have a Start connection
        Assert.Contains(result[0].Connections, c => c.Type == ConnectionType.Start);
        Assert.Contains(result[1].Connections, c => c.Type == ConnectionType.Start);
        // Root commit has no Start
        Assert.DoesNotContain(result[2].Connections, c => c.Type == ConnectionType.Start);
    }

    [Fact]
    public void Calculate_MergeCommit_HasForkConnection()
    {
        // merge has two parents: main and branch
        var commits = new[]
        {
            Make("merge111", "main1111", "branch11"),
            Make("branch11"),
            Make("main1111"),
        };

        var result = GraphLaneCalculator.Calculate(commits);

        // The merge commit row should have a Fork connection for the second parent
        Assert.Contains(result[0].Connections, c => c.Type == ConnectionType.Fork);
    }

    [Fact]
    public void Calculate_MergeCommit_SecondParentGetsNewLane()
    {
        var commits = new[]
        {
            Make("merge111", "main1111", "branch11"),
            Make("branch11"),
            Make("main1111"),
        };

        var result = GraphLaneCalculator.Calculate(commits);

        // First parent (main) continues lane 0; second parent (branch) goes to lane 1
        Assert.Equal(0, result[0].Lane);
        Assert.NotEqual(result[1].Lane, result[0].Lane);
    }

    [Fact]
    public void Calculate_TotalLanes_ReflectsActiveParallelBranches()
    {
        var commits = new[]
        {
            Make("merge111", "main1111", "branch11"),
            Make("branch11"),
            Make("main1111"),
        };

        var result = GraphLaneCalculator.Calculate(commits);

        // After the merge commit introduces a fork, there should be 2 lanes active
        Assert.Equal(2, result[0].TotalLanes);
    }

    [Fact]
    public void Calculate_ResultCountMatchesInputCount()
    {
        var commits = Enumerable.Range(0, 10)
            .Select(i => Make($"sha{i:D4}111"))
            .ToArray();

        var result = GraphLaneCalculator.Calculate(commits);

        Assert.Equal(commits.Length, result.Count);
    }

    [Fact]
    public void Calculate_AllRowsHaveNonDefaultColor()
    {
        var commits = new[] { Make("aaa1111"), Make("bbb1111"), Make("ccc1111") };

        var result = GraphLaneCalculator.Calculate(commits);

        Assert.All(result, r => Assert.NotEqual(default(Color), r.Color));
    }
}
