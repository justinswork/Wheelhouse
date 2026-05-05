using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Wheelhouse.Core.Models;
using Wheelhouse.Core.Services;
using Wheelhouse.UI.Messages;

namespace Wheelhouse.UI.ViewModels;

public sealed partial class ReflogViewModel : ViewModelBase,
    IRecipient<RepositoryOpenedMessage>,
    IRecipient<RepositoryClosedMessage>
{
    private readonly IRepositoryService _repositoryService;

    [ObservableProperty] private ObservableCollection<ReflogEntryViewModel> _entries = [];
    [ObservableProperty] private ReflogEntryViewModel? _selectedEntry;
    [ObservableProperty] private bool _isLoading;

    public ReflogViewModel(IRepositoryService repositoryService)
    {
        _repositoryService = repositoryService;
        WeakReferenceMessenger.Default.RegisterAll(this);
        if (_repositoryService.IsOpen) LoadAsync().ConfigureAwait(false);
    }

    void IRecipient<RepositoryOpenedMessage>.Receive(RepositoryOpenedMessage _) => LoadAsync().ConfigureAwait(false);
    void IRecipient<RepositoryClosedMessage>.Receive(RepositoryClosedMessage _) =>
        Application.Current.Dispatcher.Invoke(() => Entries.Clear());

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (!_repositoryService.IsOpen) return;
        IsLoading = true;
        try
        {
            var entries = await _repositoryService.GetReflogAsync();
            Application.Current.Dispatcher.Invoke(() =>
                Entries = new ObservableCollection<ReflogEntryViewModel>(
                    entries.Select(e => new ReflogEntryViewModel(e, _repositoryService))));
        }
        finally { IsLoading = false; }
    }
}

public sealed partial class ReflogEntryViewModel : ViewModelBase
{
    private readonly IRepositoryService _repositoryService;
    public ReflogEntry Entry { get; }

    public string ShortSha  => Entry.ShortSha;
    public string RefName   => Entry.RefName;
    public string Message   => Entry.Message;
    public string RelativeDate => FormatRelativeDate(Entry.When);

    public ReflogEntryViewModel(ReflogEntry entry, IRepositoryService repositoryService)
    {
        Entry = entry;
        _repositoryService = repositoryService;
    }

    [RelayCommand]
    private async Task CheckoutAsync()
    {
        try
        {
            await _repositoryService.CheckoutBranchAsync(Entry.Sha);
            WeakReferenceMessenger.Default.Send(new BranchChangedMessage());
            WeakReferenceMessenger.Default.Send(new WorkingTreeChangedMessage());
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Checkout failed: {ex.Message}", "Checkout", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task ResetToAsync()
    {
        var dialog = new Wheelhouse.UI.Views.ResetDialog(Entry.Message) { Owner = Application.Current.MainWindow };
        if (dialog.ShowDialog() != true) return;
        try
        {
            await _repositoryService.ResetAsync(Entry.Sha, dialog.SelectedMode);
            WeakReferenceMessenger.Default.Send(new WorkingTreeChangedMessage());
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Reset failed: {ex.Message}", "Reset", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task CherryPickAsync()
    {
        try
        {
            await _repositoryService.CherryPickAsync(Entry.Sha);
            WeakReferenceMessenger.Default.Send(new WorkingTreeChangedMessage());
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Cherry-pick failed: {ex.Message}", "Cherry-pick", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static string FormatRelativeDate(DateTimeOffset when)
    {
        var d = DateTimeOffset.Now - when;
        return d.TotalSeconds < 60  ? "just now"
             : d.TotalMinutes < 60  ? $"{(int)d.TotalMinutes}m ago"
             : d.TotalHours < 24    ? $"{(int)d.TotalHours}h ago"
             : d.TotalDays < 30     ? $"{(int)d.TotalDays}d ago"
             : d.TotalDays < 365    ? $"{(int)(d.TotalDays / 30)}mo ago"
             :                        $"{(int)(d.TotalDays / 365)}y ago";
    }
}
