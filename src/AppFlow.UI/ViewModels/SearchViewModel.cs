namespace AppFlow.UI.ViewModels;

using AppFlow.Core.Interfaces;
using AppFlow.Core.Models;
using AppFlow.Database.Repositories;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

public partial class SearchViewModel : ObservableObject
{
    private const string AllSources = "All Sources";
    private readonly IPackageService _packageService;
    private readonly FavoritesRepository _favoritesRepository;
    private readonly List<PackageInfo> _allResults = new();
    private CancellationTokenSource? _searchCts;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _selectedFilter = "All";

    [ObservableProperty]
    private string _selectedSource = AllSources;

    [ObservableProperty]
    private bool _isSearching;

    [ObservableProperty]
    private string _statusMessage = "Type an app name to search all enabled sources.";

    public ObservableCollection<PackageInfo> SearchResults { get; } = new();
    public ObservableCollection<string> SourceFilters { get; } = new() { AllSources };

    public SearchViewModel(
        IPackageService packageService,
        FavoritesRepository favoritesRepository)
    {
        _packageService = packageService;
        _favoritesRepository = favoritesRepository;
    }

    partial void OnSearchQueryChanged(string value) => _ = StartSearchAsync(debounce: true);
    partial void OnSelectedFilterChanged(string value) => ApplyCurrentFilters();
    partial void OnSelectedSourceChanged(string value) => ApplyCurrentFilters();

    [RelayCommand]
    public Task SearchAsync() => StartSearchAsync(debounce: false);

    [RelayCommand]
    public void ApplyFilter(string filter) => SelectedFilter = filter;

    private async Task StartSearchAsync(bool debounce)
    {
        var current = new CancellationTokenSource();
        var previous = Interlocked.Exchange(ref _searchCts, current);
        previous?.Cancel();
        previous?.Dispose();

        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            _allResults.Clear();
            SearchResults.Clear();
            StatusMessage = "Type an app name to search all enabled sources.";
            IsSearching = false;
            return;
        }

        try
        {
            if (debounce)
                await Task.Delay(300, current.Token);

            IsSearching = true;
            StatusMessage = "Searching...";
            var results = await _packageService.SearchAllSourcesAsync(SearchQuery, current.Token);
            var favorites = (await _favoritesRepository.GetAllAsync(current.Token))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (!ReferenceEquals(_searchCts, current)) return;

            _allResults.Clear();
            foreach (var result in results)
            {
                result.IsFavorite = favorites.Contains(result.Id);
                _allResults.Add(result);
            }

            RefreshSourceFilters();
            ApplyCurrentFilters();
        }
        catch (OperationCanceledException)
        {
            // A newer query replaced this one.
        }
        finally
        {
            if (ReferenceEquals(_searchCts, current))
                IsSearching = false;
        }
    }

    private void RefreshSourceFilters()
    {
        var previous = SelectedSource;
        SourceFilters.Clear();
        SourceFilters.Add(AllSources);
        foreach (var source in _allResults
                     .SelectMany(p => p.AvailableSources.Append(p.SourceId))
                     .Where(s => !string.IsNullOrWhiteSpace(s))
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(s => s))
        {
            SourceFilters.Add(source);
        }
        SelectedSource = SourceFilters.Contains(previous) ? previous : AllSources;
    }

    private void ApplyCurrentFilters()
    {
        var filtered = _allResults.Where(result =>
            (SelectedFilter switch
            {
                "Installed" => result.IsInstalled,
                "Updates" => result.HasUpdate,
                "Favorites" => result.IsFavorite,
                _ => true
            })
            && (SelectedSource == AllSources
                || result.AvailableSources.Contains(SelectedSource, StringComparer.OrdinalIgnoreCase)
                || string.Equals(result.SourceId, SelectedSource, StringComparison.OrdinalIgnoreCase)));

        SearchResults.Clear();
        foreach (var result in filtered)
            SearchResults.Add(result);

        StatusMessage = SearchResults.Count == 0
            ? "No packages matched the current search and filters."
            : $"{SearchResults.Count} package(s) found.";
    }
}
