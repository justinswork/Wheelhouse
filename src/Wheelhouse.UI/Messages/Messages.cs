using CommunityToolkit.Mvvm.Messaging.Messages;
using Wheelhouse.Core.Models;

namespace Wheelhouse.UI.Messages;

public sealed class RepositoryOpenedMessage : ValueChangedMessage<RepositoryInfo>
{
    public RepositoryOpenedMessage(RepositoryInfo repo) : base(repo) { }
}

public sealed class RepositoryClosedMessage { }

public sealed class WorkingTreeChangedMessage { }

public sealed class BranchChangedMessage { }

public sealed class StashChangedMessage { }

public sealed class TagChangedMessage { }

public sealed class RemoteChangedMessage { }

public sealed class OpenReflogMessage { }

public sealed class OpenFileHistoryMessage
{
    public string FilePath { get; }
    public OpenFileHistoryMessage(string filePath) => FilePath = filePath;
}

public sealed class OpenBlameMessage
{
    public string FilePath { get; }
    public OpenBlameMessage(string filePath) => FilePath = filePath;
}

public sealed class CommitFileSelectedMessage
{
    public string CommitSha { get; }
    public string FilePath { get; }
    public CommitFileSelectedMessage(string commitSha, string filePath) { CommitSha = commitSha; FilePath = filePath; }
}

public sealed class NavigateToCommitMessage
{
    public string Sha { get; }
    public NavigateToCommitMessage(string sha) => Sha = sha;
}

public sealed class FileSelectedForDiffMessage
{
    public string FilePath { get; }
    public bool IsStaged { get; }
    public FileSelectedForDiffMessage(string filePath, bool isStaged) { FilePath = filePath; IsStaged = isStaged; }
}

public sealed class CommitSelectedMessage
{
    public CommitInfo Commit { get; }
    public CommitSelectedMessage(CommitInfo commit) { Commit = commit; }
}

public sealed class OpenPullRequestsMessage { }

public sealed class PullRequestsChangedMessage { }

public sealed class OpenIndexEditorMessage
{
    public string FilePath { get; }
    public OpenIndexEditorMessage(string filePath) => FilePath = filePath;
}

public sealed class UpdateAvailableMessage
{
    public string Version { get; }
    public string ReleaseNotes { get; }
    public string? DownloadUrl { get; }

    public UpdateAvailableMessage(string version, string releaseNotes, string? downloadUrl)
    {
        Version = version;
        ReleaseNotes = releaseNotes;
        DownloadUrl = downloadUrl;
    }
}
