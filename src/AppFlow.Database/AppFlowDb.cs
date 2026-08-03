namespace AppFlow.Database;

using Microsoft.Data.Sqlite;
using System.Reflection;

public sealed class AppFlowDb
{
    private const string MigrationResource = "AppFlow.Database.Migrations.InitialSchema.sql";
    private readonly string _connectionString;
    private bool _initialized;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public AppFlowDb() : this(GetDefaultDatabasePath())
    {
    }

    public AppFlowDb(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();
    }

    public SqliteConnection CreateConnection() => new(_connectionString);

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (_initialized) return;
        await _initLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_initialized) return;

            await using var conn = CreateConnection();
            await conn.OpenAsync(ct).ConfigureAwait(false);

            await using (var pragma = conn.CreateCommand())
            {
                pragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON; PRAGMA busy_timeout=5000;";
                await pragma.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }

            var assembly = Assembly.GetExecutingAssembly();
            await using var stream = assembly.GetManifestResourceStream(MigrationResource)
                ?? throw new InvalidOperationException("The embedded database migration could not be found.");
            using var reader = new StreamReader(stream);
            var sql = await reader.ReadToEndAsync(ct).ConfigureAwait(false);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private static string GetDefaultDatabasePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appData, "AppFlow", "appflow.db");
    }
}
