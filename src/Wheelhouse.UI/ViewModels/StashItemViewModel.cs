using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System.Windows;
using Wheelhouse.Core.Models;
using Wheelhouse.Core.Services;
using Wheelhouse.UI.Messages;

namespace Wheelhouse.UI.ViewModels;

public sealed partial class StashItemViewModel : ViewModelBase
{
    private readonly IRepositoryService _repositoryService;

    public StashInfo Stash { get; }

    public int Index => Stash.Index;
    public string Message => Stash.Message;
    public string RelativeDate => FormatRelativeDate(Stash.When);

    public StashItemViewModel(StashInfo stash, IRepositoryService repositoryService)
    {
        Stash = stash;
        _repositoryService = repositoryService;
    }

    [RelayCommand]
    private async Task ApplyAsync()
    {
        try
        {
            await _repositoryService.ApplyStashAsync(Index, drop: false);
            WeakReferenceMessenger.Default.Send(new WorkingTreeChangedMessage());
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Apply failed: {ex.Message}", "Apply Stash", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task PopAsync()
    {
        try
        {
            await _repositoryService.ApplyStashAsync(Index, drop: true);
            WeakReferenceMessenger.Default.Send(new StashChangedMessage());
            WeakReferenceMessenger.Default.Send(new WorkingTreeChangedMessage());
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Pop failed: {ex.Message}", "Pop Stash", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task DropAsync()
    {
        if (MessageBox.Show($"Drop '{Message}'?", "Drop Stash", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;
        try
        {
            await _repositoryService.DropStashAsync(Index);
            WeakReferenceMessenger.Default.Send(new StashChangedMessage());
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Drop failed: {ex.Message}", "Drop Stash", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static string FormatRelativeDate(DateTimeOffset date)
    {
        var delta = DateTimeOffset.Now - date;
        return delta.TotalSeconds switch
        {
            < 60      => "just now",
            < 3600    => $"{(int)delta.TotalMinutes}m ago",
            < 86400   => $"{(int)delta.TotalHours}h ago",
            < 2592000 => $"{(int)delta.TotalDays}d ago",
            < 31536000 => $"{(int)(delta.TotalDays / 30)}mo ago",
            _          => $"{(int)(delta.TotalDays / 365)}y ago"
        };
    }
}
