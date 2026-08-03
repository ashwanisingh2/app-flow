namespace AppFlow.UI;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

using AppFlow.Core.Interfaces;
using AppFlow.Core.Services;
using AppFlow.Database;
using AppFlow.Database.Repositories;
using AppFlow.Sources;
using AppFlow.UI.ViewModels;

/// <summary>
/// Application entry point. Configures DI container and initializes the database.
/// </summary>
public partial class App : Application
{
    private Window? _window;

    /// <summary>
    /// Gets the current App instance.
    /// </summary>
    public static new App Current => (App)Application.Current;

    /// <summary>
    /// Gets the DI service provider.
    /// </summary>
    public IServiceProvider Services { get; private set; } = null!;

    public App()
    {
        this.InitializeComponent();
        Services = ConfigureServices();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Initialize database on startup
        var db = Services.GetRequiredService<AppFlowDb>();
        await db.InitializeAsync();

        _window = new MainWindow();
        _window.Activate();
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Database
        services.AddSingleton<AppFlowDb>();
        services.AddSingleton<InstallRecordRepository>();
        services.AddSingleton<FavoritesRepository>();
        services.AddSingleton<HistoryRepository>();

        // Source Adapters
        services.AddSingleton<ISourceAdapter, WinGetAdapter>();
        services.AddSingleton<ISourceAdapter, ChocolateyAdapter>();
        services.AddSingleton<ISourceAdapter, ScoopAdapter>();
        services.AddSingleton<ISourceAdapter, GitHubAdapter>();
        services.AddSingleton<ISourceAdapter, OfflineAdapter>();

        // Core Services
        services.AddSingleton<ISecurityValidator, SecurityValidator>();
        services.AddSingleton<SourceResolver>();
        services.AddSingleton<IActionEngine, ActionEngine>();
        services.AddSingleton<IPackageService, PackageService>();
        services.AddSingleton<ILogService, LogService>();

        // ViewModels
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<SearchViewModel>();
        services.AddTransient<AppDetailViewModel>();
        services.AddTransient<SettingsViewModel>();

        return services.BuildServiceProvider();
    }
}
