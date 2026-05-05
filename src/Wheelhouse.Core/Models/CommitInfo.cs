namespace Wheelhouse.Core.Models;

public sealed record CommitInfo(
    string Sha,
    string ShortSha,
    string MessageShort,
    string Message,
    string AuthorName,
    string AuthorEmail,
    DateTimeOffset AuthorWhen,
    string CommitterName,
    DateTimeOffset CommitterWhen,
    IReadOnlyList<string> ParentShas);
