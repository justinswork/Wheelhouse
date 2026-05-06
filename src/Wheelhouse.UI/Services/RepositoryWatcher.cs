using System.IO;
using CommunityToolkit.Mvvm.Messaging;
using Wheelhouse.UI.Messages;

namespace Wheelhouse.UI.Services;

/// <summary>
/// Watches the .git directory for changes and debounces them into WorkingTreeChangedMessage.
/// Prevents the user from needing to click Refresh after git operations complete.
/// </summary>
public sealed class RepositoryWatcher : IDisposable,
    IRecipient<RepositoryOpenedMessage>,
    IRecipient<RepositoryClosedMessage>
{
    private FileSystemWatcher? _watcher;
    private Timer? _debounce;
    private bool _disposed;

    public RepositoryWatcher()
    {
        WeakReferenceMessenger.Default.Register<RepositoryOpenedMessage>(this);
        WeakReferenceMessenger.Default.Register<RepositoryClosedMessage>(this);
    }

    void IRecipient<RepositoryOpenedMessage>.Receive(RepositoryOpenedMessage msg) =>
        Start(msg.Value.Path);

    void IRecipient<RepositoryClosedMessage>.Receive(RepositoryClosedMessage _) =>
        Stop();

    private void Start(string repoPath)
    {
        Stop();

        var gitDir = Path.Combine(repoPath, ".git");
        if (!Directory.Exists(gitDir)) return;

        _debounce = new Timer(OnDebounceElapsed, null, Timeout.Infinite, Timeout.Infinite);

        _watcher = new FileSystemWatcher(gitDir)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
            IncludeSubdirectories = true,
            EnableRaisingEvents = true
        };
        _watcher.Changed += OnGitDirEvent;
        _watcher.Created += OnGitDirEvent;
        _watcher.Deleted += OnGitDirEvent;
        _watcher.Renamed += OnGitDirRenamed;
        _watcher.Error   += OnWatcherError;
    }

    private void Stop()
    {
        _watcher?.Dispose();
        _watcher = null;
        _debounce?.Dispose();
        _debounce = null;
    }

    private void OnGitDirEvent(object sender, FileSystemEventArgs e)
    {
        // Skip the object store — bulk writes happen during fetch/clone and would overwhelm the UI
        if (IsObjectStore(e.FullPath)) return;
        // Skip lock files — these are transient and always paired with the real change
        if (e.FullPath.EndsWith(".lock", StringComparison.OrdinalIgnoreCase)) return;
        // Skip the index file itself — git and libgit2 both rewrite it during stat-cache
        // refreshes (every status read), which would cause an infinite refresh loop.
        if (string.Equals(Path.GetFileName(e.FullPath), "index", StringComparison.OrdinalIgnoreCase)) return;

        _debounce?.Change(400, Timeout.Infinite);
    }

    private void OnGitDirRenamed(object sender, RenamedEventArgs e)
    {
        if (IsObjectStore(e.FullPath)) return;
        if (string.Equals(Path.GetFileName(e.FullPath), "index", StringComparison.OrdinalIgnoreCase)) return;
        _debounce?.Change(400, Timeout.Infinite);
    }

    private static bool IsObjectStore(string path) =>
        path.Contains($"{Path.DirectorySeparatorChar}objects{Path.DirectorySeparatorChar}",
            StringComparison.OrdinalIgnoreCase);

    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        // Watcher can die if the directory is deleted or we hit the OS limit.
        // Try to restart the next time the repo is opened.
        Stop();
    }

    private static void OnDebounceElapsed(object? state) =>
        WeakReferenceMessenger.Default.Send(new WorkingTreeChangedMessage());

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        WeakReferenceMessenger.Default.UnregisterAll(this);
        Stop();
    }
}
