using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System.Windows;
using Wheelhouse.Core.Models;
using Wheelhouse.Core.Services;
using Wheelhouse.UI.Messages;

namespace Wheelhouse.UI.ViewModels;

public sealed partial class WorktreeItemViewModel : ViewModelBase
{
    private readonly IRepositoryService _repositoryService;

    public WorktreeInfo Worktree { get; }
    public string DisplayPath  => System.IO.Path.GetFileName(Worktree.Path.TrimEnd('/', '\\'));
    public string Branch       => Worktree.Branch ?? "(detached)";
    public string ShortSha     => Worktree.HeadSha?[..Math.Min(7, Worktree.HeadSha.Length)] ?? string.Empty;
    public bool IsMain         => Worktree.IsMain;
    public bool IsLocked       => Worktree.IsLocked;
    public bool CanRemove      => !Worktree.IsMain;

    public WorktreeItemViewModel(WorktreeInfo worktree, IRepositoryService repositoryService)
    {
        Worktree = worktree;
        _repositoryService = repositoryService;
    }

    [RelayCommand]
    private async Task RemoveAsync()
    {
        if (MessageBox.Show($"Remove worktree '{DisplayPath}'?",
                "Remove Worktree", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;
        try
        {
            await _repositoryService.RemoveWorktreeAsync(Worktree.Path, force: false);
            WeakReferenceMessenger.Default.Send(new WorkingTreeChangedMessage());
        }
        catch (Exception ex)
        {
            if (MessageBox.Show($"Remove failed: {ex.Message}\n\nForce remove?",
                    "Remove Worktree", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                try
                {
                    await _repositoryService.RemoveWorktreeAsync(Worktree.Path, force: true);
                    WeakReferenceMessenger.Default.Send(new WorkingTreeChangedMessage());
                }
                catch (Exception ex2)
                {
                    MessageBox.Show($"Force remove failed: {ex2.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
