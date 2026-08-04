namespace AppFlow.UI.ViewModels;

using AppFlow.Core.Enums;
using AppFlow.Core.Interfaces;
using AppFlow.Core.Models;
using AppFlow.Core.Services;
using AppFlow.Database.Repositories;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

public partial class AppDetailViewModel : ObservableObject
{
    private readonly SourceResolver _resolver;
    private readonly IActionEngine _actionEngine;
    private readonly FavoritesRepository _favoritesRepository;
    private readonly InstallRecordRepository _installRecordRepository;
    private InstallRecord? _installRecord;
    private CancellationTokenSource? _actionCts;

    [ObservableProperty] private PackageDetail _packageDetail = new();
    [ObservableProperty] private SourceQueryResult? _selectedSourceOption;
    [ObservableProperty] private bool _isExecuting;
    [ObservableProperty] private bool _isFavorite;
    [ObservableProperty] private bool _isInstalled;
    [ObservableProperty] private bool _hasUpdate;
    [ObservableProperty] private string _statusMessage = "Loading package details...";

    public ObservableCollection<SourceQueryResult> SourceOptions { get; } = new();
    public ObservableCollection<string> LogOutput { get; } = new();

    /// <summary>Assigned by the Page so the ViewModel remains testable and UI-framework independent.</summary>
    public Func<string, Task<bool>>? RequestConfirmationAsync { get; set; }

    public AppDetailViewModel(
        SourceResolver resolver,
        IActionEngine actionEngine,
        FavoritesRepository favoritesRepository,
        InstallRecordRepository installRecordRepository)
    {
        _resolver = resolver;
        _actionEngine = actionEngine;
        _favoritesRepository = favoritesRepository;
        _installRecordRepository = installRecordRepository;
    }

    partial void OnSelectedSourceOptionChanged(SourceQueryResult? value)
    {
        if (value is not null)
            ApplySource(value);
    }

    [RelayCommand]
    public async Task LoadPackageAsync(PackageNavigationRequest request)
    {
        StatusMessage = "Loading package details...";
        SourceOptions.Clear();
        LogOutput.Clear();

        var result = await _resolver.ResolveAsync(request.PackageId, request.SourceId);
        foreach (var option in result.AllOptions.Select(scored => scored.Result))
            SourceOptions.Add(option);

        if (result.BestMatch is null)
        {
            PackageDetail = new PackageDetail { Id = request.PackageId, Name = request.PackageId };
            StatusMessage = "This package is no longer available from any enabled source.";
            return;
        }

        _installRecord = await _installRecordRepository.GetByPackageIdAsync(request.PackageId);
        IsFavorite = await _favoritesRepository.IsFavoriteAsync(request.PackageId);

        var lockedOption = _installRecord is not null
            ? SourceOptions.FirstOrDefault(option => string.Equals(
                option.SourceId,
                _installRecord.LockedSource,
                StringComparison.OrdinalIgnoreCase))
            : null;
        SelectedSourceOption = lockedOption ?? result.BestMatch;
        StatusMessage = result.RecommendedSourceId == SelectedSourceOption.SourceId
            ? "Recommended source selected."
            : $"Selected source: {SelectedSourceOption.SourceId}.";
    }

    [RelayCommand]
    public Task InstallAsync() => ExecuteActionAsync(ActionType.Install);

    [RelayCommand]
    public Task UpdateAsync() => ExecuteActionAsync(ActionType.Update);

    [RelayCommand]
    public Task UninstallAsync() => ExecuteActionAsync(ActionType.Uninstall);

    [RelayCommand]
    public Task RepairAsync() => ExecuteActionAsync(ActionType.Repair);

    [RelayCommand]
    public void CancelAction() => _actionCts?.Cancel();

    [RelayCommand]
    public async Task ToggleFavoriteAsync()
    {
        if (string.IsNullOrWhiteSpace(PackageDetail.Id)) return;
        IsFavorite = await _favoritesRepository.ToggleAsync(PackageDetail.Id);
    }

