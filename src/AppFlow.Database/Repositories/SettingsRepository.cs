namespace AppFlow.Database.Repositories;

using AppFlow.Core.Models;
using Dapper;
using System.Text.Json;

public sealed class SettingsRepository
{
    private const string AppSettingsKey = "app.settings";
    private readonly AppFlowDb _db;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public SettingsRepository(AppFlowDb db) => _db = db;

    public async Task<AppSettings> LoadAppSettingsAsync(CancellationToken ct = default)
    {
        var json = await GetAsync(AppSettingsKey, ct);
        if (string.IsNullOrWhiteSpace(json))
            return new AppSettings();

        try
        {
            return JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions) ?? new AppSettings();
        }
        catch (JsonException)
        {
            return new AppSettings();
        }
    }

    public Task SaveAppSettingsAsync(AppSettings settings, CancellationToken ct = default) =>
        SetAsync(AppSettingsKey, JsonSerializer.Serialize(settings, _jsonOptions), ct);

    public async Task<bool> GetSourceEnabledAsync(string sourceId, CancellationToken ct = default)
    {
        var value = await GetAsync(SourceKey(sourceId), ct);
        return !bool.TryParse(value, out var enabled) || enabled;
    }

    public Task SetSourceEnabledAsync(string sourceId, bool enabled, CancellationToken ct = default) =>
        SetAsync(SourceKey(sourceId), enabled.ToString(), ct);

    public async Task<string?> GetAsync(string key, CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<string>(new CommandDefinition(
            "SELECT Value FROM Settings WHERE Key = @Key",
            new { Key = key },
            cancellationToken: ct));
    }

    public async Task SetAsync(string key, string value, CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(new CommandDefinition("""
            INSERT INTO Settings (Key, Value) VALUES (@Key, @Value)
            ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value
            """,
            new { Key = key, Value = value },
            cancellationToken: ct));
    }

    private static string SourceKey(string sourceId) =>
        $"source.{sourceId.ToLowerInvariant()}.enabled";
}
