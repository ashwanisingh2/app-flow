namespace AppFlow.UI.ViewModels;

using AppFlow.Database.Repositories;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

public partial class HistoryViewModel : ObservableObject
{
    private readonly HistoryRepository _repository;
    private readonly List<ActionHistoryEntry> _allEntries = new();

    [ObservableProperty] private string _searchQuery = string.Empty;
    [ObservableProperty] private string _selectedAction = "All";
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _statusMessage = string.Empty;

    public ObservableCollection<ActionHistoryEntry> Entries { get; } = new();

    public HistoryViewModel(HistoryRepository repository) => _repository = repository;

    partial void OnSearchQueryChanged(string value) => ApplyFilters();
    partial void OnSelectedActionChanged(string value) => ApplyFilters();

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var entries = await _repository.GetRecentAsync(500);
            _allEntries.Clear();
            _allEntries.AddRange(entries);
            ApplyFilters();
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task ClearAsync()
    {
        await _repository.ClearAllAsync();
        _allEntries.Clear();
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        var filtered = _allEntries.Where(entry =>
            (SelectedAction == "All"
             || string.Equals(entry.ActionType, SelectedAction, StringComparison.OrdinalIgnoreCase))
            && (string.IsNullOrWhiteSpace(SearchQuery)
                || entry.PackageName.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase)
                || entry.PackageId.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase)));

        Entries.Clear();
        foreach (var entry in filtered)
            Entries.Add(entry);
        StatusMessage = Entries.Count == 0 ? "No matching action history." : $"{Entries.Count} action(s).";
    }
}