    private async Task ExecuteActionAsync(ActionType type)
    {
        var source = SelectedSourceOption;
        if (source is null || IsExecuting) return;
        if (type == ActionType.Install && IsInstalled)
        {
            LogOutput.Add("> This package is already installed. Use Update or Uninstall.");
            return;
        }

        IsExecuting = true;
        _actionCts?.Dispose();
        _actionCts = new CancellationTokenSource();
        var cancellationToken = _actionCts.Token;
        LogOutput.Clear();
        StatusMessage = $"{type} in progress...";
        var progress = new Progress<string>(message => LogOutput.Add($"> {message}"));
        var action = new PackageAction
        {
            Type = type,
            PackageId = PackageDetail.Id,
            PackageName = PackageDetail.Name,
            SourceId = source.SourceId,
            SupportsSilent = PackageDetail.SupportsSilent
        };

        try
        {
            var result = await _actionEngine.ValidateAndExecuteAsync(
                action, source, progress, cancellationToken);
            if (result.RequiresConfirmation)
            {
                var accepted = RequestConfirmationAsync is not null
                               && await RequestConfirmationAsync(
                                   result.ConfirmationMessage ?? "Continue with this package?");
                if (!accepted)
                {
                    LogOutput.Add("> Operation cancelled because the security warning was not accepted.");
                    StatusMessage = "Operation cancelled.";
                    return;
                }

                action.UserConfirmedRisk = true;
                result = await _actionEngine.ValidateAndExecuteAsync(
                    action, source, progress, cancellationToken);
            }

            if (result.Success)
            {
                LogOutput.Add("> Operation completed successfully.");
                ApplySuccessfulAction(type);
                StatusMessage = "Operation completed successfully.";
            }
            else
            {
                LogOutput.Add($"> Operation failed: {result.ErrorMessage}");
                if (!string.IsNullOrWhiteSpace(result.ErrorSuggestion))
                    LogOutput.Add($"> Suggestion: {result.ErrorSuggestion}");
                StatusMessage = result.ErrorMessage ?? "Operation failed.";
            }
        }
        catch (OperationCanceledException)
        {
            LogOutput.Add("> Operation cancelled.");
            StatusMessage = "Operation cancelled.";
        }
        catch (Exception)
        {
            LogOutput.Add("> An unexpected error occurred. Check the AppFlow log for details.");
            StatusMessage = "Operation failed unexpectedly.";
        }
        finally
        {
            _actionCts?.Dispose();
            _actionCts = null;
            IsExecuting = false;
        }
    }

    private void ApplySource(SourceQueryResult source)
    {
        var detail = source.Detail ?? new PackageDetail
        {
            Id = source.PackageId,
            Name = source.PackageName,
            Publisher = source.Publisher,
            Description = source.Description ?? string.Empty,
            LatestVersion = source.Version,
            SourceId = source.SourceId,
            SourceTrustScore = source.SourceTrustScore,
            IsSigned = source.IsSigned,
            IsOfficialSource = source.IsOfficialSource,
            SupportsSilent = source.SupportsSilent,
            DownloadUrl = source.DownloadUrl,
            ExpectedHash = source.ExpectedHash,
            LocalInstallerPath = source.LocalInstallerPath
        };

        detail.AvailableSources = SourceOptions.Select(option => option.SourceId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        detail.IsInstalled = _installRecord is not null;
        detail.InstalledVersion = _installRecord?.InstalledVersion;
        PackageDetail = detail;
        IsInstalled = detail.IsInstalled;
        HasUpdate = detail.HasUpdate;
    }

    private void ApplySuccessfulAction(ActionType type)
    {
        switch (type)
        {
            case ActionType.Install:
            case ActionType.Update:
                IsInstalled = true;
                HasUpdate = false;
                PackageDetail.IsInstalled = true;
                PackageDetail.InstalledVersion = PackageDetail.LatestVersion;
                break;
            case ActionType.Uninstall:
                IsInstalled = false;
                HasUpdate = false;
                PackageDetail.IsInstalled = false;
                PackageDetail.InstalledVersion = null;
                break;
        }
        OnPropertyChanged(nameof(PackageDetail));
    }
}
