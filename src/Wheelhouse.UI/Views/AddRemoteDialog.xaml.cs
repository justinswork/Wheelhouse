using System.Windows;

namespace Wheelhouse.UI.Views;

public partial class AddRemoteDialog : Window
{
    public string RemoteName => NameBox.Text.Trim();
    public string RemoteUrl => UrlBox.Text.Trim();

    public AddRemoteDialog()
    {
        InitializeComponent();
        NameBox.Focus();
    }

    private void OnFieldChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) =>
        AddButton.IsEnabled = !string.IsNullOrWhiteSpace(NameBox.Text) && !string.IsNullOrWhiteSpace(UrlBox.Text);

    private void OnAdd(object sender, RoutedEventArgs e) => DialogResult = true;
}
