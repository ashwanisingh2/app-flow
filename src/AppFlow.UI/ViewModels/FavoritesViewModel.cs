namespace AppFlow.UI.ViewModels;

using AppFlow.Core.Interfaces;
using AppFlow.Core.Models;
using AppFlow.Database.Repositories;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

public partial class FavoritesViewModel : ObservableObject
{
    private readonly FavoritesRepository _favoritesRepository;
    private readonly IPackageService _packageService;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _statusMessage = string.Empty;

    public ObservableCollection<PackageInfo> Favorites { get; } = new();

    public FavoritesViewModel(
        FavoritesRepository favoritesRepository,
        IPackageService packageService)
    {
        _favoritesRepository = favoritesRepository;
        _packageService = packageService;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var ids = await _favoritesRepository.GetAllAsync();
            var detailTasks = ids.Select(id => LoadFavoriteSafeAsync(id));
            var packages = await Task.WhenAll(detailTasks);

            Favorites.Clear();
            foreach (var package in packages.Where(p => p is not null))
                Favorites.Add(package!);
            StatusMessage = Favorites.Count == 0
                ? "Packages you star will appear here."
                : $"{Favorites.Count} favorite package(s).";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task RemoveAsync(PackageInfo package)
    {
        await _favoritesRepository.RemoveAsync(package.Id);
        Favorites.Remove(package);
        StatusMessage = Favorites.Count == 0
            ? "Packages you star will appear here."
            : $"{Favorites.Count} favorite package(s).";
    }

    private async Task<PackageInfo?> LoadFavoriteSafeAsync(string id)
    {
        try
        {
            var detail = await _packageService.GetPackageDetailAsync(id);
            if (detail is null)
            {
                return new PackageInfo
                {
                    Id = id,
                    Name = id,
                    IsFavorite = true,
                    SourceId = "unavailable"
                };
            }

            return new PackageInfo
            {
                Id = detail.Id,
                Name = detail.Name,
                Publisher = detail.Publisher,
                LatestVersion = detail.LatestVersion,
                InstalledVersion = detail.InstalledVersion,
                IsInstalled = detail.IsInstalled,
                IsSigned = detail.IsSigned,
                TrustLevel = detail.TrustLevel,
                SourceId = detail.SourceId,
                AvailableSources = detail.AvailableSources,
                IsFavorite = true
            };
        }
        catch (Exception)
        {
            return new PackageInfo { Id = id, Name = id, IsFavorite = true, SourceId = "unavailable" };
        }
    }
}
