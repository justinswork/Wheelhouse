using System.IO;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Wheelhouse.Core.Models;
using Wheelhouse.UI.Messages;

namespace Wheelhouse.UI.ViewModels;

public sealed partial class FileStatusItemViewModel : ViewModelBase
{
    public FileStatusEntry Entry { get; }
    public bool IsStaged { get; }

    public string FilePath => Entry.FilePath;
    public string FileName => Path.GetFileName(Entry.FilePath);
    public string Directory => Path.GetDirectoryName(Entry.FilePath) ?? string.Empty;

    public FileState State => IsStaged ? Entry.IndexState : Entry.WorkingTreeState;

    public string StateLabel => State switch
    {
        FileState.Added     => "A",
        FileState.Modified  => "M",
        FileState.Deleted   => "D",
        FileState.Renamed   => "R",
        FileState.Copied    => "C",
        FileState.Untracked => "?",
        FileState.Conflicted => "!",
        _                   => " "
    };

    public string StateColor => State switch
    {
        FileState.Added      => "BrushAdded",
        FileState.Modified   => "BrushModified",
        FileState.Deleted    => "BrushRemoved",
        FileState.Renamed    => "BrushModified",
        FileState.Untracked  => "BrushUntracked",
        FileState.Conflicted => "BrushConflicted",
        _                    => "BrushOnSurfaceMuted"
    };

    public FileStatusItemViewModel(FileStatusEntry entry, bool isStaged)
    {
        Entry = entry;
        IsStaged = isStaged;
    }

    [RelayCommand]
    private void OpenFileHistory() =>
        WeakReferenceMessenger.Default.Send(new OpenFileHistoryMessage(FilePath));

    [RelayCommand]
    private void OpenBlame() =>
        WeakReferenceMessenger.Default.Send(new OpenBlameMessage(FilePath));

    [RelayCommand]
    private void OpenIndexEditor() =>
        WeakReferenceMessenger.Default.Send(new OpenIndexEditorMessage(FilePath));
}
