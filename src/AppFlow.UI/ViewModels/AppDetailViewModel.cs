namespace AppFlow.UI.ViewModels;

using AppFlow.Core.Interfaces;
using AppFlow.Core.Models;
using AppFlow.Core.Services;
using AppFlow.Database.Repositories;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

public partial class AppDetailViewModel : ObservableObject
{
    private readonly SourceResolver _resolver;
    private readonly IActionEngine _actionEngine;
    private readonly FavoritesRepository _favoritesRepo;

    [ObservableProperty]
    private PackageDetail? _packageDetail;

    [ObservableProperty]
    private bool _isExecuting;

    [ObservableProperty]
    private bool _isFavorite;

    [ObservableProperty]
    private string _selectedSource = "winget";

    public ObservableCollection<string> LogOutput { get; } = new();

    public AppDetailViewModel(SourceResolver resolver, IActionEngine actionEngine, FavoritesRepository favoritesRepo)
    {
        _resolver = resolver;
        _actionEngine = actionEngine;
        _favoritesRepo = favoritesRepo;
    }

    [RelayCommand]
    public async Task LoadPackageAsync(string packageId)
    {
        var result = await _resolver.ResolveAsync(packageId);
        if (result.BestMatch != null)
        {
            PackageDetail = new PackageDetail
            {
                Id = result.BestMatch.PackageId,
                Name = result.BestMatch.PackageName,
                LatestVersion = result.BestMatch.Version,
                SourceId = result.BestMatch.SourceId,
                SourceTrustScore = result.BestMatch.SourceTrustScore,
                IsOfficialSource = result.BestMatch.IsOfficialSource,
                SupportsSilent = result.BestMatch.SupportsSilent,
                ExpectedHash = result.BestMatch.ExpectedHash,
                DownloadUrl = result.BestMatch.DownloadUrl,
                Description = result.BestMatch.Description ?? ""
            };
            SelectedSource = result.BestMatch.SourceId;
        }
        IsFavorite = await _favoritesRepo.IsFavoriteAsync(packageId);
    }

    [RelayCommand]
    public async Task InstallAsync()
    {
        if (PackageDetail == null) return;
        await ExecuteActionAsync(AppFlow.Core.Enums.ActionType.Install);
    }

    [RelayCommand]
    public async Task UpdateAsync()
    {
        if (PackageDetail == null) return;
        await ExecuteActionAsync(AppFlow.Core.Enums.ActionType.Update);
    }

    [RelayCommand]
    public async Task UninstallAsync()
    {
        if (PackageDetail == null) return;
        await ExecuteActionAsync(AppFlow.Core.Enums.ActionType.Uninstall);
    }

    [RelayCommand]
    public async Task ToggleFavoriteAsync()
    {
        if (PackageDetail == null) return;
        IsFavorite = await _favoritesRepo.ToggleAsync(PackageDetail.Id);
    }

    private async Task ExecuteActionAsync(AppFlow.Core.Enums.ActionType type)
    {
        IsExecuting = true;
        LogOutput.Clear();
        var progress = new Progress<string>(msg => LogOutput.Add($"> {msg}"));

        var action = new PackageAction
        {
            Type = type,
            PackageId = PackageDetail!.Id,
            PackageName = PackageDetail.Name,
            SourceId = SelectedSource,
            SupportsSilent = PackageDetail.SupportsSilent
        };

        var result = await _actionEngine.ExecuteAsync(action, progress);
        if (result.Success)
        {
            LogOutput.Add("> Operation completed successfully.");
        }
        else
        {
            LogOutput.Add($"> Operation failed: {result.ErrorMessage}");
            if (!string.IsNullOrEmpty(result.ErrorSuggestion))
            {
                LogOutput.Add($"> Suggestion: {result.ErrorSuggestion}");
            }
        }
        IsExecuting = false;
    }
}
