namespace Wheelhouse.Terminal;

public enum ShellType { PowerShell7, WindowsPowerShell, CommandPrompt, GitBash, Wsl }

public sealed record ShellProfile(string Name, ShellType Type, string ExecutablePath, string? Arguments = null);

public interface ITerminalService
{
    IReadOnlyList<ShellProfile> AvailableShells { get; }
    ShellProfile DefaultShell { get; }
    Task<ITerminalSession> CreateSessionAsync(ShellProfile shell, string workingDirectory, CancellationToken ct = default);
}

public interface ITerminalSession : IAsyncDisposable
{
    Guid Id { get; }
    ShellProfile Shell { get; }
    string WorkingDirectory { get; }
    bool IsAlive { get; }
    Task WriteInputAsync(string input, CancellationToken ct = default);
    Task ResizeAsync(int columns, int rows, CancellationToken ct = default);
    event EventHandler<string> OutputReceived;
    event EventHandler SessionExited;
}
