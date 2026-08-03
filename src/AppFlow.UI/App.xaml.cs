namespace AppFlow.UI;

using AppFlow.Core.Interfaces;
using AppFlow.Core.Models;
using AppFlow.Core.Services;
using AppFlow.Database;
using AppFlow.Database.Repositories;
using AppFlow.Sources;
using AppFlow.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

/// <summary>Application entry point, dependency registration, and startup initialization.</summary>
public partial class App : Application
{
    private MainWindow? _window;

    public static new App Current => (App)Application.Current;
    public IServiceProvider Services { get; }

    public App()
    {
        InitializeComponent();
        Services = ConfigureServices();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        string? startupError = null;
        try
        {
            var db = Services.GetRequiredService<AppFlowDb>();
            await db.InitializeAsync();
            await LoadPersistedStateAsync();
        }
        catch (Exception ex)
        {
            startupError = "AppFlow could not initialize its local database. Some pages may be unavailable.";
            try { Services.GetService<ILogService>()?.LogError("Application startup failed.", ex); }
            catch (Exception) { /* Logging must not prevent the error window from opening. */ }
        }

        _window = new MainWindow();
        _window.Activate();
        ApplyTheme(Services.GetRequiredService<AppSettings>().Theme);

        if (startupError is not null)
            await _window.ShowStartupErrorAsync(startupError);
    }

    public void ApplyTheme(string theme) => _window?.ApplyTheme(theme);

    private async Task LoadPersistedStateAsync()
    {
        var settingsRepository = Services.GetRequiredService<SettingsRepository>();
        var persisted = await settingsRepository.LoadAppSettingsAsync();
        var settings = Services.GetRequiredService<AppSettings>();
        settings.Theme = persisted.Theme;
        settings.DefaultSourceId = persisted.DefaultSourceId;
        settings.CustomInstallPath = persisted.CustomInstallPath;
        settings.EnableSecurityValidation = true;
        settings.EnableSourceLocking = true;
        settings.MaxSearchResults = Math.Clamp(persisted.MaxSearchResults, 1, 500);
        settings.QueryTimeoutSeconds = Math.Clamp(persisted.QueryTimeoutSeconds, 1, 60);

        var registry = Services.GetRequiredService<SourceRegistry>();
        var adapters = Services.GetServices<ISourceAdapter>();
        foreach (var adapter in adapters)
        {
            registry.SetEnabled(
                adapter.SourceId,
                await settingsRepository.GetSourceEnabledAsync(adapter.SourceId));
        }
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        services.AddSingleton<AppSettings>();
        services.AddSingleton<SourceRegistry>();

        services.AddSingleton<AppFlowDb>();
        services.AddSingleton<InstallRecordRepository>();
        services.AddSingleton<IInstallRecordStore>(provider =>
            provider.GetRequiredService<InstallRecordRepository>());
        services.AddSingleton<FavoritesRepository>();
        services.AddSingleton<HistoryRepository>();
        services.AddSingleton<IActionHistoryStore>(provider =>
            provider.GetRequiredService<HistoryRepository>());
        services.AddSingleton<SettingsRepository>();

        services.AddSingleton<IInstallerIntegrityVerifier, InstallerIntegrityVerifier>();
        services.AddSingleton<ISourceAdapter, WinGetAdapter>();
        services.AddSingleton<ISourceAdapter, ChocolateyAdapter>();
        services.AddSingleton<ISourceAdapter, ScoopAdapter>();
        services.AddSingleton<ISourceAdapter, GitHubAdapter>();
        services.AddSingleton<ISourceAdapter, OfflineAdapter>();

        services.AddSingleton<ISecurityValidator, SecurityValidator>();
        services.AddSingleton<SourceResolver>();
        services.AddSingleton<IActionEngine, ActionEngine>();
        services.AddSingleton<IPackageService, PackageService>();
        services.AddSingleton<ILogService, LogService>();

        services.AddTransient<DashboardViewModel>();
        services.AddTransient<SearchViewModel>();
        services.AddTransient<AppDetailViewModel>();
        services.AddTransient<FavoritesViewModel>();
        services.AddTransient<HistoryViewModel>();
        services.AddTransient<SourcesViewModel>();
        services.AddTransient<SettingsViewModel>();

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
    }
}
