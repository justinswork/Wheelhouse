using Wheelhouse.Hosting.Abstractions;

namespace Wheelhouse.UI.Services;

public interface IHostingService
{
    IReadOnlyList<IHostingProvider> AllProviders { get; }
    IHostingProvider? GetProviderForUrl(string remoteUrl);
}
