namespace AppFlow.Database.Repositories;

using AppFlow.Core.Interfaces;
using AppFlow.Core.Models;
using Dapper;

public sealed class HistoryRepository : IActionHistoryStore
{
    private readonly AppFlowDb _db;

    public HistoryRepository(AppFlowDb db) => _db = db;

    public async Task LogActionAsync(
        ActionResult result,
        string packageName,
        CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(new CommandDefinition("""
            INSERT INTO ActionHistory
                (PackageId, PackageName, ActionType, SourceUsed, Success, ExitCode,
                 ErrorMessage, InstallerArgs, LogOutput, Timestamp)
            VALUES
                (@PackageId, @PackageName, @ActionType, @SourceUsed, @Success, @ExitCode,
                 @ErrorMessage, @InstallerArgs, @LogOutput, @Timestamp)
            """,
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
                Timestamp = result.Timestamp.ToUniversalTime().ToString("O")
            },
            cancellationToken: ct));
    }

    public Task<IReadOnlyList<ActionHistoryEntry>> GetRecentAsync(
        int count = 50,
        CancellationToken ct = default) =>
        QueryAsync(
            "SELECT * FROM ActionHistory ORDER BY Timestamp DESC LIMIT @Count",
            new { Count = Math.Clamp(count, 1, 1000) },
            ct);

    public Task<IReadOnlyList<ActionHistoryEntry>> GetByPackageIdAsync(
        string packageId,
        CancellationToken ct = default) =>
        QueryAsync(
            "SELECT * FROM ActionHistory WHERE PackageId = @PackageId ORDER BY Timestamp DESC",
            new { PackageId = packageId },
            ct);

    public Task<IReadOnlyList<ActionHistoryEntry>> GetByDateRangeAsync(
        DateTime from,
        DateTime to,
        CancellationToken ct = default) =>
        QueryAsync(
            "SELECT * FROM ActionHistory WHERE Timestamp >= @From AND Timestamp <= @To ORDER BY Timestamp DESC",
            new { From = from.ToUniversalTime().ToString("O"), To = to.ToUniversalTime().ToString("O") },
            ct);

    public Task<IReadOnlyList<ActionHistoryEntry>> SearchAsync(
        string query,
        CancellationToken ct = default) =>
        QueryAsync(
            "SELECT * FROM ActionHistory WHERE PackageName LIKE @Query ESCAPE '\\' OR PackageId LIKE @Query ESCAPE '\\' ORDER BY Timestamp DESC",
            new { Query = $"%{EscapeLike(query)}%" },
            ct);

    public async Task ClearAllAsync(CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(new CommandDefinition(
            "DELETE FROM ActionHistory",
            cancellationToken: ct));
    }

    private async Task<IReadOnlyList<ActionHistoryEntry>> QueryAsync(
        string sql,
        object? parameters,
        CancellationToken ct)
    {
        using var conn = _db.CreateConnection();
        var results = await conn.QueryAsync<ActionHistoryEntry>(new CommandDefinition(
            sql,
            parameters,
            cancellationToken: ct));
        return results.ToList();
    }

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
             .Replace("%", "\\%", StringComparison.Ordinal)
             .Replace("_", "\\_", StringComparison.Ordinal);
}
