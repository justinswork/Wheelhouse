using Microsoft.Extensions.Logging;
using Wheelhouse.Hosting.Abstractions;

namespace Wheelhouse.Hosting.AzureDevOps;

public sealed class AzureDevOpsHostingProvider : IHostingProvider
{
    private readonly ILogger<AzureDevOpsHostingProvider> _logger;

    public string Id => "azuredevops";
    public string DisplayName => "Azure DevOps";

    public AzureDevOpsHostingProvider(ILogger<AzureDevOpsHostingProvider> logger)
    {
        _logger = logger;
    }

    public Task<bool> IsAuthenticatedAsync(CancellationToken ct = default)
    {
        // TODO Phase 4: check Windows Credential Manager for stored PAT
        return Task.FromResult(false);
    }

    public Task<bool> AuthenticateAsync(CancellationToken ct = default)
    {
        // TODO Phase 4: PAT or Azure AD OAuth flow
        throw new NotImplementedException("Azure DevOps authentication coming in Phase 4.");
    }

    public Task SignOutAsync(CancellationToken ct = default)
    {
        // TODO Phase 4
        return Task.CompletedTask;
    }

    public Task<IEnumerable<IRemoteRepository>> GetRepositoriesAsync(CancellationToken ct = default)
        => throw new NotImplementedException("Azure DevOps integration coming in Phase 4.");

    public Task<IEnumerable<IPullRequest>> GetPullRequestsAsync(string repoUrl, CancellationToken ct = default)
        => throw new NotImplementedException("Azure DevOps integration coming in Phase 4.");

    public Task<IPullRequest> CreatePullRequestAsync(CreatePullRequestOptions options, CancellationToken ct = default)
        => throw new NotImplementedException("Azure DevOps integration coming in Phase 4.");

    public Task MergePullRequestAsync(string repoUrl, int number, MergeMethod method, CancellationToken ct = default)
        => throw new NotImplementedException("Azure DevOps integration coming in Phase 4.");

    public Task<IEnumerable<ICheckRun>> GetCheckRunsAsync(string repoUrl, string commitSha, CancellationToken ct = default)
        => throw new NotImplementedException("Azure DevOps integration coming in Phase 4.");
}
