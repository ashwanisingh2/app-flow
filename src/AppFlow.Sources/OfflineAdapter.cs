namespace AppFlow.Sources;

using AppFlow.Core.Enums;
using AppFlow.Core.Interfaces;
using AppFlow.Core.Models;
using System.Diagnostics;

public sealed class OfflineAdapter : ISourceAdapter
{
    private readonly IInstallerIntegrityVerifier _integrityVerifier;

    public OfflineAdapter(IInstallerIntegrityVerifier integrityVerifier) =>
        _integrityVerifier = integrityVerifier;

    public string SourceId => "offline";
    public string DisplayName => "Local Installers (Downloads)";
    public int TrustScore => 1;
    public bool IsAvailable => Directory.Exists(DownloadsPath);

    private static string DownloadsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Downloads");

    public Task<List<PackageInfo>> SearchAsync(string query, CancellationToken ct = default)
    {
        var packages = new List<PackageInfo>();
        if (!IsAvailable) return Task.FromResult(packages);

        try
        {
            foreach (var file in Directory.EnumerateFiles(DownloadsPath, "*", SearchOption.TopDirectoryOnly))
            {
                ct.ThrowIfCancellationRequested();
                if (!IsInstaller(file)
                    || !Path.GetFileName(file).Contains(query, StringComparison.OrdinalIgnoreCase))
                    continue;

                var name = Path.GetFileName(file);
                packages.Add(new PackageInfo
                {
                    Id = file,
                    Name = name,
                    LatestVersion = "Local",
                    SourceId = SourceId,
                    AvailableSources = new List<string> { SourceId },
                    SourcePackageIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        [SourceId] = file
                    },
                    TrustLevel = TrustLevel.Unknown,
                    IsSigned = _integrityVerifier.HasTrustedAuthenticodeSignature(file),
                    SupportedActions = new List<ActionType> { ActionType.Install }
                });
            }
        }
        catch (DirectoryNotFoundException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        return Task.FromResult(packages);
    }

    public Task<PackageDetail?> GetDetailsAsync(string packageId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (!File.Exists(packageId) || !IsInstaller(packageId))
            return Task.FromResult<PackageDetail?>(null);

        var info = FileVersionInfo.GetVersionInfo(packageId);
        var signatureTrusted = _integrityVerifier.HasTrustedAuthenticodeSignature(packageId);
        return Task.FromResult<PackageDetail?>(new PackageDetail
        {
            Id = packageId,
            Name = Path.GetFileName(packageId),
            LatestVersion = info.FileVersion ?? "Local",
            Publisher = info.CompanyName ?? "Unknown",
            Description = info.FileDescription ?? "Local installer file",
            SourceId = SourceId,
            SourceTrustScore = TrustScore,
            TrustLevel = TrustLevel.Unknown,
            IsSigned = signatureTrusted,
            IsTrustedPublisher = signatureTrusted,
            LocalInstallerPath = packageId,
            SupportsSilent = packageId.EndsWith(".msi", StringComparison.OrdinalIgnoreCase),
            AvailableSources = new List<string> { SourceId }
        });
    }

    public Task<ActionResult> InstallAsync(
        PackageAction action,
        IProgress<string> progress,
        CancellationToken ct = default) =>
        RunInstallerAsync(action, ActionType.Install, progress, ct);

    public Task<ActionResult> UpdateAsync(
        PackageAction action,
        IProgress<string> progress,
        CancellationToken ct = default) =>
        RunInstallerAsync(action, ActionType.Update, progress, ct);

    public Task<ActionResult> UninstallAsync(
        PackageAction action,
        IProgress<string> progress,
        CancellationToken ct = default) =>
        Task.FromResult(Failure(
            action,
            ActionType.Uninstall,
            "A local installer cannot determine the application's uninstaller.",
            "Use Windows Settings or the source that originally installed the package."));

    public Task<ActionResult> RepairAsync(
        PackageAction action,
        IProgress<string> progress,
        CancellationToken ct = default) =>
        RunInstallerAsync(action, ActionType.Repair, progress, ct);

    public Task<List<PackageInfo>> GetInstalledPackagesAsync(CancellationToken ct = default) =>
        Task.FromResult(new List<PackageInfo>());

    private async Task<ActionResult> RunInstallerAsync(
        PackageAction action,
        ActionType type,
        IProgress<string> progress,
        CancellationToken ct)
    {
        if (!File.Exists(action.PackageId) || !IsInstaller(action.PackageId))
            return Failure(action, type, "The local installer file was not found.");

        var trusted = _integrityVerifier.HasTrustedAuthenticodeSignature(action.PackageId);
        if (!trusted && !action.UserConfirmedRisk)
        {
            return Failure(
                action,
                type,
                "The local installer does not have a trusted Authenticode signature.",
                "Verify its publisher before continuing.");
        }

        progress.Report(trusted
            ? "Authenticode signature verified."
            : "Warning: continuing with the unsigned installer after confirmation.");
        progress.Report($"Starting {Path.GetFileName(action.PackageId)}...");

        var startInfo = CreateStartInfo(action.PackageId, action.SupportsSilent);
        using var process = Process.Start(startInfo);
        if (process is null)
            return Failure(action, type, "Windows could not start the installer.");

        await process.WaitForExitAsync(ct).ConfigureAwait(false);
        var success = process.ExitCode is 0 or 1641 or 3010;
        return new ActionResult
        {
            Success = success,
            ExitCode = process.ExitCode,
            LogOutput = success ? "Installer completed successfully." : "Installer returned a failure exit code.",
            ErrorMessage = success ? null : "The installer did not complete successfully.",
            ErrorSuggestion = success ? null : "Review the installer UI or run AppFlow as administrator.",
            ActionPerformed = type,
            PackageId = action.PackageId,
            SourceUsed = SourceId
        };
    }

    private static ProcessStartInfo CreateStartInfo(string installerPath, bool silent)
    {
        if (installerPath.EndsWith(".msi", StringComparison.OrdinalIgnoreCase))
        {
            var msi = new ProcessStartInfo { FileName = "msiexec.exe", UseShellExecute = true };
            msi.ArgumentList.Add("/i");
            msi.ArgumentList.Add(installerPath);
            if (silent)
            {
                msi.ArgumentList.Add("/qn");
                msi.ArgumentList.Add("/norestart");
            }
            return msi;
        }

        var executable = new ProcessStartInfo { FileName = installerPath, UseShellExecute = true };
        if (silent) executable.ArgumentList.Add("/S");
        return executable;
    }

    private static bool IsInstaller(string path) =>
        path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".msi", StringComparison.OrdinalIgnoreCase);

    private static ActionResult Failure(
        PackageAction action,
        ActionType type,
        string message,
        string? suggestion = null) => new()
    {
        Success = false,
        ExitCode = -1,
        ErrorMessage = message,
        ErrorSuggestion = suggestion,
        ActionPerformed = type,
        PackageId = action.PackageId,
        SourceUsed = "offline"
    };
}
