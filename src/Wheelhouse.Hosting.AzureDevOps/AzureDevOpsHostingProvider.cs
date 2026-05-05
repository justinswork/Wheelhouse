using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using System.Text.RegularExpressions;
using Wheelhouse.Hosting.Abstractions;

namespace Wheelhouse.Hosting.AzureDevOps;

public sealed class AzureDevOpsHostingProvider : IHostingProvider
{
    private const string CredentialTarget = "Wheelhouse:AzureDevOps";
    private readonly ILogger<AzureDevOpsHostingProvider> _logger;
    private string? _pat;
    private string? _organizationUrl;

    public string Id => "azuredevops";
    public string DisplayName => "Azure DevOps";

    public AzureDevOpsHostingProvider(ILogger<AzureDevOpsHostingProvider> logger)
    {
        _logger = logger;
        var stored = CredentialStore.Load(CredentialTarget);
        if (stored is not null)
        {
            var parts = stored.Split('\n', 2);
            if (parts.Length == 2) { _organizationUrl = parts[0]; _pat = parts[1]; }
        }
    }

    public bool CanHandleUrl(string remoteUrl) =>
        remoteUrl.Contains("dev.azure.com", StringComparison.OrdinalIgnoreCase) ||
        remoteUrl.Contains("visualstudio.com", StringComparison.OrdinalIgnoreCase);

    public async Task<bool> IsAuthenticatedAsync(CancellationToken ct = default)
    {
        if (_pat is null || _organizationUrl is null) return false;
        try
        {
            var conn = CreateConnection(_organizationUrl, _pat);
            await conn.ConnectAsync(ct);
            return true;
        }
        catch { return false; }
    }

    public async Task<bool> ConnectWithTokenAsync(string token, CancellationToken ct = default)
    {
        // token format: "orgUrl\npat"
        var parts = token.Split('\n', 2);
        if (parts.Length != 2) return false;
        var (orgUrl, pat) = (parts[0].TrimEnd('/'), parts[1]);
        try
        {
            var conn = CreateConnection(orgUrl, pat);
            await conn.ConnectAsync(ct);
            _organizationUrl = orgUrl;
            _pat = pat;
            CredentialStore.Save(CredentialTarget, $"{orgUrl}\n{pat}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Azure DevOps token validation failed");
            return false;
        }
    }

    public Task<bool> AuthenticateAsync(CancellationToken ct = default) => Task.FromResult(false);

    public Task SignOutAsync(CancellationToken ct = default)
    {
        _pat = null;
        _organizationUrl = null;
        CredentialStore.Delete(CredentialTarget);
        return Task.CompletedTask;
    }

    public async Task<string?> GetConnectedUserAsync(CancellationToken ct = default)
    {
        if (_pat is null || _organizationUrl is null) return null;
        try
        {
            var conn = CreateConnection(_organizationUrl, _pat);
            await conn.ConnectAsync(ct);
            return conn.AuthenticatedIdentity?.ProviderDisplayName;
        }
        catch { return null; }
    }

    public async Task<IEnumerable<IRemoteRepository>> GetRepositoriesAsync(CancellationToken ct = default)
    {
        EnsureCredentials();
        var conn = CreateConnection(_organizationUrl!, _pat!);
        var client = conn.GetClient<GitHttpClient>();
        var repos = await client.GetRepositoriesAsync(cancellationToken: ct);
        return repos.Select(r => (IRemoteRepository)new AzureDevOpsRepository(r));
    }

    public async Task<IEnumerable<IPullRequest>> GetPullRequestsAsync(string repoUrl, CancellationToken ct = default)
    {
        EnsureCredentials();
        var (orgUrl, project, repoName) = ParseUrl(repoUrl);
        var conn = CreateConnection(orgUrl, _pat!);
        var client = conn.GetClient<GitHttpClient>();
        var criteria = new GitPullRequestSearchCriteria { Status = PullRequestStatus.Active };
        var prs = await client.GetPullRequestsByProjectAsync(project, criteria, cancellationToken: ct);
        return prs.Where(p => p.Repository?.Name.Equals(repoName, StringComparison.OrdinalIgnoreCase) == true)
                  .Select(p => (IPullRequest)new AzureDevOpsPullRequest(p));
    }

    public async Task<IPullRequest?> GetPullRequestAsync(string repoUrl, int number, CancellationToken ct = default)
    {
        EnsureCredentials();
        var (orgUrl, project, repoName) = ParseUrl(repoUrl);
        var conn = CreateConnection(orgUrl, _pat!);
        var client = conn.GetClient<GitHttpClient>();
        try
        {
            var pr = await client.GetPullRequestAsync(project, repoName, number, cancellationToken: ct);
            return new AzureDevOpsPullRequest(pr);
        }
        catch { return null; }
    }

    public async Task<IPullRequest> CreatePullRequestAsync(CreatePullRequestOptions options, CancellationToken ct = default)
    {
        EnsureCredentials();
        var (orgUrl, project, repoName) = ParseUrl(options.RepoUrl);
        var conn = CreateConnection(orgUrl, _pat!);
        var client = conn.GetClient<GitHttpClient>();
        var newPr = new GitPullRequest
        {
            Title = options.Title,
            Description = options.Body,
            SourceRefName = $"refs/heads/{options.HeadBranch}",
            TargetRefName = $"refs/heads/{options.BaseBranch}",
            IsDraft = options.Draft
        };
        var pr = await client.CreatePullRequestAsync(newPr, project, repoName, cancellationToken: ct);
        return new AzureDevOpsPullRequest(pr);
    }

