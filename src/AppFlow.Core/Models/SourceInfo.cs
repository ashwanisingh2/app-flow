namespace AppFlow.Core.Models;

public class SourceInfo
{
    public string SourceId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int TrustScore { get; set; }
    public bool IsAvailable { get; set; }
    public bool IsEnabled { get; set; } = true;
    public string? InstalledPath { get; set; }
}
