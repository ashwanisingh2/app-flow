namespace AppFlow.Core.Models;

public class SourceQueryResult
{
    public string PackageId { get; set; } = string.Empty;
    public string SourceId { get; set; } = string.Empty;
    public string PackageName { get; set; } = string.Empty;
    public string Publisher { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public bool IsSigned { get; set; }
    public bool IsOfficialSource { get; set; }
    public bool IsLatestVersion { get; set; }
    public bool IsTrustedPublisher { get; set; }
    public bool SupportsSilent { get; set; }
    public int SourceTrustScore { get; set; }
    public string? DownloadUrl { get; set; }
    public string? ExpectedHash { get; set; }
    public string? LocalInstallerPath { get; set; }
    public string? Description { get; set; }
    public PackageDetail? Detail { get; set; }
}
