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

    private void OnUpdateBannerClick(object sender, MouseButtonEventArgs e)
    {
        var vm = (MainWindowViewModel)DataContext;
        if (vm.PendingUpdate is null) return;
        var dialog = new Views.UpdateDialog(vm.PendingUpdate) { Owner = this };
        dialog.ShowDialog();
    }
}
