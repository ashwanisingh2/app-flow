namespace AppFlow.UI.ViewModels;

using AppFlow.Core.Interfaces;
using AppFlow.Core.Models;
using AppFlow.Database.Repositories;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Text.Json;

public partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsRepository _settingsRepository;
    private readonly AppSettings _settings;
    private readonly IPackageService _packageService;

    [ObservableProperty] private string _selectedTheme = "System";
    [ObservableProperty] private double _queryTimeoutSeconds = 5;
    [ObservableProperty] private double _maxSearchResults = 50;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;

    public bool SecurityValidationEnabled => true;
    public bool SourceLockingEnabled => true;

    public SettingsViewModel(
        SettingsRepository settingsRepository,
        AppSettings settings,
        IPackageService packageService)
    {
        _settingsRepository = settingsRepository;
        _settings = settings;
        _packageService = packageService;
    }

    [RelayCommand]
    public void LoadSettings()
    {
        SelectedTheme = _settings.Theme;
        QueryTimeoutSeconds = _settings.QueryTimeoutSeconds;
        MaxSearchResults = _settings.MaxSearchResults;
    }

    [RelayCommand]
    public async Task SaveSettingsAsync()
    {
        _settings.Theme = SelectedTheme is "Light" or "Dark" or "System" ? SelectedTheme : "System";
        _settings.QueryTimeoutSeconds = Math.Clamp((int)Math.Round(QueryTimeoutSeconds), 1, 60);
        _settings.MaxSearchResults = Math.Clamp((int)Math.Round(MaxSearchResults), 1, 500);
        // These safeguards are mandatory and cannot be disabled.
        _settings.EnableSecurityValidation = true;
        _settings.EnableSourceLocking = true;

        await _settingsRepository.SaveAppSettingsAsync(_settings);
        App.Current.ApplyTheme(_settings.Theme);
        StatusMessage = "Settings saved.";
    }

    [RelayCommand]
    public async Task ExportPackageListAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var packages = await _packageService.GetInstalledPackagesAsync();
            var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var exportDirectory = Path.Combine(documents, "AppFlow", "Exports");
            Directory.CreateDirectory(exportDirectory);
            var path = Path.Combine(exportDirectory, $"packages-{DateTime.Now:yyyyMMdd-HHmmss}.json");
            var json = JsonSerializer.Serialize(packages, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            await File.WriteAllTextAsync(path, json);
            StatusMessage = $"Package list exported to {path}";
        }
        catch (IOException)
        {
            StatusMessage = "The package list could not be written to Documents.";
        }
        catch (UnauthorizedAccessException)
        {
            StatusMessage = "AppFlow does not have permission to write the export file.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
