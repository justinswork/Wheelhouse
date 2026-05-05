using System.Windows;
using System.Windows.Controls;
using Wheelhouse.Hosting.Abstractions;
using Wheelhouse.UI.Services;

namespace Wheelhouse.UI.Views;

public partial class AccountSettingsDialog : Window
{
    private readonly IHostingService _hostingService;

    public AccountSettingsDialog(IHostingService hostingService)
    {
        InitializeComponent();
        _hostingService = hostingService;
        Loaded += async (_, _) => await BuildProviderPanelsAsync();
    }

    private async Task BuildProviderPanelsAsync()
    {
        ProvidersPanel.Items.Clear();
        foreach (var provider in _hostingService.AllProviders)
        {
            var isAuthed = await provider.IsAuthenticatedAsync();
            var user = isAuthed ? await provider.GetConnectedUserAsync() : null;
            ProvidersPanel.Items.Add(BuildProviderItem(provider, isAuthed, user));
        }
    }

    private UIElement BuildProviderItem(IHostingProvider provider, bool isAuthed, string? user)
    {
        var border = new Border
        {
            BorderThickness = new Thickness(0, 0, 0, 1),
            BorderBrush = (System.Windows.Media.Brush)FindResource("BrushBorderSubtle"),
            Padding = new Thickness(0, 10, 0, 10),
            Margin = new Thickness(0, 0, 0, 2)
        };

        var panel = new DockPanel();
        border.Child = panel;

        var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal };
        DockPanel.SetDock(buttonPanel, Dock.Right);
        panel.Children.Add(buttonPanel);

        if (isAuthed)
        {
            var disconnectBtn = new Button { Content = "Disconnect", Padding = new Thickness(10, 4, 10, 4) };
            disconnectBtn.Click += async (_, _) =>
            {
                await provider.SignOutAsync();
                await BuildProviderPanelsAsync();
            };
            buttonPanel.Children.Add(disconnectBtn);
        }
        else
        {
            var connectBtn = new Button { Content = "Connect with PAT", Padding = new Thickness(10, 4, 10, 4) };
            connectBtn.Click += async (_, _) => await ConnectWithPatAsync(provider);
            buttonPanel.Children.Add(connectBtn);
        }

        var infoPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        panel.Children.Add(infoPanel);

        infoPanel.Children.Add(new TextBlock
        {
            Text = provider.DisplayName,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold
        });
        infoPanel.Children.Add(new TextBlock
        {
            Text = isAuthed ? $"Connected as {user ?? "unknown"}" : "Not connected",
            FontSize = 11,
            Foreground = (System.Windows.Media.Brush)FindResource(
                isAuthed ? "BrushPrimary" : "BrushOnSurfaceMuted"),
            Margin = new Thickness(0, 2, 0, 0)
        });

        return border;
    }

    private async Task ConnectWithPatAsync(IHostingProvider provider)
    {
        string token;

        if (provider.Id == "azuredevops")
        {
            var orgDialog = new InputDialog("Azure DevOps — Organization URL",
                "Enter your Azure DevOps organization URL:\n(e.g. https://dev.azure.com/myorg)") { Owner = this };
            if (orgDialog.ShowDialog() != true || string.IsNullOrWhiteSpace(orgDialog.Value)) return;

            var patDialog = new InputDialog("Azure DevOps — Personal Access Token",
                "Enter your Personal Access Token (PAT):") { Owner = this };
            if (patDialog.ShowDialog() != true || string.IsNullOrWhiteSpace(patDialog.Value)) return;

            token = $"{orgDialog.Value.Trim().TrimEnd('/')}\n{patDialog.Value.Trim()}";
        }
        else
        {
            var dialog = new InputDialog($"{provider.DisplayName} — Personal Access Token",
                $"Enter your {provider.DisplayName} Personal Access Token (PAT):") { Owner = this };
            if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.Value)) return;
            token = dialog.Value.Trim();
        }

        var success = await provider.ConnectWithTokenAsync(token);
        if (success)
            await BuildProviderPanelsAsync();
        else
            MessageBox.Show("Authentication failed. Please check your credentials and try again.",
                "Connection Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
