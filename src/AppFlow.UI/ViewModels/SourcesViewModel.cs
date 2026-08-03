namespace AppFlow.UI.ViewModels;

using AppFlow.Core.Interfaces;
using AppFlow.Core.Services;
using AppFlow.Database.Repositories;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

public partial class SourceItemViewModel : ObservableObject
{
    public string SourceId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public int TrustScore { get; init; }
    public bool IsAvailable { get; init; }
    [ObservableProperty] private bool _isEnabled;

    public string AvailabilityText => IsAvailable ? "Available" : "Not installed or unavailable";
    public string TrustText => $"Trust score: {TrustScore}/5";
}

public partial class SourcesViewModel : ObservableObject
{
    private readonly IReadOnlyList<ISourceAdapter> _adapters;
    private readonly SettingsRepository _settingsRepository;
    private readonly SourceRegistry _sourceRegistry;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _statusMessage = string.Empty;

    public ObservableCollection<SourceItemViewModel> Sources { get; } = new();

    public SourcesViewModel(
        IEnumerable<ISourceAdapter> adapters,
        SettingsRepository settingsRepository,
        SourceRegistry sourceRegistry)
    {
        _adapters = adapters.ToList();
        _settingsRepository = settingsRepository;
        _sourceRegistry = sourceRegistry;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        Sources.Clear();
        var tasks = _adapters.Select(async adapter =>
        {
            var enabledTask = _settingsRepository.GetSourceEnabledAsync(adapter.SourceId);
            var availabilityTask = Task.Run(() => adapter.IsAvailable);
            await Task.WhenAll(enabledTask, availabilityTask);
            return new SourceItemViewModel
            {
                SourceId = adapter.SourceId,
                DisplayName = adapter.DisplayName,
                TrustScore = adapter.TrustScore,
                IsAvailable = await availabilityTask,
                IsEnabled = await enabledTask
            };
        });
        var items = await Task.WhenAll(tasks);
        foreach (var item in items.OrderByDescending(item => item.TrustScore))
        {
            _sourceRegistry.SetEnabled(item.SourceId, item.IsEnabled);
            Sources.Add(item);
        }
        IsLoading = false;
        StatusMessage = "Source preferences are saved automatically.";
    }

    [RelayCommand]
    public async Task UpdateSourceAsync(SourceItemViewModel source)
    {
        _sourceRegistry.SetEnabled(source.SourceId, source.IsEnabled);
        await _settingsRepository.SetSourceEnabledAsync(source.SourceId, source.IsEnabled);
        StatusMessage = source.IsEnabled
            ? $"{source.DisplayName} enabled."
            : $"{source.DisplayName} disabled.";
    }
}
