using CommunityToolkit.Mvvm.ComponentModel;
using Wheelhouse.Core.Models;
using Wheelhouse.UI.Controls.CommitGraph;
using Wheelhouse.UI.Properties;

namespace Wheelhouse.UI.ViewModels;

public sealed partial class CommitItemViewModel : ViewModelBase
{
    public CommitInfo Commit { get; }
    public GraphRow GraphRow { get; set; }

    public string ShortSha => Commit.ShortSha;
    public string MessageShort => Commit.MessageShort;
    public string AuthorName => Commit.AuthorName;
    public string RelativeDate => FormatRelativeDate(Commit.AuthorWhen);

    [ObservableProperty] private string _ciStatus = "";

    public CommitItemViewModel(CommitInfo commit, GraphRow graphRow)
    {
        Commit = commit;
        GraphRow = graphRow;
    }

    private static string FormatRelativeDate(DateTimeOffset date)
    {
        var delta = DateTimeOffset.Now - date;
        return delta.TotalSeconds switch
        {
            < 60    => Strings.Date_JustNow,
            < 3600  => string.Format(Strings.Date_MinutesAgo, (int)delta.TotalMinutes),
            < 86400 => string.Format(Strings.Date_HoursAgo, (int)delta.TotalHours),
            < 2592000 => string.Format(Strings.Date_DaysAgo, (int)delta.TotalDays),
            < 31536000 => string.Format(Strings.Date_MonthsAgo, (int)(delta.TotalDays / 30)),
            _ => string.Format(Strings.Date_YearsAgo, (int)(delta.TotalDays / 365))
        };
    }
}
