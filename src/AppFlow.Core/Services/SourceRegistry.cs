namespace AppFlow.Core.Services;

using System.Collections.Concurrent;

/// <summary>
/// In-memory source enablement state. Values are loaded from SQLite at startup.
/// </summary>
public sealed class SourceRegistry
{
    private readonly ConcurrentDictionary<string, bool> _enabled =
        new(StringComparer.OrdinalIgnoreCase);
    private long _version;

    public long Version => Interlocked.Read(ref _version);

    public bool IsEnabled(string sourceId) =>
        !_enabled.TryGetValue(sourceId, out var enabled) || enabled;

    public void SetEnabled(string sourceId, bool enabled)
    {
        var changed = !_enabled.TryGetValue(sourceId, out var previous) || previous != enabled;
        _enabled[sourceId] = enabled;
        if (changed) Interlocked.Increment(ref _version);
    }
}
