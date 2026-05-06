using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Wheelhouse.Terminal;

namespace Wheelhouse.UI.ViewModels;

public sealed partial class TerminalTabViewModel : ViewModelBase
{
    public string TerminalId { get; } = Guid.NewGuid().ToString("N");
    public ITerminalSession Session { get; }

    [ObservableProperty] private string _header;
    [ObservableProperty] private bool _isActive;

    private readonly Action<TerminalTabViewModel> _onClose;

    public TerminalTabViewModel(ITerminalSession session, Action<TerminalTabViewModel> onClose)
    {
        Session = session;
        Header = session.Shell.Name;
        _onClose = onClose;

        session.SessionExited += (_, _) => Header = session.Shell.Name + " [exited]";
    }

    [RelayCommand]
    private async Task CloseAsync()
    {
        _onClose(this);
        await Session.DisposeAsync();
    }
}
