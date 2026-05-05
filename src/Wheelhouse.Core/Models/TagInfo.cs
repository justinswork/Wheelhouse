namespace Wheelhouse.Core.Models;

public sealed record TagInfo(string Name, string Sha, bool IsAnnotated, DateTimeOffset? When, string? Message);
