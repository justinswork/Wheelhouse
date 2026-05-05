using System.Diagnostics;
using System.Text;
using Wheelhouse.Core.Models;

namespace Wheelhouse.Core.Services;

internal static class GitCli
{
    internal static async Task<string> RunAsync(string repoPath, string args, CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo("git", args)
        {
            WorkingDirectory = repoPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
        };

        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start git process.");

        var stdoutTask = proc.StandardOutput.ReadToEndAsync(ct);
        await proc.WaitForExitAsync(ct);
        var stdout = await stdoutTask;

        if (proc.ExitCode != 0)
        {
            var stderr = await proc.StandardError.ReadToEndAsync(ct);
            throw new InvalidOperationException($"git {args}: {stderr.Trim()}");
        }

        return stdout;
    }

    internal static async Task<WorkingTreeStatus> GetStatusAsync(string repoPath, CancellationToken ct = default)
    {
        var output = await RunAsync(repoPath, "status --porcelain=v1", ct);

        var staged = new List<FileStatusEntry>();
        var unstaged = new List<FileStatusEntry>();
        var untracked = new List<FileStatusEntry>();
        var conflicted = new List<FileStatusEntry>();

        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length < 3) continue;

            char x = line[0];
            char y = line[1];
            var path = line[3..];

            // Renames show as "new_name -> old_name"; take the new name as path
            string? origPath = null;
            var arrowIdx = path.IndexOf(" -> ", StringComparison.Ordinal);
            if (arrowIdx >= 0)
            {
                origPath = path[(arrowIdx + 4)..];
                path = path[..arrowIdx];
            }

            if (x == '?' && y == '?') { untracked.Add(Entry(path, null, FileState.Unmodified, FileState.Untracked)); continue; }
            if (x == '!' && y == '!') continue;

            if (x == 'U' || y == 'U' || (x == 'A' && y == 'A') || (x == 'D' && y == 'D'))
            { conflicted.Add(Entry(path, null, FileState.Conflicted, FileState.Conflicted)); continue; }

            if (x != ' ') staged.Add(Entry(path, origPath, MapState(x), FileState.Unmodified));
            if (y != ' ' && y != '?') unstaged.Add(Entry(path, null, FileState.Unmodified, MapState(y)));
        }

        return new WorkingTreeStatus(staged, unstaged, conflicted, untracked);
    }

    internal static async Task StageAsync(string repoPath, IEnumerable<string> paths, CancellationToken ct = default)
    {
        var quoted = string.Join(" ", paths.Select(p => $"\"{p}\""));
        await RunAsync(repoPath, $"add -- {quoted}", ct);
    }

    internal static Task StageAllAsync(string repoPath, CancellationToken ct = default) =>
        RunAsync(repoPath, "add -A", ct);

    internal static async Task UnstageAsync(string repoPath, IEnumerable<string> paths, CancellationToken ct = default)
    {
        var quoted = string.Join(" ", paths.Select(p => $"\"{p}\""));
        await RunAsync(repoPath, $"restore --staged -- {quoted}", ct);
    }

    internal static Task UnstageAllAsync(string repoPath, CancellationToken ct = default) =>
        RunAsync(repoPath, "restore --staged .", ct);

    internal static async Task CommitAsync(string repoPath, string message, bool amend, CancellationToken ct = default)
    {
        var amendFlag = amend ? " --amend" : string.Empty;
        // Write message to a temp file to avoid shell quoting issues
        var msgFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(msgFile, message, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), ct);
            await RunAsync(repoPath, $"commit{amendFlag} -F \"{msgFile}\"", ct);
        }
        finally
        {
            File.Delete(msgFile);
        }
    }

    internal static async Task<FileDiff?> GetFileDiffAsync(string repoPath, string filePath, bool staged, CancellationToken ct = default)
    {
        var cachedFlag = staged ? "--cached " : string.Empty;
        var raw = await RunAsync(repoPath, $"diff {cachedFlag}-- \"{filePath}\"", ct);

        if (string.IsNullOrEmpty(raw)) return null;

        var isNew = raw.Contains("new file mode");
        var isDeleted = raw.Contains("deleted file mode");
        var isRenamed = raw.Contains("rename from");
        var isBinary = raw.Contains("Binary files");

        int added = 0, removed = 0;
        string? oldPath = null, newPath = null;

        foreach (var line in raw.Split('\n'))
        {
            if (line.StartsWith("--- a/")) oldPath = line[6..];
            else if (line.StartsWith("+++ b/")) newPath = line[6..];
            else if (line.StartsWith('+') && !line.StartsWith("+++")) added++;
            else if (line.StartsWith('-') && !line.StartsWith("---")) removed++;
        }

        var hunks = isBinary ? [] : DiffParser.ParseHunks(raw);

        return new FileDiff(
            oldPath ?? filePath,
            newPath ?? filePath,
            isBinary, isNew, isDeleted, isRenamed,
            added, removed, hunks);
    }

    private static FileStatusEntry Entry(string path, string? orig, FileState index, FileState workdir) =>
        new(path, orig, index, workdir);

    private static FileState MapState(char c) => c switch
    {
        'A' => FileState.Added,
        'M' => FileState.Modified,
        'D' => FileState.Deleted,
        'R' => FileState.Renamed,
        'C' => FileState.Copied,
        _ => FileState.Unmodified,
    };
}
