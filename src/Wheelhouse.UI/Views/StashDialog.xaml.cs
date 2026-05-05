using System.Windows;

namespace Wheelhouse.UI.Views;

public sealed partial class StashDialog : Window
{
    public string Message { get; private set; } = string.Empty;
    public bool IncludeUntracked { get; private set; } = true;

    public StashDialog()
    {
        InitializeComponent();
        Loaded += (_, _) => MessageTextBox.Focus();
    }

    private void OnStashClick(object sender, RoutedEventArgs e)
    {
        Message = MessageTextBox.Text.Trim();
        IncludeUntracked = UntrackedCheckBox.IsChecked == true;
        DialogResult = true;
    }
}
