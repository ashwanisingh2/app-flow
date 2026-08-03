namespace AppFlow.Core.Models;

public class InstallRecord
{
    public string PackageId { get; set; } = string.Empty;
    public string InstalledFrom { get; set; } = string.Empty;
    public string InstalledVersion { get; set; } = string.Empty;
    public string LockedSource { get; set; } = string.Empty;
    public DateTime InstalledAt { get; set; }
    public bool UserOverriddenSource { get; set; } = false;
}
