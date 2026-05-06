using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Wheelhouse.Core.Services;
using Wheelhouse.Terminal;
using Wheelhouse.UI.Messages;

namespace Wheelhouse.UI.ViewModels;

public sealed partial class TerminalPaneViewModel : ViewModelBase,
    IRecipient<RepositoryOpenedMessage>,
    IRecipient<RepositoryClosedMessage>
{
    private readonly ITerminalService _terminalService;
    private readonly IRepositoryService _repositoryService;
    private string _workingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    [ObservableProperty] private ObservableCollection<TerminalTabViewModel> _tabs = [];
    [ObservableProperty] private TerminalTabViewModel? _activeTab;

    public event EventHandler<TerminalTabViewModel>? TabCreated;
    public event EventHandler<TerminalTabViewModel>? TabClosed;
    public event EventHandler<TerminalTabViewModel>? TabActivated;

    public TerminalPaneViewModel(ITerminalService terminalService, IRepositoryService repositoryService)
    {
        _terminalService = terminalService;
        _repositoryService = repositoryService;
        WeakReferenceMessenger.Default.Register<RepositoryOpenedMessage>(this);
        WeakReferenceMessenger.Default.Register<RepositoryClosedMessage>(this);
    }

    void IRecipient<RepositoryOpenedMessage>.Receive(RepositoryOpenedMessage msg) =>
        _workingDirectory = msg.Value.Path;

    void IRecipient<RepositoryClosedMessage>.Receive(RepositoryClosedMessage _) =>
        _workingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    partial void OnActiveTabChanged(TerminalTabViewModel? value)
    {
        foreach (var t in Tabs) t.IsActive = t == value;
        if (value is not null) TabActivated?.Invoke(this, value);
    }

    [RelayCommand]
    public async Task AddTabAsync()
    {
        try
        {
            var shell = _terminalService.DefaultShell;
            var session = await _terminalService.CreateSessionAsync(shell, _workingDirectory);
            var tab = new TerminalTabViewModel(session, CloseTab);

            Application.Current.Dispatcher.Invoke(() =>
            {
                Tabs.Add(tab);
                ActiveTab = tab;
            });

            TabCreated?.Invoke(this, tab);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to start terminal: {ex.Message}", "Terminal",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CloseTab(TerminalTabViewModel tab)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var idx = Tabs.IndexOf(tab);
            Tabs.Remove(tab);
            if (ActiveTab == tab)
                ActiveTab = Tabs.ElementAtOrDefault(Math.Max(0, idx - 1));
            TabClosed?.Invoke(this, tab);
        });
    }
}