    public async Task MergePullRequestAsync(string repoUrl, int number,
        Wheelhouse.Hosting.Abstractions.MergeMethod method, CancellationToken ct = default)
    {
        EnsureCredentials();
        var (orgUrl, project, repoName) = ParseUrl(repoUrl);
        var conn = CreateConnection(orgUrl, _pat!);
        var client = conn.GetClient<GitHttpClient>();

        var mergeStrategy = method switch
        {
            Wheelhouse.Hosting.Abstractions.MergeMethod.Squash => GitPullRequestMergeStrategy.Squash,
            Wheelhouse.Hosting.Abstractions.MergeMethod.Rebase => GitPullRequestMergeStrategy.Rebase,
            _ => GitPullRequestMergeStrategy.NoFastForward
        };

        // Must supply last merge source commit ID to complete
        var existing = await client.GetPullRequestAsync(project, repoName, number, cancellationToken: ct);
        var update = new GitPullRequest
        {
            Status = PullRequestStatus.Completed,
            LastMergeSourceCommit = existing.LastMergeSourceCommit,
            CompletionOptions = new GitPullRequestCompletionOptions { MergeStrategy = mergeStrategy }
        };
        await client.UpdatePullRequestAsync(update, project, repoName, number, cancellationToken: ct);
    }

    public async Task<IEnumerable<ICheckRun>> GetCheckRunsAsync(string repoUrl, string commitSha, CancellationToken ct = default)
    {
        EnsureCredentials();
        var (orgUrl, project, repoName) = ParseUrl(repoUrl);
        var conn = CreateConnection(orgUrl, _pat!);
        var gitClient = conn.GetClient<GitHttpClient>();
        var buildClient = conn.GetClient<BuildHttpClient>();

        var repo = await gitClient.GetRepositoryAsync(project, repoName, cancellationToken: ct);
        var builds = await buildClient.GetBuildsAsync(project,
            repositoryId: repo.Id.ToString(),
            repositoryType: "TfsGit",
            cancellationToken: ct);

        return builds
            .Where(b => string.Equals(b.SourceVersion, commitSha, StringComparison.OrdinalIgnoreCase))
            .Select(b => (ICheckRun)new AzureDevOpsBuildCheckRun(b));
    }

    private void EnsureCredentials()
    {
        if (_pat is null || _organizationUrl is null)
            throw new InvalidOperationException("Not authenticated with Azure DevOps.");
    }

    private static VssConnection CreateConnection(string orgUrl, string pat) =>
        new(new Uri(orgUrl), new VssBasicCredential(string.Empty, pat));

    private static (string orgUrl, string project, string repo) ParseUrl(string url)
    {
        // https://dev.azure.com/{org}/{project}/_git/{repo}
        var m = Regex.Match(url,
            @"(https://dev\.azure\.com/[^/]+)/([^/]+)/_git/([^/]+?)(?:\.git)?$",
            RegexOptions.IgnoreCase);
        if (m.Success)
            return (m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value);

        // https://{org}.visualstudio.com/{project}/_git/{repo}
        m = Regex.Match(url,
            @"(https://[^.]+\.visualstudio\.com)/([^/]+)/_git/([^/]+?)(?:\.git)?$",
            RegexOptions.IgnoreCase);
        if (m.Success)
            return (m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value);

        throw new ArgumentException($"Cannot parse Azure DevOps URL: {url}");
    }
}

file sealed class AzureDevOpsRepository(GitRepository r) : IRemoteRepository
{
    public string Name => r.Name;
    public string FullName => $"{r.ProjectReference?.Name}/{r.Name}";
    public string CloneUrl => r.RemoteUrl;
    public string? Description => null;
    public bool IsPrivate => true;
    public bool IsFork => false;
    public string DefaultBranch => r.DefaultBranch?.Replace("refs/heads/", "") ?? "main";
}

file sealed class AzureDevOpsPullRequest(GitPullRequest p) : IPullRequest
{
    public int Number => p.PullRequestId;
    public string Title => p.Title ?? "";
    public string? Body => string.IsNullOrEmpty(p.Description) ? null : p.Description;
    public string State => p.Status == PullRequestStatus.Active ? "open" : "closed";
    public string AuthorLogin => p.CreatedBy?.UniqueName ?? "";
    public string HeadBranch => p.SourceRefName?.Replace("refs/heads/", "") ?? "";
    public string BaseBranch => p.TargetRefName?.Replace("refs/heads/", "") ?? "";
    public string HeadSha => p.LastMergeSourceCommit?.CommitId ?? "";
    public bool IsDraft => p.IsDraft ?? false;
    public DateTimeOffset CreatedAt => new DateTimeOffset(p.CreationDate.ToUniversalTime(), TimeSpan.Zero);
    public DateTimeOffset? MergedAt => p.Status == PullRequestStatus.Completed && p.ClosedDate != default
        ? new DateTimeOffset(p.ClosedDate.ToUniversalTime(), TimeSpan.Zero)
        : null;
    public string Url => p.Repository?.RemoteUrl is string repoUrl
        ? $"{repoUrl}/pullrequest/{p.PullRequestId}"
        : "";
    public int CommentCount => 0;
    public bool IsOpen => p.Status == PullRequestStatus.Active;
}

file sealed class AzureDevOpsBuildCheckRun(Build b) : ICheckRun
{
    public string Name => b.Definition?.Name ?? "Build";
    public string Status => b.Status switch
    {
        BuildStatus.Completed => "completed",
        BuildStatus.InProgress => "in_progress",
        BuildStatus.NotStarted => "queued",
        _ => "unknown"
    };
    public string? Conclusion => b.Result switch
    {
        BuildResult.Succeeded => "success",
        BuildResult.Failed => "failure",
        BuildResult.Canceled => "cancelled",
        BuildResult.PartiallySucceeded => "neutral",
        _ => null
    };
    public string? DetailsUrl => null;
    public string App => "Azure Pipelines";
}
