namespace AppFlow.Database.Repositories;

using AppFlow.Core.Interfaces;
using AppFlow.Core.Models;
using Dapper;

public sealed class InstallRecordRepository : IInstallRecordStore
{
    private const string SelectColumns = """
        PackageId, InstalledFrom, InstalledVersion, LockedSource, InstalledAt,
        UserOverridden AS UserOverriddenSource
        """;

    private readonly AppFlowDb _db;

    public InstallRecordRepository(AppFlowDb db) => _db = db;

    public async Task<InstallRecord?> GetByPackageIdAsync(
        string packageId,
        CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<InstallRecord>(new CommandDefinition(
            $"SELECT {SelectColumns} FROM InstallRecords WHERE PackageId = @PackageId",
            new { PackageId = packageId },
            cancellationToken: ct));
    }

    public async Task SaveAsync(InstallRecord record, CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(new CommandDefinition("""
            INSERT INTO InstallRecords
                (PackageId, InstalledFrom, InstalledVersion, LockedSource, InstalledAt, UserOverridden)
            VALUES
                (@PackageId, @InstalledFrom, @InstalledVersion, @LockedSource, @InstalledAt, @UserOverriddenSource)
            ON CONFLICT(PackageId) DO UPDATE SET
                InstalledFrom = excluded.InstalledFrom,
                InstalledVersion = excluded.InstalledVersion,
                LockedSource = excluded.LockedSource,
                InstalledAt = excluded.InstalledAt,
                UserOverridden = excluded.UserOverridden
            """,
            new
            {
                record.PackageId,
                record.InstalledFrom,
                record.InstalledVersion,
                record.LockedSource,
                InstalledAt = record.InstalledAt.ToUniversalTime().ToString("O"),
                record.UserOverriddenSource
            },
            cancellationToken: ct));
    }

    public async Task DeleteAsync(string packageId, CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(new CommandDefinition(
            "DELETE FROM InstallRecords WHERE PackageId = @PackageId",
            new { PackageId = packageId },
            cancellationToken: ct));
    }

    public async Task<IReadOnlyList<InstallRecord>> GetAllAsync(CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();
        var results = await conn.QueryAsync<InstallRecord>(new CommandDefinition(
            $"SELECT {SelectColumns} FROM InstallRecords ORDER BY InstalledAt DESC",
            cancellationToken: ct));
        return results.ToList();
    }

    public async Task<bool> ExistsAsync(string packageId, CancellationToken ct = default) =>
        await GetByPackageIdAsync(packageId, ct) is not null;

    public async Task<string?> GetLockedSourceAsync(string packageId, CancellationToken ct = default) =>
        (await GetByPackageIdAsync(packageId, ct))?.LockedSource;

    public async Task<bool> IsInstalledFromDifferentSourceAsync(
        string packageId,
        string sourceId,
        CancellationToken ct = default)
    {
        var record = await GetByPackageIdAsync(packageId, ct);
        return record is not null
               && !string.Equals(record.LockedSource, sourceId, StringComparison.OrdinalIgnoreCase);
    }
}
