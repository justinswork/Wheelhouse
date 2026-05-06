using Microsoft.Extensions.Logging;

namespace Wheelhouse.Terminal;

public sealed class TerminalService : ITerminalService
{
    private readonly ILogger<TerminalService> _logger;
    private IReadOnlyList<ShellProfile>? _shells;

    public IReadOnlyList<ShellProfile> AvailableShells => _shells ??= DetectShells();

    public ShellProfile DefaultShell =>
        AvailableShells.FirstOrDefault()
        ?? new ShellProfile("Windows PowerShell", ShellType.WindowsPowerShell,
               Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
                   @"WindowsPowerShell\v1.0\powershell.exe"));

    public TerminalService(ILogger<TerminalService> logger) => _logger = logger;

    public Task<ITerminalSession> CreateSessionAsync(ShellProfile shell, string workingDirectory,
        CancellationToken ct = default)
    {
        var session = new ConPtyTerminalSession(shell, workingDirectory, _logger);
        session.Start();
        return Task.FromResult<ITerminalSession>(session);
    }

    private static IReadOnlyList<ShellProfile> DetectShells()
    {
        var shells = new List<ShellProfile>();

        // PowerShell 7 (pwsh)
        var pwsh = FindInPath("pwsh.exe")
            ?? FirstExisting(
                @"C:\Program Files\PowerShell\7\pwsh.exe",
                @"C:\Program Files (x86)\PowerShell\7\pwsh.exe");
        if (pwsh is not null)
            shells.Add(new ShellProfile("PowerShell 7", ShellType.PowerShell7, pwsh));

        // Windows PowerShell 5.1
        var winPs = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
            @"WindowsPowerShell\v1.0\powershell.exe");
        if (File.Exists(winPs))
            shells.Add(new ShellProfile("Windows PowerShell", ShellType.WindowsPowerShell, winPs));

        // Command Prompt
        var cmd = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
        if (File.Exists(cmd))
            shells.Add(new ShellProfile("Command Prompt", ShellType.CommandPrompt, cmd));

        // Git Bash
        var gitBash = FirstExisting(
            @"C:\Program Files\Git\bin\bash.exe",
            @"C:\Program Files (x86)\Git\bin\bash.exe");
        if (gitBash is not null)
            shells.Add(new ShellProfile("Git Bash", ShellType.GitBash, gitBash, "--login -i"));

        // WSL (default distro)
        var wsl = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "wsl.exe");
        if (File.Exists(wsl))
            shells.Add(new ShellProfile("WSL", ShellType.Wsl, wsl));

        return shells;
    }

    private static string? FindInPath(string exe)
    {
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';'))
        {
            try
            {
                var path = Path.Combine(dir.Trim(), exe);
                if (File.Exists(path)) return path;
            }
            catch { }
        }
        return null;
    }

    private static string? FirstExisting(params string[] paths)
    {
        foreach (var p in paths)
            if (File.Exists(p)) return p;
        return null;
    }
}
