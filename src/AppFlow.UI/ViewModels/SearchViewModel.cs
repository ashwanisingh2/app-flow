namespace AppFlow.UI.ViewModels;

using AppFlow.Core.Interfaces;
using AppFlow.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

public partial class SearchViewModel : ObservableObject
{
    private readonly IPackageService _packageService;
    private CancellationTokenSource? _searchCts;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _selectedFilter = "All";

    [ObservableProperty]
    private bool _isSearching;

    public ObservableCollection<PackageInfo> SearchResults { get; } = new();

    public SearchViewModel(IPackageService packageService)
    {
        _packageService = packageService;
    }

    [RelayCommand]
    public async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            SearchResults.Clear();
            return;
        }

        IsSearching = true;
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();

        try
        {
            var results = await _packageService.SearchAllSourcesAsync(SearchQuery, _searchCts.Token);
            SearchResults.Clear();
            
            // Simple filtering simulation for now
            foreach (var result in results)
            {
                if (SelectedFilter == "Installed" && !result.IsInstalled) continue;
                if (SelectedFilter == "Updates" && !result.HasUpdate) continue;
                SearchResults.Add(result);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on cancellation
        }
        finally
        {
            IsSearching = false;
        }
    }

    [RelayCommand]
    public void ApplyFilter(string filter)
    {
        SelectedFilter = filter;
        SearchCommand.Execute(null);
    }
}
