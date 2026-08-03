namespace AppFlow.Core.Models;

using AppFlow.Core.Enums;

/// <summary>
/// Extended package details for the AppDetailPage.
/// </summary>
public class PackageDetail
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Publisher { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Homepage { get; set; }
    public string? License { get; set; }
    public string LatestVersion { get; set; } = string.Empty;
    public bool IsSigned { get; set; }
    public TrustLevel TrustLevel { get; set; }
    public List<string> AvailableSources { get; set; } = new();
    public string? Category { get; set; }
    public List<string> Tags { get; set; } = new();
    public string? ReleaseNotes { get; set; }
    public List<string> Dependencies { get; set; } = new();
    public string? DownloadUrl { get; set; }
    public string? ExpectedHash { get; set; }
    public string SourceId { get; set; } = string.Empty;
    public int SourceTrustScore { get; set; }
    public bool IsOfficialSource { get; set; }
    public bool SupportsSilent { get; set; }
}
