namespace AppFlow.Core.Models;

public class ScoredResult
{
    public SourceQueryResult Result { get; set; } = new();
    public int Score { get; set; }
}
