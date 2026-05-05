namespace Wheelhouse.Core.Models;

public sealed record RepositoryInfo(
    string Name,
    string Path,
    string? RemoteUrl,
    bool IsBare);
