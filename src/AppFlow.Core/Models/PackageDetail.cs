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
    public string? InstalledVersion { get; set; }
    public bool IsInstalled { get; set; }
    public bool HasUpdate => IsInstalled
                             && !string.IsNullOrWhiteSpace(InstalledVersion)
                             && !string.IsNullOrWhiteSpace(LatestVersion)
                             && !string.Equals(InstalledVersion, LatestVersion, StringComparison.OrdinalIgnoreCase);
    public bool IsSigned { get; set; }
    public bool IsTrustedPublisher { get; set; }
    public TrustLevel TrustLevel { get; set; }
    public List<string> AvailableSources { get; set; } = new();
    public string? Category { get; set; }
    public List<string> Tags { get; set; } = new();
    public string? ReleaseNotes { get; set; }
    public List<string> Dependencies { get; set; } = new();
    public string? DownloadUrl { get; set; }
    public string? ExpectedHash { get; set; }
    public string? LocalInstallerPath { get; set; }
    public string SourceId { get; set; } = string.Empty;
    public int SourceTrustScore { get; set; }
    public bool IsOfficialSource { get; set; }
    public bool SupportsSilent { get; set; }
}
