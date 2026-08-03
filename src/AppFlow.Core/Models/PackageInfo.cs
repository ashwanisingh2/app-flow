namespace AppFlow.Core.Models;

using AppFlow.Core.Enums;

/// <summary>
/// Lightweight package representation used by search and installed-package lists.
/// </summary>
public class PackageInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Publisher { get; set; } = string.Empty;
    public string? InstalledVersion { get; set; }
    public string? LatestVersion { get; set; }
    public bool IsInstalled { get; set; }
    public bool HasUpdate => IsInstalled
                             && !string.IsNullOrWhiteSpace(InstalledVersion)
                             && !string.IsNullOrWhiteSpace(LatestVersion)
                             && !string.Equals(InstalledVersion, LatestVersion, StringComparison.OrdinalIgnoreCase);
    public bool IsSigned { get; set; }
    public TrustLevel TrustLevel { get; set; }

    /// <summary>The source represented by Id and used when opening details.</summary>
    public string SourceId { get; set; } = string.Empty;

    public List<string> AvailableSources { get; set; } = new();
    public Dictionary<string, string> SourcePackageIds { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
    public List<ActionType> SupportedActions { get; set; } = new();
    public string? Category { get; set; }
    public List<string> Tags { get; set; } = new();
    public bool IsFavorite { get; set; }
}
