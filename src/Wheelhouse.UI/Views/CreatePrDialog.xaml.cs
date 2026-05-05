using System.Windows;

namespace Wheelhouse.UI.Views;

public partial class CreatePrDialog : Window
{
    public string PrTitle => TitleBox.Text.Trim();
    public string? Body => string.IsNullOrWhiteSpace(BodyBox.Text) ? null : BodyBox.Text.Trim();
    public string HeadBranch => (HeadBranchBox.Text ?? "").Trim();
    public string BaseBranch => (BaseBranchBox.Text ?? "").Trim();
    public bool IsDraft => DraftCheckBox.IsChecked == true;

    public CreatePrDialog(IEnumerable<string> branches)
    {
        InitializeComponent();
        var list = branches.ToList();
        HeadBranchBox.ItemsSource = list;
        BaseBranchBox.ItemsSource = list;
        if (list.Count > 0) HeadBranchBox.SelectedIndex = 0;
        if (list.Count > 1) BaseBranchBox.SelectedIndex = 1;
        else if (list.Count > 0) BaseBranchBox.Text = "main";
    }

    private void OnCreateClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TitleBox.Text))
        {
            MessageBox.Show("Title is required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            TitleBox.Focus();
            return;
        }
        if (string.IsNullOrWhiteSpace(HeadBranchBox.Text))
        {
            MessageBox.Show("From branch is required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            HeadBranchBox.Focus();
            return;
        }
        if (string.IsNullOrWhiteSpace(BaseBranchBox.Text))
        {
            MessageBox.Show("Into branch is required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            BaseBranchBox.Focus();
            return;
        }
        DialogResult = true;
    }

    private void OnTitleChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (CreateButton is not null)
            CreateButton.IsEnabled = !string.IsNullOrWhiteSpace(TitleBox.Text);
    }
}
