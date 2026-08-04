namespace AppFlow.Core.Services;

using AppFlow.Core.Enums;
using AppFlow.Core.Interfaces;
using AppFlow.Core.Models;
using System.Collections.Concurrent;
using System.ComponentModel;

public sealed class ActionEngine : IActionEngine
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> PackageLocks =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly IReadOnlyDictionary<string, ISourceAdapter> _adapters;
    private readonly ISecurityValidator _validator;
    private readonly IInstallRecordStore _installRecords;
    private readonly IActionHistoryStore _history;
    private readonly ILogService _log;
    private readonly SourceRegistry _sourceRegistry;

    public ActionEngine(
        IEnumerable<ISourceAdapter> adapters,
        ISecurityValidator validator,
        IInstallRecordStore installRecords,
        IActionHistoryStore history,
        ILogService log,
        SourceRegistry sourceRegistry)
    {
        _adapters = adapters.ToDictionary(a => a.SourceId, StringComparer.OrdinalIgnoreCase);
        _validator = validator;
        _installRecords = installRecords;
        _history = history;
        _log = log;
        _sourceRegistry = sourceRegistry;
    }

    public Task<ActionResult> ExecuteAsync(
        PackageAction action,
        IProgress<string> progress,
        CancellationToken ct = default) =>
        RunWorkflowAsync(action, source: null, progress, ct);

    public Task<ActionResult> ValidateAndExecuteAsync(
        PackageAction action,
        SourceQueryResult source,
        IProgress<string> progress,
        CancellationToken ct = default) =>
        RunWorkflowAsync(action, source, progress, ct);

    private async Task<ActionResult> RunWorkflowAsync(
        PackageAction requestedAction,
        SourceQueryResult? suppliedSource,
        IProgress<string> progress,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(requestedAction);
        if (string.IsNullOrWhiteSpace(requestedAction.PackageId))
            return await CompleteAsync(Failure(requestedAction, "A package ID is required."), requestedAction.PackageName, ct);

        var packageLock = PackageLocks.GetOrAdd(requestedAction.PackageId, _ => new SemaphoreSlim(1, 1));
        await packageLock.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            var action = Clone(requestedAction);
            var record = await _installRecords.GetByPackageIdAsync(action.PackageId, ct).ConfigureAwait(false);

            if (action.Type == ActionType.Install && record is not null)
            {
                return await CompleteAsync(
                    Failure(
                        action,
                        "This package is already managed by AppFlow.",
                        $"Use Update or Uninstall. Its locked source is {record.LockedSource}."),
                    action.PackageName,
                    ct).ConfigureAwait(false);
            }

            if (record is not null
                && action.Type is ActionType.Update or ActionType.Uninstall or ActionType.Repair
                && !string.Equals(action.SourceId, record.LockedSource, StringComparison.OrdinalIgnoreCase))
            {
                progress.Report($"Source lock: routing this action through {record.LockedSource}.");
                action.SourceId = record.LockedSource;
                suppliedSource = null;
            }

            if (!_adapters.TryGetValue(action.SourceId, out var adapter))
            {
                return await CompleteAsync(
                    Failure(action, "The selected package source is not registered."),
                    action.PackageName,
                    ct).ConfigureAwait(false);
            }

            if (!_sourceRegistry.IsEnabled(action.SourceId))
            {
                return await CompleteAsync(
                    Failure(action, "The selected package source is disabled.", "Enable it on the Sources page and try again."),
                    action.PackageName,
                    ct).ConfigureAwait(false);
            }

            var adapterAvailable = await Task.Run(() => adapter.IsAvailable, ct).ConfigureAwait(false);
            if (!adapterAvailable)
            {
                return await CompleteAsync(
                    Failure(action, $"{adapter.DisplayName} is not available on this computer."),
                    action.PackageName,
                    ct).ConfigureAwait(false);
            }

            if (action.Type == ActionType.Install && record is null)
            {
                var existingPackage = await FindExistingPackageSafeAsync(adapter, action.PackageId, ct)
                    .ConfigureAwait(false);
                if (existingPackage is not null)
                {
                    await _installRecords.SaveAsync(new InstallRecord
                    {
                        PackageId = action.PackageId,
                        InstalledFrom = action.SourceId,
                        InstalledVersion = existingPackage.InstalledVersion ?? "unknown",
                        LockedSource = action.SourceId,
                        InstalledAt = DateTime.UtcNow
                    }, ct).ConfigureAwait(false);

                    return await CompleteAsync(
                        Failure(
                            action,
                            "This package is already installed.",
                            "AppFlow imported the existing installation and locked it to its detected source."),
                        action.PackageName,
                        ct).ConfigureAwait(false);
                }
            }

            SourceQueryResult? source = suppliedSource;
            if (RequiresInstallerValidation(action.Type))
            {
                if (source is null
                    || !string.Equals(source.SourceId, action.SourceId, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(source.PackageId, action.PackageId, StringComparison.OrdinalIgnoreCase))
                {
                    var detail = await adapter.GetDetailsAsync(action.PackageId, ct).ConfigureAwait(false);
                    if (detail is null)
                    {
                        return await CompleteAsync(
                            Failure(action, "Package details could not be found in the selected source."),
                            action.PackageName,
                            ct).ConfigureAwait(false);
                    }
                    source = ToSourceResult(detail, adapter);
                }

                var validation = await _validator.ValidateAsync(action, source, ct).ConfigureAwait(false);
                foreach (var warning in validation.Warnings)
                    progress.Report($"Security warning: {warning}");

                if (!validation.IsValid)
                {
                    return await CompleteAsync(
                        Failure(
                            action,
                            "The operation was blocked by the security policy.",
                            string.Join(" ", validation.Errors)),
                        action.PackageName,
                        ct).ConfigureAwait(false);
                }

                if (validation.RequiresUserConfirmation && !action.UserConfirmedRisk)
                {
                    return new ActionResult
                    {
                        Success = false,
                        RequiresConfirmation = true,
                        ConfirmationMessage = validation.ConfirmationMessage,
                        ErrorMessage = "Your confirmation is required before continuing.",
                        ActionPerformed = action.Type,
                        PackageId = action.PackageId,
                        SourceUsed = action.SourceId
                    };
                }
            }

            var result = await ExecuteAdapterAsync(adapter, action, progress, ct).ConfigureAwait(false);
            result.PackageId = action.PackageId;
            result.SourceUsed = action.SourceId;
            result.ActionPerformed = action.Type;

            if (result.Success)
            {
                await UpdateInstallRecordAsync(action, source, record, result, ct).ConfigureAwait(false);
            }

            return await CompleteAsync(result, action.PackageName, ct).ConfigureAwait(false);
        }
        finally
        {
            packageLock.Release();
        }
    }

    private static bool RequiresInstallerValidation(ActionType type) =>
        type is ActionType.Install or ActionType.Update or ActionType.Repair;

    private async Task<PackageInfo?> FindExistingPackageSafeAsync(
        ISourceAdapter adapter,
        string packageId,
        CancellationToken ct)
    {
        try
        {
            var installed = await adapter.GetInstalledPackagesAsync(ct).ConfigureAwait(false)
                            ?? new List<PackageInfo>();
            return installed.FirstOrDefault(package => string.Equals(
                package.Id,
                packageId,
                StringComparison.OrdinalIgnoreCase));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _log.LogWarning($"Could not probe installed packages through {adapter.SourceId}: {ex.Message}");
            return null;
        }
    }

    private async Task<ActionResult> ExecuteAdapterAsync(
        ISourceAdapter adapter,
        PackageAction action,
        IProgress<string> progress,
        CancellationToken ct)
    {
        try
        {
            return action.Type switch
            {
                ActionType.Install => await adapter.InstallAsync(action, progress, ct).ConfigureAwait(false),
                ActionType.Update => await adapter.UpdateAsync(action, progress, ct).ConfigureAwait(false),
                ActionType.Uninstall => await adapter.UninstallAsync(action, progress, ct).ConfigureAwait(false),
                ActionType.Repair => await adapter.RepairAsync(action, progress, ct).ConfigureAwait(false),
                _ => Failure(action, "This action is not supported.")
            };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            var cancelled = Failure(action, "The operation was cancelled.");
            cancelled.WasCancelled = true;
            return cancelled;
        }
        catch (OperationCanceledException)
        {
            return Failure(action, "The operation was cancelled.");
        }
        catch (TimeoutException)
        {
            return Failure(action, "The package-manager command timed out.", "Check the network and try again.");
        }
        catch (UnauthorizedAccessException)
        {
            return Failure(action, "Permission was denied.", "Restart AppFlow as administrator and try again.");
        }
        catch (Win32Exception)
        {
            return Failure(action, "Windows could not start the package manager.", "Verify that the selected source is installed correctly.");
        }
        catch (HttpRequestException)
        {
            return Failure(action, "The package source could not be reached.", "Check your internet connection and try again.");
        }
        catch (IOException)
        {
            return Failure(action, "A required installer file could not be read or written.");
        }
        catch (InvalidOperationException)
        {
            return Failure(action, "The package manager returned an invalid operation state.");
        }
        catch (Exception ex)
        {
            _log.LogError("Unexpected source-adapter failure.", ex);
            return Failure(
                action,
                "The package manager failed unexpectedly.",
                "Review the AppFlow log and try again.");
        }
    }

    private async Task UpdateInstallRecordAsync(
        PackageAction action,
        SourceQueryResult? source,
        InstallRecord? existing,
        ActionResult result,
        CancellationToken ct)
    {
        try
        {
            if (action.Type == ActionType.Uninstall)
            {
                await _installRecords.DeleteAsync(action.PackageId, ct).ConfigureAwait(false);
                return;
            }

            if (action.Type is ActionType.Install or ActionType.Update)
            {
                var version = action.TargetVersion
                              ?? source?.Version
                              ?? existing?.InstalledVersion
                              ?? "unknown";

                await _installRecords.SaveAsync(new InstallRecord
                {
                    PackageId = action.PackageId,
                    InstalledFrom = existing?.InstalledFrom ?? action.SourceId,
                    InstalledVersion = version,
                    LockedSource = existing?.LockedSource ?? action.SourceId,
                    InstalledAt = existing?.InstalledAt ?? DateTime.UtcNow,
                    UserOverriddenSource = existing?.UserOverriddenSource ?? false
                }, ct).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            result.LogOutput += Environment.NewLine +
                                "Warning: the operation completed, but AppFlow could not update its local install record.";
            _log.LogError("Failed to update the install record.", ex);
        }
    }

    private async Task<ActionResult> CompleteAsync(
        ActionResult result,
        string packageName,
        CancellationToken ct)
    {
        _log.LogAction(result);
        try
        {
            await _history.LogActionAsync(
                result,
                string.IsNullOrWhiteSpace(packageName) ? result.PackageId : packageName,
                ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _log.LogError("Failed to persist action history.", ex);
        }
        return result;
    }

    private static SourceQueryResult ToSourceResult(PackageDetail detail, ISourceAdapter adapter) =>
        new()
        {
            PackageId = detail.Id,
            SourceId = adapter.SourceId,
            PackageName = string.IsNullOrWhiteSpace(detail.Name) ? detail.Id : detail.Name,
            Publisher = detail.Publisher,
            Version = detail.LatestVersion,
            IsSigned = detail.IsSigned,
            IsOfficialSource = detail.IsOfficialSource,
            IsLatestVersion = !string.IsNullOrWhiteSpace(detail.LatestVersion),
            IsTrustedPublisher = detail.IsTrustedPublisher,
            SupportsSilent = detail.SupportsSilent,
            SourceTrustScore = adapter.TrustScore,
            DownloadUrl = detail.DownloadUrl,
            ExpectedHash = detail.ExpectedHash,
            LocalInstallerPath = detail.LocalInstallerPath,
            Description = detail.Description,
            Detail = detail
        };

    private static PackageAction Clone(PackageAction action) => new()
    {
        Type = action.Type,
        PackageId = action.PackageId,
        PackageName = action.PackageName,
        SourceId = action.SourceId,
        RequiresAdmin = action.RequiresAdmin,
        SupportsSilent = action.SupportsSilent,
        SupportsPortable = action.SupportsPortable,
        CustomArgs = action.CustomArgs,
        TargetVersion = action.TargetVersion,
        UserConfirmedRisk = action.UserConfirmedRisk
    };

    private static ActionResult Failure(
        PackageAction action,
        string message,
        string? suggestion = null) => new()
    {
        Success = false,
        ExitCode = -1,
        ErrorMessage = message,
        ErrorSuggestion = suggestion,
        ActionPerformed = action.Type,
        PackageId = action.PackageId,
        SourceUsed = action.SourceId
    };
}
