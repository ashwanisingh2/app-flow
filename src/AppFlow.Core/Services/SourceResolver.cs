namespace AppFlow.Core.Services;

using AppFlow.Core.Interfaces;
using AppFlow.Core.Models;

public class SourceResolver
{
    private readonly IEnumerable<ISourceAdapter> _adapters;

    public SourceResolver(IEnumerable<ISourceAdapter> adapters)
    {
        _adapters = adapters;
    }

    public async Task<SourceResolutionResult> ResolveAsync(string packageId, CancellationToken ct = default)
    {
        var tasks = _adapters
            .Where(a => a.IsAvailable)
            .Select(a => QuerySourceSafeAsync(a, packageId, ct));

        var results = await Task.WhenAll(tasks);

        var scored = results
            .Where(r => r != null)
            .Select(r => new ScoredResult
            {
                Result = r!,
                Score = CalculateScore(r!)
            })
            .OrderByDescending(s => s.Score)
            .ToList();

        return new SourceResolutionResult
        {
            BestMatch = scored.FirstOrDefault()?.Result,
            AllOptions = scored,
            RecommendedSourceId = scored.FirstOrDefault()?.Result.SourceId
        };
    }

    private int CalculateScore(SourceQueryResult result)
    {
        int score = 0;
        if (result.IsSigned)          score += 3;
        if (result.IsOfficialSource)  score += 2;
        if (result.IsLatestVersion)   score += 1;
        if (result.IsTrustedPublisher) score += 2;
        if (result.SupportsSilent)    score += 1;
        score += result.SourceTrustScore;
        return score;
    }

    private async Task<SourceQueryResult?> QuerySourceSafeAsync(
        ISourceAdapter adapter, string packageId, CancellationToken ct)
    {
        try
        {
            var detail = await adapter.GetDetailsAsync(packageId, ct);
            if (detail == null) return null;

            return new SourceQueryResult
            {
                PackageId = detail.Id,
                SourceId = adapter.SourceId,
                PackageName = detail.Name,
                Version = detail.LatestVersion,
                IsSigned = detail.IsSigned,
                IsOfficialSource = detail.IsOfficialSource,
                IsLatestVersion = true, 
                IsTrustedPublisher = detail.IsSigned, 
                SupportsSilent = detail.SupportsSilent,
                SourceTrustScore = adapter.TrustScore,
                DownloadUrl = detail.DownloadUrl,
                ExpectedHash = detail.ExpectedHash,
                Description = detail.Description
            };
        }
        catch { return null; }
    }
}
