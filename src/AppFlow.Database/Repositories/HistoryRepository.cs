namespace AppFlow.Database.Repositories;

using AppFlow.Core.Models;
using Dapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public class HistoryRepository
{
    private readonly AppFlowDb _db;

    public HistoryRepository(AppFlowDb db)
    {
        _db = db;
    }

    public async Task LogActionAsync(ActionResult result, string packageName)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(@"
            INSERT INTO ActionHistory 
            (PackageId, PackageName, ActionType, SourceUsed, Success, ExitCode, ErrorMessage, InstallerArgs, LogOutput, Timestamp)
            VALUES 
            (@PackageId, @PackageName, @ActionType, @SourceUsed, @Success, @ExitCode, @ErrorMessage, @InstallerArgs, @LogOutput, @Timestamp)",
            new
            {
                result.PackageId,
                PackageName = packageName,
                ActionType = result.ActionPerformed.ToString(),
                result.SourceUsed,
                result.Success,
                result.ExitCode,
                result.ErrorMessage,
                result.InstallerArgs,
                result.LogOutput,
                Timestamp = result.Timestamp.ToString("O")
            });
    }

    public async Task<IReadOnlyList<ActionHistoryEntry>> GetRecentAsync(int count = 50)
    {
        using var conn = _db.CreateConnection();
        var results = await conn.QueryAsync<ActionHistoryEntry>(
            "SELECT * FROM ActionHistory ORDER BY Timestamp DESC LIMIT @Count", 
            new { Count = count });
        return results.ToList();
    }

    public async Task<IReadOnlyList<ActionHistoryEntry>> GetByPackageIdAsync(string packageId)
    {
        using var conn = _db.CreateConnection();
        var results = await conn.QueryAsync<ActionHistoryEntry>(
            "SELECT * FROM ActionHistory WHERE PackageId = @PackageId ORDER BY Timestamp DESC", 
            new { PackageId = packageId });
        return results.ToList();
    }

    public async Task<IReadOnlyList<ActionHistoryEntry>> GetByDateRangeAsync(DateTime from, DateTime to)
    {
        using var conn = _db.CreateConnection();
        var results = await conn.QueryAsync<ActionHistoryEntry>(
            "SELECT * FROM ActionHistory WHERE Timestamp >= @From AND Timestamp <= @To ORDER BY Timestamp DESC", 
            new { From = from.ToString("O"), To = to.ToString("O") });
        return results.ToList();
    }

    public async Task<IReadOnlyList<ActionHistoryEntry>> SearchAsync(string query)
    {
        using var conn = _db.CreateConnection();
        var results = await conn.QueryAsync<ActionHistoryEntry>(
            "SELECT * FROM ActionHistory WHERE PackageName LIKE @Query OR PackageId LIKE @Query ORDER BY Timestamp DESC", 
            new { Query = $"%{query}%" });
        return results.ToList();
    }

    public async Task ClearAllAsync()
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync("DELETE FROM ActionHistory");
    }
}
