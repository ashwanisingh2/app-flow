namespace AppFlow.Core.Models;

using AppFlow.Core.Enums;

public class ActionResult
{
    public bool Success { get; set; }
    public int ExitCode { get; set; }
    public string LogOutput { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public string? ErrorSuggestion { get; set; }
    public string SourceUsed { get; set; } = string.Empty;
    public string? InstallerArgs { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public ActionType ActionPerformed { get; set; }
    public string PackageId { get; set; } = string.Empty;
}
