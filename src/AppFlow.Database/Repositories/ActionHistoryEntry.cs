namespace AppFlow.Database.Repositories;

public class ActionHistoryEntry
{
    public int Id { get; set; }
    public string PackageId { get; set; } = string.Empty;
    public string PackageName { get; set; } = string.Empty;
    public string ActionType { get; set; } = string.Empty;
    public string SourceUsed { get; set; } = string.Empty;
    public bool Success { get; set; }
    public int ExitCode { get; set; }
    public string? ErrorMessage { get; set; }
    public string? InstallerArgs { get; set; }
    public string? LogOutput { get; set; }
    public DateTime Timestamp { get; set; }
}
