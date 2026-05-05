using System.Text.Json;
using Microsoft.Extensions.Logging;
using Wheelhouse.Core.Settings;

namespace Wheelhouse.Core.Services;

public sealed class SettingsService : ISettingsService
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Wheelhouse", "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly ILogger<SettingsService> _logger;

    public AppSettings Current { get; private set; } = new();
    public event EventHandler<AppSettings>? SettingsChanged;

    public SettingsService(ILogger<SettingsService> logger)
    {
        _logger = logger;
        Load();
    }

    public void Update(Action<AppSettings> mutate)
    {
        mutate(Current);
        SettingsChanged?.Invoke(this, Current);
    }

    public async Task SaveAsync(CancellationToken ct = default)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            var json = JsonSerializer.Serialize(Current, JsonOptions);
            await File.WriteAllTextAsync(SettingsPath, json, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings");
        }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return;
            var json = File.ReadAllText(SettingsPath);
            Current = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load settings, using defaults");
            Current = new AppSettings();
        }
    }
}
