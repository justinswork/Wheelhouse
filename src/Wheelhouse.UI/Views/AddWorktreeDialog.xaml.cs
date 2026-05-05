using System.Windows;

namespace Wheelhouse.UI.Views;

public partial class AddWorktreeDialog : Window
{
    public string WorktreePath { get; private set; } = string.Empty;
    public string Branch { get; private set; } = string.Empty;
    public bool CreateNewBranch { get; private set; }

    public AddWorktreeDialog() => InitializeComponent();

    private void OnBrowseClick(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select worktree folder"
        };
        if (dialog.ShowDialog() == true)
            PathBox.Text = dialog.FolderName;
    }

    private void OnAddClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(PathBox.Text))
        {
            MessageBox.Show("Please enter a worktree path.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (string.IsNullOrWhiteSpace(BranchBox.Text))
        {
            MessageBox.Show("Please enter a branch name.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        WorktreePath = PathBox.Text.Trim();
        Branch = BranchBox.Text.Trim();
        CreateNewBranch = CreateBranchCheck.IsChecked == true;
        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;
}
