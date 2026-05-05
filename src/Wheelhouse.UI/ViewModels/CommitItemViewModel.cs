using Wheelhouse.Core.Models;
using Wheelhouse.UI.Controls.CommitGraph;

namespace Wheelhouse.UI.ViewModels;

public sealed class CommitItemViewModel : ViewModelBase
{
    public CommitInfo Commit { get; }
    public GraphRow GraphRow { get; set; }

    public string ShortSha => Commit.ShortSha;
    public string MessageShort => Commit.MessageShort;
    public string AuthorName => Commit.AuthorName;
    public string RelativeDate => FormatRelativeDate(Commit.AuthorWhen);

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
            < 60    => "just now",
            < 3600  => $"{(int)delta.TotalMinutes}m ago",
            < 86400 => $"{(int)delta.TotalHours}h ago",
            < 2592000 => $"{(int)delta.TotalDays}d ago",
            < 31536000 => $"{(int)(delta.TotalDays / 30)}mo ago",
            _ => $"{(int)(delta.TotalDays / 365)}y ago"
        };
    }
}
