using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Wheelhouse.Hosting.Abstractions;
using Wheelhouse.UI.Messages;

namespace Wheelhouse.UI.ViewModels;

public sealed partial class PullRequestItemViewModel : ViewModelBase
{
    public IPullRequest PullRequest { get; }
    private readonly IHostingProvider _provider;
    private readonly string _repoUrl;

    public int Number => PullRequest.Number;
    public string Title => PullRequest.Title;
    public string AuthorLogin => PullRequest.AuthorLogin;
    public string HeadBranch => PullRequest.HeadBranch;
    public string BaseBranch => PullRequest.BaseBranch;
    public bool IsDraft => PullRequest.IsDraft;
    public string RelativeDate => FormatRelativeDate(PullRequest.CreatedAt);
    public string CommentInfo => PullRequest.CommentCount > 0 ? $"{PullRequest.CommentCount} 💬" : "";
    public string DraftLabel => IsDraft ? " [Draft]" : "";

    [ObservableProperty] private string _ciStatus = "";

    public PullRequestItemViewModel(IPullRequest pr, IHostingProvider provider, string repoUrl)
    {
        PullRequest = pr;
        _provider = provider;
        _repoUrl = repoUrl;
    }

    [RelayCommand]
    private async Task MergeAsync()
    {
        if (MessageBox.Show($"Merge pull request #{Number}?\n\n\"{Title}\"",
                "Merge PR", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;
        try
        {
            await _provider.MergePullRequestAsync(_repoUrl, Number, MergeMethod.Merge);
            WeakReferenceMessenger.Default.Send(new PullRequestsChangedMessage());
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Merge failed: {ex.Message}", "Merge PR", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void OpenInBrowser() =>
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(PullRequest.Url)
            { UseShellExecute = true });

    public async Task LoadCiStatusAsync()
    {
        if (string.IsNullOrEmpty(PullRequest.HeadSha)) return;
        try
        {
            var checks = (await _provider.GetCheckRunsAsync(_repoUrl, PullRequest.HeadSha)).ToList();
            if (checks.Count == 0) return;

            if (checks.All(c => c.Conclusion == "success")) CiStatus = "✓";
            else if (checks.Any(c => c.Conclusion == "failure")) CiStatus = "✗";
            else if (checks.Any(c => c.Status == "in_progress")) CiStatus = "⟳";
            else CiStatus = "·";
        }
        catch { CiStatus = ""; }
    }

    private static string FormatRelativeDate(DateTimeOffset when)
    {
        var d = DateTimeOffset.Now - when;
        return d.TotalSeconds < 60   ? "just now"
             : d.TotalMinutes < 60   ? $"{(int)d.TotalMinutes}m ago"
             : d.TotalHours < 24     ? $"{(int)d.TotalHours}h ago"
             : d.TotalDays < 30      ? $"{(int)d.TotalDays}d ago"
             : d.TotalDays < 365     ? $"{(int)(d.TotalDays / 30)}mo ago"
             :                         $"{(int)(d.TotalDays / 365)}y ago";
    }
}
