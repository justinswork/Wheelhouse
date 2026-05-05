using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System.Windows;
using Wheelhouse.Core.Models;
using Wheelhouse.Core.Services;
using Wheelhouse.UI.Messages;
using Wheelhouse.UI.Views;

namespace Wheelhouse.UI.ViewModels;

public sealed partial class BranchItemViewModel : ViewModelBase
{
    private readonly IRepositoryService _repositoryService;

    public BranchInfo Branch { get; }

    public string DisplayName => Branch.FriendlyName;
    public bool IsCurrent => Branch.IsCurrentRepositoryHead;
    public bool IsRemote => Branch.IsRemote;
    public bool IsLocal => !Branch.IsRemote;
    public int AheadBy => Branch.AheadBy;
    public int BehindBy => Branch.BehindBy;
    public string AheadBehind => (AheadBy, BehindBy) switch
    {
        (0, 0) => string.Empty,
        (var a, 0) => $"↑{a}",
        (0, var b) => $"↓{b}",
        (var a, var b) => $"↑{a} ↓{b}"
    };

    public BranchItemViewModel(BranchInfo branch, IRepositoryService repositoryService)
    {
        Branch = branch;
        _repositoryService = repositoryService;
    }

    [RelayCommand]
    private async Task CheckoutAsync()
    {
        if (IsCurrent) return;
        try
        {
            await _repositoryService.CheckoutBranchAsync(Branch.FriendlyName);
            WeakReferenceMessenger.Default.Send(new BranchChangedMessage());
            WeakReferenceMessenger.Default.Send(new WorkingTreeChangedMessage());
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Checkout failed: {ex.Message}", "Checkout", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (IsCurrent)
        {
            MessageBox.Show("Cannot delete the currently checked-out branch.", "Delete Branch", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (MessageBox.Show($"Delete branch '{DisplayName}'?", "Delete Branch", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        try
        {
            await _repositoryService.DeleteBranchAsync(Branch.FriendlyName, force: false);
            WeakReferenceMessenger.Default.Send(new BranchChangedMessage());
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("not fully merged") || ex.Message.Contains("not merged") || ex.Message.Contains("not an ancestor"))
            {
                if (MessageBox.Show($"'{DisplayName}' is not fully merged. Force delete?", "Force Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                    return;
                try
                {
                    await _repositoryService.DeleteBranchAsync(Branch.FriendlyName, force: true);
                    WeakReferenceMessenger.Default.Send(new BranchChangedMessage());
                }
                catch (Exception ex2)
                {
                    MessageBox.Show($"Delete failed: {ex2.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show($"Delete failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    [RelayCommand]
    private async Task RenameAsync()
    {
        var dialog = new InputDialog("Rename Branch", $"New name for '{DisplayName}':", DisplayName)
        {
            Owner = Application.Current.MainWindow
        };
        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.Value)) return;
        try
        {
            await _repositoryService.RenameBranchAsync(Branch.FriendlyName, dialog.Value.Trim());
            WeakReferenceMessenger.Default.Send(new BranchChangedMessage());
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Rename failed: {ex.Message}", "Rename Branch", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task DeleteRemoteBranchAsync()
    {
        if (!IsRemote) return;
        // Remote branch FriendlyName is e.g. "origin/main" — split on first "/"
        var slash = Branch.FriendlyName.IndexOf('/');
        if (slash < 0) return;
        var remoteName = Branch.FriendlyName[..slash];
        var branchName = Branch.FriendlyName[(slash + 1)..];

        if (MessageBox.Show($"Delete remote branch '{Branch.FriendlyName}'?\n\nThis cannot be undone.", "Delete Remote Branch", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;
        try
        {
            await _repositoryService.DeleteRemoteBranchAsync(remoteName, branchName);
            WeakReferenceMessenger.Default.Send(new BranchChangedMessage());
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Delete failed: {ex.Message}", "Delete Remote Branch", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task MergeIntoCurrentAsync()
    {
        if (IsCurrent) return;
        if (MessageBox.Show($"Merge '{DisplayName}' into current branch?", "Merge Branch", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
            return;
        try
        {
            await _repositoryService.MergeAsync(Branch.FriendlyName);
            WeakReferenceMessenger.Default.Send(new WorkingTreeChangedMessage());
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Merge failed: {ex.Message}", "Merge", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
