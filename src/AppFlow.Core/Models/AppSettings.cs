namespace AppFlow.Core.Models;

public class AppSettings
{
    public string Theme { get; set; } = "System";
    public string? DefaultSourceId { get; set; }
    public string? CustomInstallPath { get; set; }
    public bool EnableSecurityValidation { get; set; } = true;
    public bool EnableSourceLocking { get; set; } = true;
    public int MaxSearchResults { get; set; } = 50;
    public int QueryTimeoutSeconds { get; set; } = 5;
}
