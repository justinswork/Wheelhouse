using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Wheelhouse.Core.Models;
using Wheelhouse.Core.Services;
using Wheelhouse.Hosting.Abstractions;
using Wheelhouse.UI.Messages;
using Wheelhouse.UI.Services;

namespace Wheelhouse.UI.ViewModels;

public sealed partial class PullRequestsViewModel : ViewModelBase,
    IRecipient<RepositoryOpenedMessage>,
    IRecipient<RepositoryClosedMessage>,
    IRecipient<PullRequestsChangedMessage>
{
    private readonly IRepositoryService _repositoryService;
    private readonly IHostingService _hostingService;
    private string? _repoUrl;
    private IHostingProvider? _provider;

    [ObservableProperty] private ObservableCollection<PullRequestItemViewModel> _pullRequests = [];
    [ObservableProperty] private PullRequestItemViewModel? _selectedPullRequest;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isAuthenticated;
    [ObservableProperty] private string _noContentMessage = "Open a repository to view pull requests.";

    public PullRequestsViewModel(IRepositoryService repositoryService, IHostingService hostingService)
    {
        _repositoryService = repositoryService;
        _hostingService = hostingService;
        WeakReferenceMessenger.Default.RegisterAll(this);
    }

    void IRecipient<RepositoryOpenedMessage>.Receive(RepositoryOpenedMessage msg) =>
        InitializeAsync(msg.Value).ConfigureAwait(false);

    void IRecipient<RepositoryClosedMessage>.Receive(RepositoryClosedMessage _)
    {
        _repoUrl = null;
        _provider = null;
        Application.Current.Dispatcher.Invoke(() =>
        {
            PullRequests.Clear();
            IsAuthenticated = false;
            NoContentMessage = "Open a repository to view pull requests.";
        });
    }

    void IRecipient<PullRequestsChangedMessage>.Receive(PullRequestsChangedMessage _) =>
        LoadAsync().ConfigureAwait(false);

    private async Task InitializeAsync(RepositoryInfo repo)
    {
        _repoUrl = repo.RemoteUrl;
        if (_repoUrl is null)
        {
            Application.Current.Dispatcher.Invoke(() => NoContentMessage = "No remote URL configured for this repository.");
            return;
        }

        _provider = _hostingService.GetProviderForUrl(_repoUrl);
        if (_provider is null)
        {
            Application.Current.Dispatcher.Invoke(() => NoContentMessage = "No hosting provider found for this repository.");
            return;
        }

        var authed = await _provider.IsAuthenticatedAsync();
        Application.Current.Dispatcher.Invoke(() =>
        {
            IsAuthenticated = authed;
            if (!authed)
                NoContentMessage = $"Connect to {_provider.DisplayName} in Account Settings to view pull requests.";
        });

        if (authed) await LoadAsync();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (_provider is null || _repoUrl is null) return;
        IsLoading = true;
        try
        {
            var prs = await _provider.GetPullRequestsAsync(_repoUrl);
            var items = prs.Select(p => new PullRequestItemViewModel(p, _provider, _repoUrl)).ToList();

            Application.Current.Dispatcher.Invoke(() =>
            {
                PullRequests = new ObservableCollection<PullRequestItemViewModel>(items);
                NoContentMessage = items.Count == 0 ? "No open pull requests." : "";
            });

            foreach (var item in items)
                _ = item.LoadCiStatusAsync();
        }
        catch (Exception ex)
        {
            Application.Current.Dispatcher.Invoke(() => NoContentMessage = $"Failed to load: {ex.Message}");
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task CreatePullRequestAsync()
    {
        if (_provider is null || _repoUrl is null) return;

        List<string> branches = [];
        if (_repositoryService.IsOpen)
        {
            try
            {
                var branchInfos = await _repositoryService.GetBranchesAsync();
                branches = branchInfos.Select(b => b.FriendlyName).ToList();
            }
            catch { /* non-fatal */ }
        }

        var currentBranch = _repositoryService.CurrentRepository is not null
            ? branches.FirstOrDefault() ?? ""
            : "";

        var dialog = new Views.CreatePrDialog(branches) { Owner = Application.Current.MainWindow };
        if (dialog.ShowDialog() != true) return;

        try
        {
            var options = new CreatePullRequestOptions(
                _repoUrl,
                dialog.PrTitle,
                dialog.Body,
                dialog.HeadBranch,
                dialog.BaseBranch,
                dialog.IsDraft);
            await _provider.CreatePullRequestAsync(options);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to create pull request: {ex.Message}", "Create PR",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
