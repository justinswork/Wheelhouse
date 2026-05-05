namespace Wheelhouse.Core.Models;

public sealed record StashInfo(
    int Index,
    string Message,
    string CommitSha,
    DateTimeOffset When);
