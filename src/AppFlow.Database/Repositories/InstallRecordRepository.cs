namespace AppFlow.Database.Repositories;

using AppFlow.Core.Models;
using Dapper;

public class InstallRecordRepository
{
    private readonly AppFlowDb _db;

    public InstallRecordRepository(AppFlowDb db)
    {
        _db = db;
    }

    public async Task<InstallRecord?> GetByPackageIdAsync(string packageId)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<InstallRecord>(
            "SELECT * FROM InstallRecords WHERE PackageId = @PackageId", 
            new { PackageId = packageId });
    }

    public async Task SaveAsync(InstallRecord record)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(@"
            INSERT OR REPLACE INTO InstallRecords 
            (PackageId, InstalledFrom, InstalledVersion, LockedSource, InstalledAt, UserOverridden)
            VALUES (@PackageId, @InstalledFrom, @InstalledVersion, @LockedSource, @InstalledAt, @UserOverriddenSource)",
            record);
    }

    public async Task DeleteAsync(string packageId)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync("DELETE FROM InstallRecords WHERE PackageId = @PackageId", new { PackageId = packageId });
    }

    public async Task<IReadOnlyList<InstallRecord>> GetAllAsync()
    {
        using var conn = _db.CreateConnection();
        var results = await conn.QueryAsync<InstallRecord>("SELECT * FROM InstallRecords");
        return results.ToList();
    }

    public async Task<bool> ExistsAsync(string packageId)
    {
        using var conn = _db.CreateConnection();
        var count = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM InstallRecords WHERE PackageId = @PackageId", 
            new { PackageId = packageId });
        return count > 0;
    }

    public async Task<string?> GetLockedSourceAsync(string packageId)
    {
        var record = await GetByPackageIdAsync(packageId);
        return record?.LockedSource;
    }

    public async Task<bool> IsInstalledFromDifferentSourceAsync(string packageId, string sourceId)
    {
        var record = await GetByPackageIdAsync(packageId);
        if (record == null) return false;
        return record.LockedSource != sourceId;
    }
}
