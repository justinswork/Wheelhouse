using System.Windows;

namespace Wheelhouse.UI.Views;

public partial class CreateTagDialog : Window
{
    public string TagName => TagNameBox.Text.Trim();
    public string? Message => string.IsNullOrWhiteSpace(MessageBox.Text) ? null : MessageBox.Text.Trim();

    public CreateTagDialog()
    {
        InitializeComponent();
        TagNameBox.Focus();
    }

    private void OnNameChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) =>
        CreateButton.IsEnabled = !string.IsNullOrWhiteSpace(TagNameBox.Text);

    private void OnCreate(object sender, RoutedEventArgs e) => DialogResult = true;
}
