using Microsoft.Extensions.Logging;
using Octokit;
using System.Text.RegularExpressions;
using Wheelhouse.Hosting.Abstractions;

namespace Wheelhouse.Hosting.GitHub;

public sealed class GitHubHostingProvider : IHostingProvider
{
    private const string CredentialTarget = "Wheelhouse:GitHub";
    private readonly ILogger<GitHubHostingProvider> _logger;
    private GitHubClient? _client;

    public string Id => "github";
    public string DisplayName => "GitHub";

    public GitHubHostingProvider(ILogger<GitHubHostingProvider> logger)
    {
        _logger = logger;
        var token = CredentialStore.Load(CredentialTarget);
        if (token is not null)
            _client = CreateClient(token);
    }

    public bool CanHandleUrl(string remoteUrl) =>
        remoteUrl.Contains("github.com", StringComparison.OrdinalIgnoreCase);

    public async Task<bool> IsAuthenticatedAsync(CancellationToken ct = default)
    {
        if (_client is null) return false;
        try { await _client.User.Current(); return true; }
        catch { return false; }
    }

    public async Task<bool> ConnectWithTokenAsync(string token, CancellationToken ct = default)
    {
        var client = CreateClient(token);
        try
        {
            await client.User.Current();
            _client = client;
            CredentialStore.Save(CredentialTarget, token);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GitHub token validation failed");
            return false;
        }
    }

    public Task<bool> AuthenticateAsync(CancellationToken ct = default) =>
        Task.FromResult(false);

    public Task SignOutAsync(CancellationToken ct = default)
    {
        _client = null;
        CredentialStore.Delete(CredentialTarget);
        return Task.CompletedTask;
    }

    public async Task<string?> GetConnectedUserAsync(CancellationToken ct = default)
    {
        if (_client is null) return null;
        try { return (await _client.User.Current()).Login; }
        catch { return null; }
    }

    public async Task<IEnumerable<IRemoteRepository>> GetRepositoriesAsync(CancellationToken ct = default)
    {
        EnsureClient();
        var repos = await _client!.Repository.GetAllForCurrent();
        return repos.Select(r => (IRemoteRepository)new GitHubRepository(r));
    }

    public async Task<IEnumerable<IPullRequest>> GetPullRequestsAsync(string repoUrl, CancellationToken ct = default)
    {
        EnsureClient();
        var (owner, repo) = ParseUrl(repoUrl);
        var prs = await _client!.PullRequest.GetAllForRepository(
            owner, repo, new PullRequestRequest { State = ItemStateFilter.Open });
        return prs.Select(p => (IPullRequest)new GitHubPullRequest(p));
    }

    public async Task<IPullRequest?> GetPullRequestAsync(string repoUrl, int number, CancellationToken ct = default)
    {
        EnsureClient();
        var (owner, repo) = ParseUrl(repoUrl);
        try { return new GitHubPullRequest(await _client!.PullRequest.Get(owner, repo, number)); }
        catch (NotFoundException) { return null; }
    }

    public async Task<IPullRequest> CreatePullRequestAsync(CreatePullRequestOptions options, CancellationToken ct = default)
    {
        EnsureClient();
        var (owner, repo) = ParseUrl(options.RepoUrl);
        var newPr = new NewPullRequest(options.Title, options.HeadBranch, options.BaseBranch)
        {
            Body = options.Body,
            Draft = options.Draft
        };
        return new GitHubPullRequest(await _client!.PullRequest.Create(owner, repo, newPr));
    }

    public async Task MergePullRequestAsync(string repoUrl, int number,
        Wheelhouse.Hosting.Abstractions.MergeMethod method, CancellationToken ct = default)
    {
        EnsureClient();
        var (owner, repo) = ParseUrl(repoUrl);
        var octokitMethod = method switch
        {
            Wheelhouse.Hosting.Abstractions.MergeMethod.Squash => PullRequestMergeMethod.Squash,
            Wheelhouse.Hosting.Abstractions.MergeMethod.Rebase => PullRequestMergeMethod.Rebase,
            _ => PullRequestMergeMethod.Merge
        };
        await _client!.PullRequest.Merge(owner, repo, number, new MergePullRequest { MergeMethod = octokitMethod });
    }

    public async Task<IEnumerable<ICheckRun>> GetCheckRunsAsync(string repoUrl, string commitSha, CancellationToken ct = default)
    {
        EnsureClient();
        var (owner, repo) = ParseUrl(repoUrl);
        var response = await _client!.Check.Run.GetAllForReference(owner, repo, commitSha);
        return response.CheckRuns.Select(r => (ICheckRun)new GitHubCheckRun(r));
    }

    private void EnsureClient()
    {
        if (_client is null) throw new InvalidOperationException("Not authenticated with GitHub.");
    }

    private static GitHubClient CreateClient(string token) =>
        new(new ProductHeaderValue("Wheelhouse")) { Credentials = new Credentials(token) };

    private static (string owner, string repo) ParseUrl(string url)
    {
        var m = Regex.Match(url, @"github\.com[/:]([^/]+)/([^/]+?)(?:\.git)?$", RegexOptions.IgnoreCase);
        if (m.Success) return (m.Groups[1].Value, m.Groups[2].Value);
        throw new ArgumentException($"Cannot parse GitHub URL: {url}");
    }
}

file sealed class GitHubRepository(Octokit.Repository r) : IRemoteRepository
{
    public string Name => r.Name;
    public string FullName => r.FullName;
    public string CloneUrl => r.CloneUrl;
    public string? Description => r.Description;
    public bool IsPrivate => r.Private;
    public bool IsFork => r.Fork;
    public string DefaultBranch => r.DefaultBranch;
}

file sealed class GitHubPullRequest(Octokit.PullRequest p) : IPullRequest
{
    public int Number => p.Number;
    public string Title => p.Title;
    public string? Body => p.Body;
    public string State => p.State.Value.ToString().ToLower();
    public string AuthorLogin => p.User.Login;
    public string HeadBranch => p.Head.Ref;
    public string BaseBranch => p.Base.Ref;
    public string HeadSha => p.Head.Sha;
    public bool IsDraft => p.Draft;
    public DateTimeOffset CreatedAt => p.CreatedAt;
    public DateTimeOffset? MergedAt => p.MergedAt;
    public string Url => p.HtmlUrl;
    public int CommentCount => p.Comments;
    public bool IsOpen => p.State.Value == Octokit.ItemState.Open;
}

file sealed class GitHubCheckRun(Octokit.CheckRun r) : ICheckRun
{
    public string Name => r.Name;
    public string Status => r.Status.Value switch
    {
        Octokit.CheckStatus.InProgress => "in_progress",
        Octokit.CheckStatus.Completed  => "completed",
        Octokit.CheckStatus.Queued     => "queued",
        _                              => r.Status.Value.ToString().ToLower()
    };
    public string? Conclusion => r.Conclusion?.Value switch
    {
        Octokit.CheckConclusion.Success   => "success",
        Octokit.CheckConclusion.Failure   => "failure",
        Octokit.CheckConclusion.Cancelled => "cancelled",
        Octokit.CheckConclusion.Neutral   => "neutral",
        null                              => null,
        var v                             => v.GetValueOrDefault().ToString().ToLower()
    };
    public string? DetailsUrl => r.DetailsUrl;
    public string App => r.App?.Name ?? "";
}
