using System.Windows.Media;
using Wheelhouse.Core.Models;
using Wheelhouse.UI.Controls.CommitGraph;
using Wheelhouse.UI.ViewModels;
using Xunit;

namespace Wheelhouse.UI.Tests.ViewModels;

public class CommitItemViewModelTests
{
    private static readonly GraphRow DefaultRow = new(0, Colors.Blue, [], 1);

    private static CommitItemViewModel Make(DateTimeOffset when, string sha = "abc1234567890") =>
        new(new CommitInfo(
            sha, sha[..7],
            "Fix the bug",
            "Fix the bug\n\nDetails here",
            "Jane Doe", "jane@example.com",
            when, "Jane Doe", when,
            []), DefaultRow);

    // Properties

    [Fact]
    public void ShortSha_Returns7Characters()
    {
        var vm = Make(DateTimeOffset.Now);
        Assert.Equal(7, vm.ShortSha.Length);
    }

    [Fact]
    public void MessageShort_ReturnsSubject()
    {
        var vm = Make(DateTimeOffset.Now);
        Assert.Equal("Fix the bug", vm.MessageShort);
    }

    [Fact]
    public void AuthorName_ReturnsCorrectName()
    {
        var vm = Make(DateTimeOffset.Now);
        Assert.Equal("Jane Doe", vm.AuthorName);
    }

    // RelativeDate

    [Fact]
    public void RelativeDate_UnderOneMinute_ReturnsJustNow()
    {
        var vm = Make(DateTimeOffset.Now.AddSeconds(-30));
        Assert.Equal("just now", vm.RelativeDate);
    }

    [Fact]
    public void RelativeDate_ExactlyZeroSeconds_ReturnsJustNow()
    {
        var vm = Make(DateTimeOffset.Now);
        Assert.Equal("just now", vm.RelativeDate);
    }

    [Theory]
    [InlineData(60)]
    [InlineData(90)]
    [InlineData(119)]
    public void RelativeDate_OneToTwoMinutes_Returns1mAgo(int seconds)
    {
        var vm = Make(DateTimeOffset.Now.AddSeconds(-seconds));
        Assert.Equal("1m ago", vm.RelativeDate);
    }

    [Fact]
    public void RelativeDate_TwoHoursAgo_Returns2hAgo()
    {
        var vm = Make(DateTimeOffset.Now.AddHours(-2));
        Assert.Equal("2h ago", vm.RelativeDate);
    }

    [Fact]
    public void RelativeDate_ThreeDaysAgo_Returns3dAgo()
    {
        var vm = Make(DateTimeOffset.Now.AddDays(-3));
        Assert.Equal("3d ago", vm.RelativeDate);
    }

    [Fact]
    public void RelativeDate_TwoMonthsAgo_Returns2moAgo()
    {
        var vm = Make(DateTimeOffset.Now.AddDays(-61));
        Assert.Equal("2mo ago", vm.RelativeDate);
    }

    [Fact]
    public void RelativeDate_TwoYearsAgo_Returns2yAgo()
    {
        var vm = Make(DateTimeOffset.Now.AddDays(-730));
        Assert.Equal("2y ago", vm.RelativeDate);
    }
}
