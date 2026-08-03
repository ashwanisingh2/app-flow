namespace AppFlow.UI.ViewModels;

using AppFlow.Core.Interfaces;
using AppFlow.Database.Repositories;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

public partial class DashboardViewModel : ObservableObject
{
    private readonly IPackageService _packageService;
    private readonly HistoryRepository _historyRepo;

    [ObservableProperty]
    private int _totalInstalled;

    [ObservableProperty]
    private int _updatesAvailable;

    [ObservableProperty]
    private int _recentActionsCount;

    public ObservableCollection<ActionHistoryEntry> RecentActions { get; } = new();

    public DashboardViewModel(IPackageService packageService, HistoryRepository historyRepo)
    {
        _packageService = packageService;
        _historyRepo = historyRepo;
    }

    [RelayCommand]
    public async Task LoadDataAsync()
    {
        var installed = await _packageService.GetInstalledPackagesAsync();
        TotalInstalled = installed.Count;

        var updates = await _packageService.GetUpdatesAvailableAsync();
        UpdatesAvailable = updates.Count;

        var history = await _historyRepo.GetRecentAsync(10);
        RecentActionsCount = history.Count;

        RecentActions.Clear();
        foreach (var item in history)
        {
            RecentActions.Add(item);
        }
    }
}
