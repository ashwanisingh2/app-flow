namespace AppFlow.Core.Interfaces;

using AppFlow.Core.Models;

/// <summary>
/// Persistence contract used by the action engine to enforce source locking.
/// </summary>
public interface IInstallRecordStore
{
    Task<InstallRecord?> GetByPackageIdAsync(string packageId, CancellationToken ct = default);
    Task SaveAsync(InstallRecord record, CancellationToken ct = default);
    Task DeleteAsync(string packageId, CancellationToken ct = default);
}
