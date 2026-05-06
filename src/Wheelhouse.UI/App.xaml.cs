using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Windows;
using Wheelhouse.Core.Services;
using Wheelhouse.Hosting.Abstractions;
using Wheelhouse.Hosting.AzureDevOps;
using Wheelhouse.Hosting.GitHub;
using Wheelhouse.Terminal;
using Wheelhouse.UI.Services;
using Wheelhouse.UI.ViewModels;

namespace Wheelhouse.UI;

public partial class App : Application
{
    private readonly IHost _host;

    public App()
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureLogging(logging =>
            {
                logging.AddDebug();
                logging.SetMinimumLevel(LogLevel.Debug);
            })
            .ConfigureServices(ConfigureServices)
            .Build();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Core
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IRepositoryService, LibGit2SharpRepositoryService>();
        services.AddSingleton<IThemeService, ThemeService>();

        // Hosting providers
        services.AddSingleton<IHostingProvider, GitHubHostingProvider>();
        services.AddSingleton<IHostingProvider, AzureDevOpsHostingProvider>();
        services.AddSingleton<IHostingService, HostingService>();

        // Terminal
        services.AddSingleton<ITerminalService, TerminalService>();

        // ViewModels
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<WorkingTreeViewModel>();
        services.AddSingleton<LogViewModel>();
        services.AddSingleton<DiffViewModel>();
        services.AddSingleton<RepositorySidebarViewModel>();
        services.AddSingleton<ReflogViewModel>();
        services.AddSingleton<PullRequestsViewModel>();
        services.AddSingleton<TerminalPaneViewModel>();

        // Services
        services.AddSingleton<RepositoryWatcher>();
        services.AddSingleton<UpdateCheckService>();

        // Windows
        services.AddSingleton<MainWindow>();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        await _host.StartAsync();

        var themeService = _host.Services.GetRequiredService<IThemeService>();
        themeService.Initialize();

        // Eagerly instantiate so it begins listening for RepositoryOpenedMessage
        _ = _host.Services.GetRequiredService<RepositoryWatcher>();

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();

        // Fire-and-forget update check — runs after window is shown
        _ = _host.Services.GetRequiredService<UpdateCheckService>().CheckAsync();

        base.OnStartup(e);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        var settings = _host.Services.GetRequiredService<ISettingsService>();
        await settings.SaveAsync();

        await _host.StopAsync();
        _host.Dispose();
        base.OnExit(e);
    }
}
