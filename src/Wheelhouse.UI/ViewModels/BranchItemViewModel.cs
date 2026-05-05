using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Wheelhouse.Core.Models;
using Wheelhouse.Core.Services;
using Wheelhouse.UI.Messages;

namespace Wheelhouse.UI.ViewModels;

public sealed partial class BranchItemViewModel : ViewModelBase
{
    private readonly IRepositoryService _repositoryService;

    public BranchInfo Branch { get; }

    public string DisplayName => Branch.FriendlyName;
    public bool IsCurrent => Branch.IsCurrentRepositoryHead;
    public bool IsRemote => Branch.IsRemote;
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
        // TODO: full checkout with dirty-check dialog in Phase 2 polish
        await Task.CompletedTask;
        WeakReferenceMessenger.Default.Send(new WorkingTreeChangedMessage());
    }
}
