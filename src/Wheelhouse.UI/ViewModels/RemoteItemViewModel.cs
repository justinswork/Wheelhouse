using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System.Windows;
using Wheelhouse.Core.Models;
using Wheelhouse.Core.Services;
using Wheelhouse.UI.Messages;
using Wheelhouse.UI.Views;

namespace Wheelhouse.UI.ViewModels;

public sealed partial class RemoteItemViewModel : ViewModelBase
{
    private readonly IRepositoryService _repositoryService;

    public RemoteInfo Remote { get; }
    public string Name => Remote.Name;
    public string Url => Remote.Url;

    public RemoteItemViewModel(RemoteInfo remote, IRepositoryService repositoryService)
    {
        Remote = remote;
        _repositoryService = repositoryService;
    }

    [RelayCommand]
    private async Task FetchAsync()
    {
        try
        {
            await _repositoryService.FetchAsync(Remote.Name);
            WeakReferenceMessenger.Default.Send(new BranchChangedMessage());
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Fetch failed: {ex.Message}", "Fetch", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task PruneAsync()
    {
        try
        {
            await _repositoryService.PruneRemoteAsync(Remote.Name);
            WeakReferenceMessenger.Default.Send(new BranchChangedMessage());
            MessageBox.Show($"Pruned stale remote-tracking branches for '{Name}'.", "Prune", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Prune failed: {ex.Message}", "Prune", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task RenameAsync()
    {
        var dialog = new InputDialog("Rename Remote", $"New name for '{Name}':", Name)
        {
            Owner = Application.Current.MainWindow
        };
        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.Value)) return;
        try
        {
            await _repositoryService.RenameRemoteAsync(Remote.Name, dialog.Value.Trim());
            WeakReferenceMessenger.Default.Send(new RemoteChangedMessage());
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Rename failed: {ex.Message}", "Rename Remote", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task RemoveAsync()
    {
        if (MessageBox.Show($"Remove remote '{Name}'?", "Remove Remote", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;
        try
        {
            await _repositoryService.RemoveRemoteAsync(Remote.Name);
            WeakReferenceMessenger.Default.Send(new RemoteChangedMessage());
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Remove failed: {ex.Message}", "Remove Remote", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
