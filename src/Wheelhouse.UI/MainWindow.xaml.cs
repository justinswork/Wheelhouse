using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using Wheelhouse.UI.ViewModels;

namespace Wheelhouse.UI;

public partial class MainWindow : Window
{
    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void OnExitClick(object sender, RoutedEventArgs e) => Close();

    private void OnUpdateBannerClick(object sender, MouseButtonEventArgs e) =>
        Process.Start(new ProcessStartInfo(
            "https://github.com/justinswork/Wheelhouse/releases/latest")
        { UseShellExecute = true });
}
