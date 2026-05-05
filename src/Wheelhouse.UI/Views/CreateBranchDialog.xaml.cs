using System.Windows;
using System.Windows.Controls;

namespace Wheelhouse.UI.Views;

public sealed partial class CreateBranchDialog : Window
{
    public string BranchName { get; private set; } = string.Empty;
    public bool CheckoutImmediately { get; private set; } = true;

    public CreateBranchDialog()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            BranchNameBox.Focus();
            CreateButton.IsEnabled = false;
        };
    }

    private void OnBranchNameChanged(object sender, TextChangedEventArgs e) =>
        CreateButton.IsEnabled = BranchNameBox.Text.Trim().Length > 0;

    private void OnCreateClick(object sender, RoutedEventArgs e)
    {
        BranchName = BranchNameBox.Text.Trim();
        if (BranchName.Length == 0) return;
        CheckoutImmediately = CheckoutCheckBox.IsChecked == true;
        DialogResult = true;
    }
}
