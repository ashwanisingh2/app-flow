namespace AppFlow.Database;

using Microsoft.Data.Sqlite;
using System.Reflection;

public class AppFlowDb
{
    private readonly string _connectionString;
    private bool _initialized;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public AppFlowDb()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dbDir = Path.Combine(appData, "AppFlow");
        Directory.CreateDirectory(dbDir);
        var dbPath = Path.Combine(dbDir, "appflow.db");
        _connectionString = $"Data Source={dbPath}";
    }

    public SqliteConnection CreateConnection() => new(_connectionString);

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (_initialized) return;
        await _initLock.WaitAsync(ct);
        try
        {
            if (_initialized) return;
            await using var conn = CreateConnection();
            await conn.OpenAsync(ct);
            
            var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
            var sqlPath = Path.Combine(assemblyDir, "Migrations", "InitialSchema.sql");
            var sql = await File.ReadAllTextAsync(sqlPath, ct);
            
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            await cmd.ExecuteNonQueryAsync(ct);
            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }
}
