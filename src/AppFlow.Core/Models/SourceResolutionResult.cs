namespace AppFlow.Core.Models;

public class SourceResolutionResult
{
    public SourceQueryResult? BestMatch { get; set; }
    public List<ScoredResult> AllOptions { get; set; } = new();
    public string? RecommendedSourceId { get; set; }
}
