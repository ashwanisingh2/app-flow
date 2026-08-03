namespace AppFlow.Core.Models;

using AppFlow.Core.Enums;

public class ValidationResult
{
    public bool IsValid => !BlockInstall && Errors.Count == 0;
    public RiskLevel RiskLevel { get; set; } = RiskLevel.Low;
    public List<string> Warnings { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public bool RequiresUserConfirmation { get; set; }
    public string? ConfirmationMessage { get; set; }
    public bool HashValid { get; set; } = true;
    public bool BlockInstall { get; set; }

    public void AddWarning(string msg) => Warnings.Add(msg);
    public void AddError(string msg) => Errors.Add(msg);
}
