using System.Windows;
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
}
