namespace AppFlow.UI.ViewModels;

using AppFlow.Core.Interfaces;
using AppFlow.Database.Repositories;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

public partial class DashboardViewModel : ObservableObject
{
    private readonly IPackageService _packageService;
    private readonly HistoryRepository _historyRepository;

    [ObservableProperty] private int _totalInstalled;
    [ObservableProperty] private int _updatesAvailable;
    [ObservableProperty] private int _recentActionsCount;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _errorMessage;

    public ObservableCollection<ActionHistoryEntry> RecentActions { get; } = new();

    public DashboardViewModel(
        IPackageService packageService,
        HistoryRepository historyRepository)
    {
        _packageService = packageService;
        _historyRepository = historyRepository;
    }

    [RelayCommand]
    public async Task LoadDataAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var installedTask = _packageService.GetInstalledPackagesAsync();
            var historyTask = _historyRepository.GetRecentAsync(10);
            await Task.WhenAll(installedTask, historyTask);

            var installed = await installedTask;
            var history = await historyTask;
            TotalInstalled = installed.Count;
            UpdatesAvailable = installed.Count(package => package.HasUpdate);
            RecentActionsCount = history.Count;

            RecentActions.Clear();
            foreach (var item in history)
                RecentActions.Add(item);
        }
        catch (Exception)
        {
            ErrorMessage = "Dashboard data could not be loaded. Check the configured package sources.";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
