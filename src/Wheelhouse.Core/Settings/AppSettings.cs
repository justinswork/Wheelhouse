namespace Wheelhouse.Core.Settings;

public sealed class AppSettings
{
    public string Theme { get; set; } = "System";
    public IList<string> RecentRepositories { get; set; } = new List<string>();
    public int MaxRecentRepositories { get; set; } = 20;
    public string DefaultShell { get; set; } = "powershell";
    public bool ShowTerminalOnStartup { get; set; } = false;
    public string FontFamily { get; set; } = "Cascadia Code";
    public double FontSize { get; set; } = 13;
    public bool DiffSideBySide { get; set; } = false;
    public bool DiffWordWrap { get; set; } = false;
}
