namespace AppFlow.Core.Services;

using AppFlow.Core.Interfaces;
using AppFlow.Core.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public class PackageService : IPackageService
{
    private readonly IEnumerable<ISourceAdapter> _adapters;
    private readonly SourceResolver _resolver;

    public PackageService(IEnumerable<ISourceAdapter> adapters, SourceResolver resolver)
    {
        _adapters = adapters;
        _resolver = resolver;
    }

    public async Task<List<PackageInfo>> SearchAllSourcesAsync(string query, CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(5));

        var tasks = _adapters.Where(a => a.IsAvailable)
                             .Select(a => SearchSafeAsync(a, query, cts.Token));

        var resultsArray = await Task.WhenAll(tasks);
        
        var merged = resultsArray.SelectMany(r => r)
                                 .GroupBy(p => p.Id)
                                 .Select(g => g.OrderByDescending(p => p.TrustLevel).First())
                                 .ToList();

        return merged;
    }

    private async Task<List<PackageInfo>> SearchSafeAsync(ISourceAdapter adapter, string query, CancellationToken ct)
    {
        try
        {
            return await adapter.SearchAsync(query, ct);
        }
        catch
        {
            return new List<PackageInfo>();
        }
    }

    public async Task<PackageDetail?> GetPackageDetailAsync(string packageId, CancellationToken ct = default)
    {
        var result = await _resolver.ResolveAsync(packageId, ct);
        if (result.BestMatch == null)
            return null;

        var adapter = _adapters.FirstOrDefault(a => a.SourceId == result.BestMatch.SourceId);
        if (adapter == null) return null;

        return await adapter.GetDetailsAsync(packageId, ct);
    }

    public async Task<List<PackageInfo>> GetInstalledPackagesAsync(CancellationToken ct = default)
    {
        var tasks = _adapters.Where(a => a.IsAvailable)
                             .Select(a => GetInstalledSafeAsync(a, ct));

        var resultsArray = await Task.WhenAll(tasks);
        
        return resultsArray.SelectMany(r => r).ToList();
    }

    private async Task<List<PackageInfo>> GetInstalledSafeAsync(ISourceAdapter adapter, CancellationToken ct)
    {
        try
        {
            return await adapter.GetInstalledPackagesAsync(ct);
        }
        catch
        {
            return new List<PackageInfo>();
        }
    }

    public async Task<List<PackageInfo>> GetUpdatesAvailableAsync(CancellationToken ct = default)
    {
        var installed = await GetInstalledPackagesAsync(ct);
        return installed.Where(p => p.HasUpdate).ToList();
    }
}
