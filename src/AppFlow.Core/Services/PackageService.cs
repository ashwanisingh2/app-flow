namespace AppFlow.Core.Services;

using AppFlow.Core.Interfaces;
using AppFlow.Core.Models;

public sealed class PackageService : IPackageService
{
    private readonly IEnumerable<ISourceAdapter> _adapters;
    private readonly SourceResolver _resolver;
    private readonly SourceRegistry _sourceRegistry;
    private readonly AppSettings _settings;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, SearchCacheEntry> _searchCache =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _installedCacheLock = new(1, 1);
    private InstalledCacheEntry? _installedCache;

    public PackageService(
        IEnumerable<ISourceAdapter> adapters,
        SourceResolver resolver,
        SourceRegistry sourceRegistry,
        AppSettings settings)
    {
        _adapters = adapters;
        _resolver = resolver;
        _sourceRegistry = sourceRegistry;
        _settings = settings;
    }

    public async Task<List<PackageInfo>> SearchAllSourcesAsync(
        string query,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new List<PackageInfo>();

        var normalizedQuery = query.Trim();
        if (_searchCache.TryGetValue(normalizedQuery, out var cached)
            && cached.SourceRegistryVersion == _sourceRegistry.Version
            && DateTimeOffset.UtcNow - cached.CreatedAt < TimeSpan.FromSeconds(30))
        {
            return cached.Packages.ToList();
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(_settings.QueryTimeoutSeconds, 1, 60)));

        var activeAdapters = await GetActiveAdaptersAsync(timeout.Token).ConfigureAwait(false);
        var tasks = activeAdapters.Select(a => SearchSafeAsync(a, normalizedQuery, timeout.Token));
        List<PackageInfo>[] results;
        try
        {
            results = await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            results = Array.Empty<List<PackageInfo>>();
        }

        var merged = MergePackages(results.SelectMany(r => r))
            .Take(Math.Clamp(_settings.MaxSearchResults, 1, 500))
            .ToList();

        try
        {
            var installed = await GetInstalledPackagesAsync(timeout.Token).ConfigureAwait(false);
            var installedById = installed.ToDictionary(
                package => package.Id,
                StringComparer.OrdinalIgnoreCase);
            foreach (var package in merged)
            {
                if (!installedById.TryGetValue(package.Id, out var match)) continue;
                package.IsInstalled = true;
                package.InstalledVersion = match.InstalledVersion;
                package.LatestVersion ??= match.LatestVersion;
                package.SupportedActions = match.SupportedActions;
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Search results remain useful even if installed-state correlation times out.
        }

        _searchCache[normalizedQuery] = new SearchCacheEntry(
            DateTimeOffset.UtcNow,
            _sourceRegistry.Version,
            merged);
        return merged.ToList();
    }

    public async Task<PackageDetail?> GetPackageDetailAsync(
        string packageId,
        CancellationToken ct = default)
    {
        var result = await _resolver.ResolveAsync(packageId, ct).ConfigureAwait(false);
        return result.BestMatch?.Detail;
    }

    public async Task<List<PackageInfo>> GetInstalledPackagesAsync(CancellationToken ct = default)
    {
        var cache = _installedCache;
        if (cache is not null
            && cache.SourceRegistryVersion == _sourceRegistry.Version
            && DateTimeOffset.UtcNow - cache.CreatedAt < TimeSpan.FromSeconds(30))
        {
            return cache.Packages.ToList();
        }

        await _installedCacheLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            cache = _installedCache;
            if (cache is not null
                && cache.SourceRegistryVersion == _sourceRegistry.Version
                && DateTimeOffset.UtcNow - cache.CreatedAt < TimeSpan.FromSeconds(30))
            {
                return cache.Packages.ToList();
            }

            var activeAdapters = await GetActiveAdaptersAsync(ct).ConfigureAwait(false);
            var tasks = activeAdapters.Select(a => GetInstalledSafeAsync(a, ct));
            var results = await Task.WhenAll(tasks).ConfigureAwait(false);
            var merged = MergePackages(results.SelectMany(r => r)).ToList();
            _installedCache = new InstalledCacheEntry(
                DateTimeOffset.UtcNow,
                _sourceRegistry.Version,
                merged);
            return merged.ToList();
        }
        finally
        {
            _installedCacheLock.Release();
        }
    }

    public async Task<List<PackageInfo>> GetUpdatesAvailableAsync(CancellationToken ct = default)
    {
        var installed = await GetInstalledPackagesAsync(ct).ConfigureAwait(false);
        return installed.Where(p => p.HasUpdate).ToList();
    }

    private async Task<IReadOnlyList<ISourceAdapter>> GetActiveAdaptersAsync(CancellationToken ct)
    {
        var candidates = _adapters
            .Where(adapter => _sourceRegistry.IsEnabled(adapter.SourceId))
            .ToList();
        var checks = candidates.Select(async adapter => new
        {
            Adapter = adapter,
            Available = await Task.Run(() => adapter.IsAvailable, ct).ConfigureAwait(false)
        });
        return (await Task.WhenAll(checks).ConfigureAwait(false))
            .Where(result => result.Available)
            .Select(result => result.Adapter)
            .ToList();
    }

    private static async Task<List<PackageInfo>> SearchSafeAsync(
        ISourceAdapter adapter,
        string query,
        CancellationToken ct)
    {
        try
        {
            return await adapter.SearchAsync(query, ct).ConfigureAwait(false)
                   ?? new List<PackageInfo>();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException)
        {
            return new List<PackageInfo>();
        }
        catch (IOException)
        {
            return new List<PackageInfo>();
        }
        catch (InvalidOperationException)
        {
            return new List<PackageInfo>();
        }
    }

    private static async Task<List<PackageInfo>> GetInstalledSafeAsync(
        ISourceAdapter adapter,
        CancellationToken ct)
    {
        try
        {
            return await adapter.GetInstalledPackagesAsync(ct).ConfigureAwait(false)
                   ?? new List<PackageInfo>();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (IOException)
        {
            return new List<PackageInfo>();
        }
        catch (InvalidOperationException)
        {
            return new List<PackageInfo>();
        }
        catch (Exception)
        {
            // Adapter isolation boundary: return results from healthy providers.
            return new List<PackageInfo>();
        }
    }

    private sealed record SearchCacheEntry(
        DateTimeOffset CreatedAt,
        long SourceRegistryVersion,
        List<PackageInfo> Packages);

    private sealed record InstalledCacheEntry(
        DateTimeOffset CreatedAt,
        long SourceRegistryVersion,
        List<PackageInfo> Packages);

    private static IEnumerable<PackageInfo> MergePackages(IEnumerable<PackageInfo> packages)
    {
        foreach (var group in packages
                     .Where(p => !string.IsNullOrWhiteSpace(p.Id))
                     .GroupBy(p => p.Id, StringComparer.OrdinalIgnoreCase))
        {
            var best = group
                .OrderByDescending(p => p.HasUpdate)
                .ThenByDescending(p => p.TrustLevel)
                .First();

            best.AvailableSources = group
                .SelectMany(p => p.AvailableSources.Append(p.SourceId))
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            best.SourcePackageIds = group
                .SelectMany(p => p.SourcePackageIds.Count > 0
                    ? p.SourcePackageIds
                    : new Dictionary<string, string> { [p.SourceId] = p.Id })
                .Where(pair => !string.IsNullOrWhiteSpace(pair.Key))
                .GroupBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Value, StringComparer.OrdinalIgnoreCase);

            best.IsInstalled = group.Any(p => p.IsInstalled);
            best.IsFavorite = group.Any(p => p.IsFavorite);
            yield return best;
        }
    }
}
