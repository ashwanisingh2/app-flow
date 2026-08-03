namespace AppFlow.Core.Interfaces;

using AppFlow.Core.Models;

public interface ISourceAdapter
{
    string SourceId { get; }
    string DisplayName { get; }
    int TrustScore { get; }
    bool IsAvailable { get; }

    Task<List<PackageInfo>> SearchAsync(string query, CancellationToken ct = default);
    Task<PackageDetail> GetDetailsAsync(string packageId, CancellationToken ct = default);
    Task<ActionResult> InstallAsync(PackageAction action, IProgress<string> progress, CancellationToken ct = default);
    Task<ActionResult> UpdateAsync(PackageAction action, IProgress<string> progress, CancellationToken ct = default);
    Task<ActionResult> UninstallAsync(PackageAction action, IProgress<string> progress, CancellationToken ct = default);
    Task<ActionResult> RepairAsync(PackageAction action, IProgress<string> progress, CancellationToken ct = default);
    Task<List<PackageInfo>> GetInstalledPackagesAsync(CancellationToken ct = default);
}
