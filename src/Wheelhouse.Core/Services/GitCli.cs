using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
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

    internal static Task StageHunkAsync(string repoPath, string filePath, DiffHunk hunk, bool isNew, CancellationToken ct = default) =>
        ApplyPatchAsync(repoPath, BuildHunkPatch(filePath, hunk, isNew, false), cached: true, reverse: false, ct);

    internal static Task UnstageHunkAsync(string repoPath, string filePath, DiffHunk hunk, CancellationToken ct = default) =>
        ApplyPatchAsync(repoPath, BuildHunkPatch(filePath, hunk, false, false), cached: true, reverse: true, ct);

    internal static Task DiscardHunkAsync(string repoPath, string filePath, DiffHunk hunk, CancellationToken ct = default) =>
        ApplyPatchAsync(repoPath, BuildHunkPatch(filePath, hunk, false, false), cached: false, reverse: true, ct);

    internal static string BuildHunkPatch(string filePath, DiffHunk hunk, bool isNew, bool isDeleted)
    {
        // git apply requires Unix line endings (\n) regardless of platform
        var sb = new System.Text.StringBuilder();
        var oldPath = isNew ? "/dev/null" : $"a/{filePath}";
        var newPath = isDeleted ? "/dev/null" : $"b/{filePath}";
        sb.Append($"diff --git a/{filePath} b/{filePath}\n");
        if (isNew) sb.Append("new file mode 100644\n");
        else if (isDeleted) sb.Append("deleted file mode 100644\n");
        sb.Append($"--- {oldPath}\n");
        sb.Append($"+++ {newPath}\n");
        sb.Append(hunk.Header.TrimEnd('\r', '\n')).Append('\n');
        foreach (var line in hunk.Lines)
        {
            var prefix = line.Type switch
            {
                DiffLineType.Added   => "+",
                DiffLineType.Removed => "-",
                _                    => " "
            };
            sb.Append(prefix).Append(line.Content.TrimEnd('\r')).Append('\n');
        }
        return sb.ToString();
    }

    private static async Task ApplyPatchAsync(string repoPath, string patch, bool cached, bool reverse, CancellationToken ct)
    {
        var cachedFlag  = cached  ? " --cached" : string.Empty;
        var reverseFlag = reverse ? " -R"       : string.Empty;
        var patchFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(patchFile, patch, new System.Text.UTF8Encoding(false), ct);
            await RunAsync(repoPath, $"apply{cachedFlag}{reverseFlag} \"{patchFile}\"", ct);
        }
        finally
        {
            File.Delete(patchFile);
        }
    }

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

        if (string.IsNullOrEmpty(raw) && !staged)
        {
            var tracked = await RunAsync(repoPath, $"ls-files -- \"{filePath}\"", ct);
            if (string.IsNullOrWhiteSpace(tracked))
                raw = await RunDiffNoIndexAsync(repoPath, filePath, ct);
        }

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

    // Branch management

    internal static Task CheckoutBranchAsync(string repoPath, string friendlyName, CancellationToken ct = default) =>
        RunAsync(repoPath, $"checkout \"{friendlyName}\"", ct);

    internal static Task CreateBranchAsync(string repoPath, string name, string? startPoint, bool checkout, CancellationToken ct = default)
    {
        var cmd = checkout ? "checkout -b" : "branch";
        var start = startPoint is not null ? $" \"{startPoint}\"" : string.Empty;
        return RunAsync(repoPath, $"{cmd} \"{name}\"{start}", ct);
    }

    internal static Task DeleteBranchAsync(string repoPath, string friendlyName, bool force, CancellationToken ct = default) =>
        RunAsync(repoPath, $"branch {(force ? "-D" : "-d")} \"{friendlyName}\"", ct);

    internal static Task RenameBranchAsync(string repoPath, string currentName, string newName, CancellationToken ct = default) =>
        RunAsync(repoPath, $"branch -m \"{currentName}\" \"{newName}\"", ct);

    internal static Task DeleteRemoteBranchAsync(string repoPath, string remoteName, string branchName, CancellationToken ct = default) =>
        RunAsync(repoPath, $"push \"{remoteName}\" --delete \"{branchName}\"", ct);

    // Tags

    internal static async Task<IReadOnlyList<TagInfo>> GetTagsAsync(string repoPath, CancellationToken ct = default)
    {
        string output;
        try
        {
            // Format: name\tobjecttype\tobjectname\tcreatordate\tsubject
            output = await RunAsync(repoPath, "tag -l --format=%(refname:short)%09%(objecttype)%09%(objectname)%09%(creatordate:iso-strict)%09%(contents:subject)", ct);
        }
        catch { return []; }

        var tags = new List<TagInfo>();
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split('\t', 5);
            if (parts.Length < 3) continue;
            var name = parts[0].Trim();
            var isAnnotated = parts[1].Trim() == "tag";
            var sha = parts[2].Trim();
            DateTimeOffset? when = parts.Length >= 4 && DateTimeOffset.TryParse(parts[3].Trim(), out var dt) ? dt : null;
            var msg = parts.Length >= 5 ? parts[4].Trim() : null;
            if (!string.IsNullOrEmpty(name))
                tags.Add(new TagInfo(name, sha, isAnnotated, when, string.IsNullOrEmpty(msg) ? null : msg));
        }
        return tags;
    }

    internal static Task CreateTagAsync(string repoPath, string name, string? targetSha, string? message, CancellationToken ct = default)
    {
        var target = targetSha is not null ? $" \"{targetSha}\"" : string.Empty;
        if (message is not null)
            return RunAsync(repoPath, $"tag -a \"{name}\"{target} -m \"{message.Replace("\"", "\\\"")}\"", ct);
        return RunAsync(repoPath, $"tag \"{name}\"{target}", ct);
    }

    internal static Task DeleteTagAsync(string repoPath, string name, CancellationToken ct = default) =>
        RunAsync(repoPath, $"tag -d \"{name}\"", ct);

    internal static Task PushTagAsync(string repoPath, string name, string? remoteName, CancellationToken ct = default) =>
        RunAsync(repoPath, $"push \"{remoteName ?? "origin"}\" \"{name}\"", ct);

    // Remotes

    internal static async Task<IReadOnlyList<RemoteInfo>> GetRemotesAsync(string repoPath, CancellationToken ct = default)
    {
        string output;
        try { output = await RunAsync(repoPath, "remote -v", ct); }
        catch { return []; }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var remotes = new List<RemoteInfo>();
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var tab = line.IndexOf('\t');
            if (tab < 0) continue;
            var name = line[..tab].Trim();
            if (!seen.Add(name)) continue;
            var rest = line[(tab + 1)..].Trim();
            // "url (fetch)" — strip the trailing " (fetch)"/"  (push)"
            var paren = rest.LastIndexOf('(');
            var url = paren > 0 ? rest[..paren].Trim() : rest;
            remotes.Add(new RemoteInfo(name, url));
        }
        return remotes;
    }

    internal static Task AddRemoteAsync(string repoPath, string name, string url, CancellationToken ct = default) =>
        RunAsync(repoPath, $"remote add \"{name}\" \"{url}\"", ct);

    internal static Task RemoveRemoteAsync(string repoPath, string name, CancellationToken ct = default) =>
        RunAsync(repoPath, $"remote remove \"{name}\"", ct);

    internal static Task RenameRemoteAsync(string repoPath, string name, string newName, CancellationToken ct = default) =>
        RunAsync(repoPath, $"remote rename \"{name}\" \"{newName}\"", ct);

    internal static Task PruneRemoteAsync(string repoPath, string name, CancellationToken ct = default) =>
        RunAsync(repoPath, $"remote prune \"{name}\"", ct);

    // Stash

    internal static async Task<IReadOnlyList<StashInfo>> GetStashesAsync(string repoPath, CancellationToken ct = default)
    {
        var output = await RunAsync(repoPath, "stash list --format=%gd%x09%gs%x09%H%x09%aI", ct);
        var stashes = new List<StashInfo>();
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split('\t', 4);
            if (parts.Length < 4) continue;
            var refName = parts[0];
            var message = parts[1];
            var sha = parts[2];
            if (!DateTimeOffset.TryParse(parts[3].Trim(), out var when)) when = DateTimeOffset.Now;
            var m = Regex.Match(refName, @"\{(\d+)\}");
            var index = m.Success ? int.Parse(m.Groups[1].Value) : stashes.Count;
            stashes.Add(new StashInfo(index, message, sha, when));
        }
        return stashes;
    }

    internal static Task StashAsync(string repoPath, string? message, bool includeUntracked, CancellationToken ct = default)
    {
        var u = includeUntracked ? " -u" : string.Empty;
        var m = message is not null ? $" -m \"{message.Replace("\"", "\\\"")}\"" : string.Empty;
        return RunAsync(repoPath, $"stash push{u}{m}", ct);
    }

    internal static Task ApplyStashAsync(string repoPath, int index, CancellationToken ct = default) =>
        RunAsync(repoPath, $"stash apply stash@{{{index}}}", ct);

    internal static Task PopStashAsync(string repoPath, int index, CancellationToken ct = default) =>
        RunAsync(repoPath, $"stash pop stash@{{{index}}}", ct);

    internal static Task DropStashAsync(string repoPath, int index, CancellationToken ct = default) =>
        RunAsync(repoPath, $"stash drop stash@{{{index}}}", ct);

    // Advanced git operations

    internal static Task MergeAsync(string repoPath, string branchName, CancellationToken ct = default) =>
        RunAsync(repoPath, $"merge \"{branchName}\"", ct);

    internal static Task ResetAsync(string repoPath, string target, ResetMode mode, CancellationToken ct = default)
    {
        var flag = mode switch
        {
            ResetMode.Soft  => "--soft",
            ResetMode.Mixed => "--mixed",
            ResetMode.Hard  => "--hard",
            _               => "--mixed",
        };
        return RunAsync(repoPath, $"reset {flag} {target}", ct);
    }

    internal static Task RevertAsync(string repoPath, string commitSha, CancellationToken ct = default) =>
        RunAsync(repoPath, $"revert --no-edit {commitSha}", ct);

    internal static Task CherryPickAsync(string repoPath, string commitSha, CancellationToken ct = default) =>
        RunAsync(repoPath, $"cherry-pick {commitSha}", ct);

    // Rebase

    internal static Task RebaseAsync(string repoPath, string onto, CancellationToken ct = default) =>
        RunAsync(repoPath, $"rebase \"{onto}\"", ct);

    internal static Task AbortRebaseAsync(string repoPath, CancellationToken ct = default) =>
        RunAsync(repoPath, "rebase --abort", ct);

    internal static Task ContinueRebaseAsync(string repoPath, CancellationToken ct = default) =>
        RunAsync(repoPath, "rebase --continue", ct);

    // Reflog

    internal static async Task<IReadOnlyList<ReflogEntry>> GetReflogAsync(string repoPath, CancellationToken ct = default)
    {
        string output;
        try { output = await RunAsync(repoPath, "log -g --format=%H%x09%h%x09%gd%x09%gs%x09%aI", ct); }
        catch { return []; }

        var entries = new List<ReflogEntry>();
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split('\t', 5);
            if (parts.Length < 5) continue;
            DateTimeOffset.TryParse(parts[4].Trim(), out var when);
            entries.Add(new ReflogEntry(parts[0].Trim(), parts[1].Trim(), parts[2].Trim(), parts[3].Trim(), when));
        }
        return entries;
    }

    // File history

    internal static async Task<IReadOnlyList<CommitInfo>> GetFileHistoryAsync(string repoPath, string filePath, CancellationToken ct = default)
    {
        var fmt = "%H%x09%h%x09%s%x09%an%x09%ae%x09%aI%x09%P";
        string output;
        try { output = await RunAsync(repoPath, $"log --follow --format={fmt} -- \"{filePath}\"", ct); }
        catch { return []; }

        var commits = new List<CommitInfo>();
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var p = line.Split('\t', 7);
            if (p.Length < 6) continue;
            DateTimeOffset.TryParse(p[5].Trim(), out var when);
            var parents = p.Length >= 7
                ? p[6].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList()
                : new List<string>();
            commits.Add(new CommitInfo(p[0].Trim(), p[1].Trim(), p[2].Trim(), p[2].Trim(),
                p[3].Trim(), p[4].Trim(), when, p[3].Trim(), when, parents));
        }
        return commits;
    }

    // Commit file diff

    internal static async Task<FileDiff?> GetCommitFileDiffAsync(string repoPath, string commitSha, string filePath, CancellationToken ct = default)
    {
        string raw;
        try { raw = await RunAsync(repoPath, $"show {commitSha} -- \"{filePath}\"", ct); }
        catch { return null; }

        if (string.IsNullOrEmpty(raw)) return null;
        var diffStart = raw.IndexOf("diff --git", StringComparison.Ordinal);
        if (diffStart < 0) return null;
        raw = raw[diffStart..];

        var isNew     = raw.Contains("new file mode");
        var isDeleted = raw.Contains("deleted file mode");
        var isRenamed = raw.Contains("rename from");
        var isBinary  = raw.Contains("Binary files");

        int added = 0, removed = 0;
        string? oldPath = null, newPath = null;
        foreach (var l in raw.Split('\n'))
        {
            if (l.StartsWith("--- a/"))      oldPath = l[6..];
            else if (l.StartsWith("+++ b/")) newPath = l[6..];
            else if (l.StartsWith('+') && !l.StartsWith("+++")) added++;
            else if (l.StartsWith('-') && !l.StartsWith("---")) removed++;
        }

        var hunks = isBinary ? [] : DiffParser.ParseHunks(raw);
        return new FileDiff(oldPath ?? filePath, newPath ?? filePath,
            isBinary, isNew, isDeleted, isRenamed, added, removed, hunks);
    }

    // Blame

    internal static async Task<IReadOnlyList<BlameLine>> GetBlameAsync(string repoPath, string filePath, CancellationToken ct = default)
    {
        string output;
        try { output = await RunAsync(repoPath, $"blame --line-porcelain \"{filePath}\"", ct); }
        catch { return []; }

        var lines = output.Split('\n');
        var result = new List<BlameLine>();
        int i = 0;
        while (i < lines.Length)
        {
            var header = lines[i];
            var parts = header.Split(' ');
            if (parts.Length < 3 || parts[0].Length < 7) { i++; continue; }
            var sha = parts[0];
            if (!int.TryParse(parts[2], out var lineNum)) { i++; continue; }
            i++;

            string author = "";
            DateTimeOffset when = DateTimeOffset.MinValue;
            while (i < lines.Length && !lines[i].StartsWith('\t'))
            {
                var hdr = lines[i++];
                if (hdr.StartsWith("author ") && !hdr.StartsWith("author-"))
                    author = hdr[7..].Trim();
                else if (hdr.StartsWith("author-time ") && long.TryParse(hdr[12..].Trim(), out var ts))
                    when = DateTimeOffset.FromUnixTimeSeconds(ts);
            }

            var content = i < lines.Length && lines[i].StartsWith('\t') ? lines[i++][1..] : "";
            var shortSha = sha[..Math.Min(7, sha.Length)];
            result.Add(new BlameLine(sha, shortSha, author, when, lineNum, content));
        }
        return result;
    }

    // Worktrees

    internal static async Task<IReadOnlyList<WorktreeInfo>> GetWorktreesAsync(string repoPath, CancellationToken ct = default)
    {
        string output;
        try { output = await RunAsync(repoPath, "worktree list --porcelain", ct); }
        catch { return []; }

        var result = new List<WorktreeInfo>();
        string path = "", head = "", branch = "";
        bool isLocked = false, started = false;

        foreach (var line in output.Split('\n'))
        {
            if (line.StartsWith("worktree "))
            {
                if (started)
                    result.Add(new WorktreeInfo(path,
                        string.IsNullOrEmpty(branch) ? null : branch,
                        string.IsNullOrEmpty(head)   ? null : head,
                        result.Count == 0, isLocked));
                path = line[9..].Trim(); head = branch = ""; isLocked = false; started = true;
            }
            else if (line.StartsWith("HEAD "))    head   = line[5..].Trim();
            else if (line.StartsWith("branch "))  branch = line[7..].TrimStart().Replace("refs/heads/", "");
            else if (line.StartsWith("locked"))   isLocked = true;
        }

        if (started)
            result.Add(new WorktreeInfo(path,
                string.IsNullOrEmpty(branch) ? null : branch,
                string.IsNullOrEmpty(head)   ? null : head,
                result.Count == 0, isLocked));

        return result;
    }

    internal static Task AddWorktreeAsync(string repoPath, string worktreePath, string branch, bool createBranch, CancellationToken ct = default)
    {
        var newFlag = createBranch ? $"-b \"{branch}\" " : string.Empty;
        var branchArg = createBranch ? string.Empty : $" \"{branch}\"";
        return RunAsync(repoPath, $"worktree add {newFlag}\"{worktreePath}\"{branchArg}", ct);
    }

    internal static Task RemoveWorktreeAsync(string repoPath, string worktreePath, bool force, CancellationToken ct = default) =>
        RunAsync(repoPath, $"worktree remove{(force ? " --force" : "")} \"{worktreePath}\"", ct);

    internal static Task PruneWorktreesAsync(string repoPath, CancellationToken ct = default) =>
        RunAsync(repoPath, "worktree prune", ct);

    private static async Task<string> RunDiffNoIndexAsync(string repoPath, string filePath, CancellationToken ct)
    {
        var psi = new ProcessStartInfo("git", $"diff --no-index -- /dev/null \"{filePath}\"")
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

        // git diff --no-index exits with 1 when files differ (normal), 0 when identical, 2+ on error
        if (proc.ExitCode > 1)
        {
            var stderr = await proc.StandardError.ReadToEndAsync(ct);
            throw new InvalidOperationException($"git diff --no-index: {stderr.Trim()}");
        }

        return stdout;
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
