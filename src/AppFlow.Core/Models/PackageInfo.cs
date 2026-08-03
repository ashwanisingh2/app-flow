namespace AppFlow.Core.Models;

using AppFlow.Core.Enums;

/// <summary>
/// Core package representation with essential metadata.
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
                             && InstalledVersion != null
                             && LatestVersion != null
                             && InstalledVersion != LatestVersion;
    public bool IsSigned { get; set; }
    public TrustLevel TrustLevel { get; set; }
    public List<string> AvailableSources { get; set; } = new();
    public List<ActionType> SupportedActions { get; set; } = new();
    public string? Category { get; set; }
    public List<string> Tags { get; set; } = new();
    public bool IsFavorite { get; set; }
}
