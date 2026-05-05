namespace Wheelhouse.Hosting.Abstractions;

public interface IHostingProvider
{
    string Id { get; }
    string DisplayName { get; }

    bool CanHandleUrl(string remoteUrl);

    Task<bool> IsAuthenticatedAsync(CancellationToken ct = default);
    Task<bool> ConnectWithTokenAsync(string token, CancellationToken ct = default);
    Task<bool> AuthenticateAsync(CancellationToken ct = default);
    Task SignOutAsync(CancellationToken ct = default);
    Task<string?> GetConnectedUserAsync(CancellationToken ct = default);

    Task<IEnumerable<IRemoteRepository>> GetRepositoriesAsync(CancellationToken ct = default);
    Task<IEnumerable<IPullRequest>> GetPullRequestsAsync(string repoUrl, CancellationToken ct = default);
    Task<IPullRequest?> GetPullRequestAsync(string repoUrl, int number, CancellationToken ct = default);
    Task<IPullRequest> CreatePullRequestAsync(CreatePullRequestOptions options, CancellationToken ct = default);
    Task MergePullRequestAsync(string repoUrl, int number, MergeMethod method, CancellationToken ct = default);
    Task<IEnumerable<ICheckRun>> GetCheckRunsAsync(string repoUrl, string commitSha, CancellationToken ct = default);
}
