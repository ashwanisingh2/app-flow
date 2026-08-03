namespace AppFlow.Sources;

using AppFlow.Core.Enums;
using AppFlow.Core.Interfaces;
using AppFlow.Core.Models;
using System.Diagnostics;
using System.IO;

public class OfflineAdapter : ISourceAdapter
{
    public string SourceId => "offline";
    public string DisplayName => "Local Installers (Downloads)";
    public int TrustScore => 1;
    public bool IsAvailable => Directory.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"));

    private string DownloadsPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

    public Task<List<PackageInfo>> SearchAsync(string query, CancellationToken ct = default)
    {
        var packages = new List<PackageInfo>();
        if (!IsAvailable) return Task.FromResult(packages);

        var files = Directory.GetFiles(DownloadsPath, "*.*", SearchOption.TopDirectoryOnly)
                             .Where(f => f.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || 
                                         f.EndsWith(".msi", StringComparison.OrdinalIgnoreCase))
                             .Where(f => Path.GetFileName(f).Contains(query, StringComparison.OrdinalIgnoreCase));

        foreach (var file in files)
        {
            var name = Path.GetFileName(file);
            packages.Add(new PackageInfo
            {
                Id = file, // Use path as ID
                Name = name,
                LatestVersion = "Local",
                AvailableSources = new List<string> { SourceId },
                TrustLevel = TrustLevel.Unknown
            });
        }
        return Task.FromResult(packages);
    }

    public Task<PackageDetail> GetDetailsAsync(string packageId, CancellationToken ct = default)
    {
        var detail = new PackageDetail { Id = packageId, SourceId = SourceId, SourceTrustScore = TrustScore };
        if (File.Exists(packageId))
        {
            var info = FileVersionInfo.GetVersionInfo(packageId);
            detail.Name = Path.GetFileName(packageId);
            detail.LatestVersion = info.FileVersion ?? "Local";
            detail.Publisher = info.CompanyName ?? "Unknown";
            detail.Description = info.FileDescription ?? "Local installer file";
        }
        return Task.FromResult(detail);
    }

    public async Task<ActionResult> InstallAsync(PackageAction action, IProgress<string> progress, CancellationToken ct = default)
    {
        if (!File.Exists(action.PackageId))
            return new ActionResult { Success = false, ErrorMessage = "File not found.", ActionPerformed = ActionType.Install, PackageId = action.PackageId, SourceUsed = SourceId };

        try
        {
            progress?.Report($"Launching {action.PackageId}...");
            var psi = new ProcessStartInfo { FileName = action.PackageId, UseShellExecute = true };
            if (action.SupportsSilent) psi.Arguments = "/S /quiet";
            
            using var process = Process.Start(psi);
            if (process != null) await process.WaitForExitAsync(ct);

            return new ActionResult { Success = true, LogOutput = "Installer launched.", ActionPerformed = ActionType.Install, PackageId = action.PackageId, SourceUsed = SourceId };
        }
        catch (Exception ex)
        {
            return new ActionResult { Success = false, ErrorMessage = ex.Message, ActionPerformed = ActionType.Install, PackageId = action.PackageId, SourceUsed = SourceId };
        }
    }

    public Task<ActionResult> UpdateAsync(PackageAction action, IProgress<string> progress, CancellationToken ct = default)
        => InstallAsync(action, progress, ct);

    public Task<ActionResult> UninstallAsync(PackageAction action, IProgress<string> progress, CancellationToken ct = default)
        => Task.FromResult(new ActionResult { Success = false, ErrorMessage = "Not supported", ActionPerformed = ActionType.Uninstall, PackageId = action.PackageId, SourceUsed = SourceId });

    public Task<ActionResult> RepairAsync(PackageAction action, IProgress<string> progress, CancellationToken ct = default)
        => InstallAsync(action, progress, ct);

    public Task<List<PackageInfo>> GetInstalledPackagesAsync(CancellationToken ct = default)
        => Task.FromResult(new List<PackageInfo>());
}
