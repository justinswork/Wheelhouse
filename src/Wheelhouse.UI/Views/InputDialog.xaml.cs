using System.Windows;

namespace Wheelhouse.UI.Views;

public partial class InputDialog : Window
{
    public string Value => InputBox.Text.Trim();

    public InputDialog(string title, string prompt, string initialValue = "")
    {
        InitializeComponent();
        Title = title;
        PromptText.Text = prompt;
        InputBox.Text = initialValue;
        InputBox.SelectAll();
        InputBox.Focus();
        OkButton.IsEnabled = !string.IsNullOrWhiteSpace(initialValue);
    }

    private void OnTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) =>
        OkButton.IsEnabled = !string.IsNullOrWhiteSpace(InputBox.Text);

    private void OnOk(object sender, RoutedEventArgs e) => DialogResult = true;
}
