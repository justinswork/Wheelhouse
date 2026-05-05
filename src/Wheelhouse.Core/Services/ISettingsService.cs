using Wheelhouse.Core.Settings;

namespace Wheelhouse.Core.Services;

public interface ISettingsService
{
    AppSettings Current { get; }
    Task SaveAsync(CancellationToken ct = default);
    void Update(Action<AppSettings> mutate);
    event EventHandler<AppSettings> SettingsChanged;
}
