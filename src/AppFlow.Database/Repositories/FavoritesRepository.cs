namespace AppFlow.Database.Repositories;

using Dapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public class FavoritesRepository
{
    private readonly AppFlowDb _db;

    public FavoritesRepository(AppFlowDb db)
    {
        _db = db;
    }

    public async Task AddAsync(string packageId)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(
            "INSERT OR IGNORE INTO Favorites (PackageId, AddedAt) VALUES (@PackageId, @AddedAt)",
            new { PackageId = packageId, AddedAt = DateTime.UtcNow.ToString("O") });
    }

    public async Task RemoveAsync(string packageId)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync("DELETE FROM Favorites WHERE PackageId = @PackageId", new { PackageId = packageId });
    }

    public async Task<IReadOnlyList<string>> GetAllAsync()
    {
        using var conn = _db.CreateConnection();
        var results = await conn.QueryAsync<string>("SELECT PackageId FROM Favorites ORDER BY AddedAt DESC");
        return results.ToList();
    }

    public async Task<bool> IsFavoriteAsync(string packageId)
    {
        using var conn = _db.CreateConnection();
        var count = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Favorites WHERE PackageId = @PackageId", 
            new { PackageId = packageId });
        return count > 0;
    }

    public async Task<bool> ToggleAsync(string packageId)
    {
        var isFav = await IsFavoriteAsync(packageId);
        if (isFav)
        {
            await RemoveAsync(packageId);
            return false;
        }
        else
        {
            await AddAsync(packageId);
            return true;
        }
    }
}
