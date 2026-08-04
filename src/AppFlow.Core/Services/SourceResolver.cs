namespace AppFlow.Core.Services;

using AppFlow.Core.Interfaces;
using AppFlow.Core.Models;

public sealed class SourceResolver
{
    private readonly IEnumerable<ISourceAdapter> _adapters;
    private readonly SourceRegistry _sourceRegistry;
    private readonly AppSettings _settings;

    public SourceResolver(
        IEnumerable<ISourceAdapter> adapters,
        SourceRegistry sourceRegistry,
        AppSettings settings)
    {
        _adapters = adapters;
        _sourceRegistry = sourceRegistry;
        _settings = settings;
    }

    public Task<SourceResolutionResult> ResolveAsync(
        string packageId,
        CancellationToken ct = default) =>
        ResolveAsync(packageId, preferredSourceId: null, ct);

    public async Task<SourceResolutionResult> ResolveAsync(
        string packageId,
        string? preferredSourceId,
        CancellationToken ct = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(_settings.QueryTimeoutSeconds, 1, 60)));

        SourceQueryResult?[] results;
        try
        {
            var activeAdapters = await GetActiveAdaptersAsync(timeout.Token).ConfigureAwait(false);
            var tasks = activeAdapters.Select(a => QuerySourceSafeAsync(a, packageId, timeout.Token));
            results = await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            results = Array.Empty<SourceQueryResult?>();
        }

        var scored = results
            .Where(r => r is not null)
            .Select(r => new ScoredResult
            {
                Result = r!,
                Score = CalculateScore(r!)
            })
            .OrderByDescending(s => s.Score)
            .ThenBy(s => s.Result.SourceId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var preferred = !string.IsNullOrWhiteSpace(preferredSourceId)
            ? scored.FirstOrDefault(s => string.Equals(
                s.Result.SourceId,
                preferredSourceId,
                StringComparison.OrdinalIgnoreCase))
            : null;
        var selected = preferred ?? scored.FirstOrDefault();

        return new SourceResolutionResult
        {
            BestMatch = selected?.Result,
            AllOptions = scored,
            RecommendedSourceId = scored.FirstOrDefault()?.Result.SourceId
        };
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

    private static int CalculateScore(SourceQueryResult result)
    {
        var score = 0;
        if (result.IsSigned) score += 3;
        if (result.IsOfficialSource) score += 2;
        if (result.IsLatestVersion) score += 1;
        if (result.IsTrustedPublisher) score += 2;
        if (result.SupportsSilent) score += 1;
        score += Math.Clamp(result.SourceTrustScore, 0, 5);
        return score;
    }

    private static async Task<SourceQueryResult?> QuerySourceSafeAsync(
        ISourceAdapter adapter,
        string packageId,
        CancellationToken ct)
    {
        try
        {
            var detail = await adapter.GetDetailsAsync(packageId, ct).ConfigureAwait(false);
            if (detail is null || string.IsNullOrWhiteSpace(detail.Id))
                return null;

            return new SourceQueryResult
            {
                PackageId = detail.Id,
                SourceId = adapter.SourceId,
                PackageName = string.IsNullOrWhiteSpace(detail.Name) ? detail.Id : detail.Name,
                Publisher = detail.Publisher,
                Version = detail.LatestVersion,
                IsSigned = detail.IsSigned,
                IsOfficialSource = detail.IsOfficialSource,
                IsLatestVersion = !string.IsNullOrWhiteSpace(detail.LatestVersion),
                IsTrustedPublisher = detail.IsTrustedPublisher,
                SupportsSilent = detail.SupportsSilent,
                SourceTrustScore = adapter.TrustScore,
                DownloadUrl = detail.DownloadUrl,
                ExpectedHash = detail.ExpectedHash,
                LocalInstallerPath = detail.LocalInstallerPath,
                Description = detail.Description,
                Detail = detail
            };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (Exception)
        {
            // Adapter isolation boundary: one provider must not break all providers.
            return null;
        }
    }
}
