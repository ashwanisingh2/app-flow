namespace AppFlow.Core.Models;

using AppFlow.Core.Enums;

public class PackageAction
{
    public ActionType Type { get; set; }
    public string PackageId { get; set; } = string.Empty;
    public string PackageName { get; set; } = string.Empty;
    public string SourceId { get; set; } = string.Empty;
    public bool RequiresAdmin { get; set; }
    public bool SupportsSilent { get; set; }
    public bool SupportsPortable { get; set; }
    public string? CustomArgs { get; set; }
    public string? TargetVersion { get; set; }
}
