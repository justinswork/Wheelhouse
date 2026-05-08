using System.IO;
using System.Net.Http;
using System.Windows;
using Wheelhouse.UI.Messages;
using Wheelhouse.UI.Properties;

namespace Wheelhouse.UI.Views;

public partial class UpdateDialog : Window
{
    private readonly string? _downloadUrl;

    public UpdateDialog(UpdateAvailableMessage update)
    {
        InitializeComponent();
        _downloadUrl = update.DownloadUrl;
        HeaderText.Text = string.Format(Strings.Update_DialogHeader, update.Version);
        ReleaseNotesBox.Text = update.ReleaseNotes;
    }

    private void OnLaterClick(object sender, RoutedEventArgs e) => Close();

    private async void OnUpdateNowClick(object sender, RoutedEventArgs e)
    {
        if (_downloadUrl is null)
        {
            Close();
            return;
        }

        UpdateNowButton.IsEnabled = false;
        LaterButton.IsEnabled = false;
        UpdateNowButton.Content = Strings.Update_Downloading;

        try
        {
            var tempPath = Path.Combine(Path.GetTempPath(), "Wheelhouse-update.msix");
            using var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Wheelhouse-UpdateCheck/1.0");
            var bytes = await http.GetByteArrayAsync(_downloadUrl);
            await File.WriteAllBytesAsync(tempPath, bytes);

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(tempPath)
            {
                UseShellExecute = true
            });

            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Download failed: {ex.Message}", Strings.Update_DialogTitle,
                MessageBoxButton.OK, MessageBoxImage.Error);
            UpdateNowButton.IsEnabled = true;
            LaterButton.IsEnabled = true;
            UpdateNowButton.Content = Strings.Update_Now;
        }
    }
}
