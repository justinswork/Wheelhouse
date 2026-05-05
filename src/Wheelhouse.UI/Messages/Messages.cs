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
