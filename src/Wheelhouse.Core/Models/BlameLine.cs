namespace Wheelhouse.Core.Models;

public sealed record BlameLine(string CommitSha, string ShortSha, string AuthorName, DateTimeOffset When, int LineNumber, string Content);
