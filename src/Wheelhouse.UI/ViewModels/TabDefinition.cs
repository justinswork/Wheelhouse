using CommunityToolkit.Mvvm.Input;

namespace Wheelhouse.UI.ViewModels;

public sealed partial class TabDefinition : ViewModelBase
{
    private readonly Action<TabDefinition>? _onClose;

    public string Header { get; }
    public bool CanClose { get; }
    public object ViewModel { get; }

    public TabDefinition(string header, object vm, bool canClose = false, Action<TabDefinition>? onClose = null)
    {
        Header = header;
        ViewModel = vm;
        CanClose = canClose;
        _onClose = onClose;
    }

    [RelayCommand]
    private void Close() => _onClose?.Invoke(this);
}
