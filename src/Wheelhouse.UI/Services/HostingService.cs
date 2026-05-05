using Wheelhouse.Hosting.Abstractions;

namespace Wheelhouse.UI.Services;

public sealed class HostingService : IHostingService
{
    public IReadOnlyList<IHostingProvider> AllProviders { get; }

    public HostingService(IEnumerable<IHostingProvider> providers)
    {
        AllProviders = providers.ToList();
    }

    public IHostingProvider? GetProviderForUrl(string remoteUrl) =>
        AllProviders.FirstOrDefault(p => p.CanHandleUrl(remoteUrl));
}
