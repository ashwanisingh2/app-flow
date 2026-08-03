namespace AppFlow.Database.Repositories;

using Dapper;

public sealed class FavoritesRepository
{
    private readonly AppFlowDb _db;

    public FavoritesRepository(AppFlowDb db) => _db = db;

    public async Task AddAsync(string packageId, CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(new CommandDefinition(
            "INSERT OR IGNORE INTO Favorites (PackageId, AddedAt) VALUES (@PackageId, @AddedAt)",
            new { PackageId = packageId, AddedAt = DateTime.UtcNow.ToString("O") },
            cancellationToken: ct));
    }

    public async Task RemoveAsync(string packageId, CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(new CommandDefinition(
            "DELETE FROM Favorites WHERE PackageId = @PackageId",
            new { PackageId = packageId },
            cancellationToken: ct));
    }

    public async Task<IReadOnlyList<string>> GetAllAsync(CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();
        var results = await conn.QueryAsync<string>(new CommandDefinition(
            "SELECT PackageId FROM Favorites ORDER BY AddedAt DESC",
            cancellationToken: ct));
        return results.ToList();
    }

    public async Task<bool> IsFavoriteAsync(string packageId, CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();
        var count = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM Favorites WHERE PackageId = @PackageId",
            new { PackageId = packageId },
            cancellationToken: ct));
        return count > 0;
    }

    public async Task<bool> ToggleAsync(string packageId, CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();
        await conn.OpenAsync(ct);
        await using var transaction = await conn.BeginTransactionAsync(ct);

        var exists = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM Favorites WHERE PackageId = @PackageId",
            new { PackageId = packageId },
            transaction,
            cancellationToken: ct)) > 0;

        if (exists)
        {
            await conn.ExecuteAsync(new CommandDefinition(
                "DELETE FROM Favorites WHERE PackageId = @PackageId",
                new { PackageId = packageId },
                transaction,
                cancellationToken: ct));
        }
        else
        {
            await conn.ExecuteAsync(new CommandDefinition(
                "INSERT INTO Favorites (PackageId, AddedAt) VALUES (@PackageId, @AddedAt)",
                new { PackageId = packageId, AddedAt = DateTime.UtcNow.ToString("O") },
                transaction,
                cancellationToken: ct));
        }

        await transaction.CommitAsync(ct);
        return !exists;
    }
}
