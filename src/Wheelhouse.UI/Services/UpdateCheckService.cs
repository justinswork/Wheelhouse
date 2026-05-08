using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.Messaging;
using Wheelhouse.UI.Messages;

namespace Wheelhouse.UI.Services;

public sealed class UpdateCheckService
{
    private const string ReleasesUrl =
        "https://api.github.com/repos/justinswork/Wheelhouse/releases/latest";

    public async Task CheckAsync()
    {
        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Wheelhouse-UpdateCheck/1.0");

            var release = await http.GetFromJsonAsync<GitHubRelease>(ReleasesUrl);
            if (release?.TagName is null) return;

            var tagName = release.TagName.TrimStart('v');
            if (!Version.TryParse(tagName, out var latest)) return;

            var current = Assembly.GetEntryAssembly()
                              ?.GetName().Version ?? new Version(0, 0, 0);

            if (latest > current)
            {
                var msixUrl = release.Assets?
                    .FirstOrDefault(a => a.Name?.EndsWith(".msix", StringComparison.OrdinalIgnoreCase) == true)
                    ?.BrowserDownloadUrl;

                WeakReferenceMessenger.Default.Send(new UpdateAvailableMessage(
                    latest.ToString(),
                    release.Body ?? string.Empty,
                    msixUrl));
            }
        }
        catch
        {
            // Non-fatal — network may not be available
        }
    }

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [JsonPropertyName("body")]
        public string? Body { get; set; }

        [JsonPropertyName("assets")]
        public GitHubAsset[]? Assets { get; set; }
    }

    private sealed class GitHubAsset
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("browser_download_url")]
        public string? BrowserDownloadUrl { get; set; }
    }
}
