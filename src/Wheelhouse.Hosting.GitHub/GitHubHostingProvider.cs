using Microsoft.Extensions.Logging;
using Wheelhouse.Hosting.Abstractions;

namespace Wheelhouse.Hosting.GitHub;

public sealed class GitHubHostingProvider : IHostingProvider
{
    private readonly ILogger<GitHubHostingProvider> _logger;

    public string Id => "github";
    public string DisplayName => "GitHub";

    public GitHubHostingProvider(ILogger<GitHubHostingProvider> logger)
    {
        _logger = logger;
    }

    public Task<bool> IsAuthenticatedAsync(CancellationToken ct = default)
    {
        // TODO Phase 4: check Windows Credential Manager for stored token
        return Task.FromResult(false);
    }

    public Task<bool> AuthenticateAsync(CancellationToken ct = default)
    {
        // TODO Phase 4: OAuth device flow
        throw new NotImplementedException("GitHub authentication coming in Phase 4.");
    }

    public Task SignOutAsync(CancellationToken ct = default)
    {
        // TODO Phase 4
        return Task.CompletedTask;
    }

    public Task<IEnumerable<IRemoteRepository>> GetRepositoriesAsync(CancellationToken ct = default)
        => throw new NotImplementedException("GitHub integration coming in Phase 4.");

    public Task<IEnumerable<IPullRequest>> GetPullRequestsAsync(string repoUrl, CancellationToken ct = default)
        => throw new NotImplementedException("GitHub integration coming in Phase 4.");

    public Task<IPullRequest> CreatePullRequestAsync(CreatePullRequestOptions options, CancellationToken ct = default)
        => throw new NotImplementedException("GitHub integration coming in Phase 4.");

    public Task MergePullRequestAsync(string repoUrl, int number, MergeMethod method, CancellationToken ct = default)
        => throw new NotImplementedException("GitHub integration coming in Phase 4.");

    public Task<IEnumerable<ICheckRun>> GetCheckRunsAsync(string repoUrl, string commitSha, CancellationToken ct = default)
        => throw new NotImplementedException("GitHub integration coming in Phase 4.");
}
