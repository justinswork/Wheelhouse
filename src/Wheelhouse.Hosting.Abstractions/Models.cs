namespace Wheelhouse.Hosting.Abstractions;

public interface IRemoteRepository
{
    string Name { get; }
    string FullName { get; }
    string CloneUrl { get; }
    string? Description { get; }
    bool IsPrivate { get; }
    bool IsFork { get; }
    string DefaultBranch { get; }
}

public interface IPullRequest
{
    int Number { get; }
    string Title { get; }
    string? Body { get; }
    string State { get; }
    string AuthorLogin { get; }
    string HeadBranch { get; }
    string BaseBranch { get; }
    string HeadSha { get; }
    bool IsDraft { get; }
    DateTimeOffset CreatedAt { get; }
    DateTimeOffset? MergedAt { get; }
    string Url { get; }
}

public interface ICheckRun
{
    string Name { get; }
    string Status { get; }
    string? Conclusion { get; }
    string? DetailsUrl { get; }
}

public enum MergeMethod { Merge, Squash, Rebase }

public sealed record CreatePullRequestOptions(
    string RepoUrl,
    string Title,
    string? Body,
    string HeadBranch,
    string BaseBranch,
    bool Draft = false);
