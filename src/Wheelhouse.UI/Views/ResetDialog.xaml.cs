using System.Windows;
using Wheelhouse.Core.Models;

namespace Wheelhouse.UI.Views;

public sealed partial class ResetDialog : Window
{
    public ResetMode SelectedMode { get; private set; } = ResetMode.Mixed;

    public ResetDialog(string commitMessage)
    {
        InitializeComponent();
        CommitLabel.Text = $"Reset to: {commitMessage}";
    }

    private void OnResetClick(object sender, RoutedEventArgs e)
    {
        if (HardRadio.IsChecked == true)
        {
            if (MessageBox.Show("Hard reset will permanently discard all uncommitted changes. Continue?",
                    "Hard Reset", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;
            SelectedMode = ResetMode.Hard;
        }
        else if (SoftRadio.IsChecked == true)
        {
            SelectedMode = ResetMode.Soft;
        }
        else
        {
            SelectedMode = ResetMode.Mixed;
        }
        DialogResult = true;
    }
}
