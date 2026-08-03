namespace AppFlow.Core.Interfaces;

using AppFlow.Core.Models;

public interface IPackageService
{
    Task<List<PackageInfo>> SearchAllSourcesAsync(string query, CancellationToken ct = default);
    Task<PackageDetail?> GetPackageDetailAsync(string packageId, CancellationToken ct = default);
    Task<List<PackageInfo>> GetInstalledPackagesAsync(CancellationToken ct = default);
    Task<List<PackageInfo>> GetUpdatesAvailableAsync(CancellationToken ct = default);
}
