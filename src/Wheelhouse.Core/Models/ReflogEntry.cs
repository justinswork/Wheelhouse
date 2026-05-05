namespace Wheelhouse.Core.Models;

public sealed record ReflogEntry(string Sha, string ShortSha, string RefName, string Message, DateTimeOffset When);
