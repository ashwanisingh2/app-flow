namespace AppFlow.Core.Services;

using AppFlow.Core.Enums;
using AppFlow.Core.Interfaces;
using AppFlow.Core.Models;

public class SecurityValidator : ISecurityValidator
{
    public Task<ValidationResult> ValidateAsync(PackageAction action, SourceQueryResult source)
    {
        var result = new ValidationResult();

        if (!source.IsSigned)
        {
            result.AddWarning("Package is unsigned. Proceed with caution.");
            result.RiskLevel = RiskLevel.Medium;
        }

        if (source.SourceTrustScore < 3)
        {
            result.AddWarning("Source has low trust rating.");
            result.RiskLevel = RiskLevel.High;
        }

        if (result.RiskLevel == RiskLevel.High)
        {
            result.RequiresUserConfirmation = true;
            result.ConfirmationMessage =
                $"This package is from an untrusted source ({source.SourceId}). " +
                "Are you sure you want to install it?";
        }

        if (!string.IsNullOrEmpty(source.ExpectedHash))
        {
            result.HashValid = VerifyHashAsync(source.DownloadUrl, source.ExpectedHash).Result;
            if (!result.HashValid)
            {
                result.BlockInstall = true;
                result.AddError("Hash mismatch detected. Installation blocked for security.");
            }
        }

        return Task.FromResult(result);
    }

    private Task<bool> VerifyHashAsync(string? downloadUrl, string expectedHash)
    {
        if (string.IsNullOrEmpty(downloadUrl))
            return Task.FromResult(true);

        // Stub implementation, usually downloads and hashes the file
        return Task.FromResult(false);
    }
}
