namespace AppFlow.Core.Services;

using AppFlow.Core.Enums;
using AppFlow.Core.Interfaces;
using AppFlow.Core.Models;

public sealed class SecurityValidator : ISecurityValidator
{
    private readonly IInstallerIntegrityVerifier _integrityVerifier;

    public SecurityValidator(IInstallerIntegrityVerifier integrityVerifier)
    {
        _integrityVerifier = integrityVerifier;
    }

    public async Task<ValidationResult> ValidateAsync(
        PackageAction action,
        SourceQueryResult source,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(source);

        var result = new ValidationResult();
        var location = source.LocalInstallerPath ?? source.DownloadUrl;

        if (!string.IsNullOrWhiteSpace(source.DownloadUrl)
            && Uri.TryCreate(source.DownloadUrl, UriKind.Absolute, out var downloadUri)
            && downloadUri.Scheme != Uri.UriSchemeHttps)
        {
            result.BlockInstall = true;
            result.RiskLevel = RiskLevel.High;
            result.AddError("Installer download does not use HTTPS.");
        }

        if (!string.IsNullOrWhiteSpace(source.ExpectedHash))
        {
            result.HashChecked = true;
            if (string.IsNullOrWhiteSpace(location))
            {
                result.HashValid = false;
                result.BlockInstall = true;
                result.AddError("A checksum was supplied, but the installer location is unavailable.");
            }
            else
            {
                try
                {
                    result.HashValid = await _integrityVerifier
                        .VerifySha256Async(location, source.ExpectedHash, ct)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    result.HashValid = false;
                    result.BlockInstall = true;
                    result.AddError("Integrity verification timed out before the installer could be checked.");
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (HttpRequestException)
                {
                    result.HashValid = false;
                    result.BlockInstall = true;
                    result.AddError("The installer could not be downloaded for integrity verification.");
                }
                catch (IOException)
                {
                    result.HashValid = false;
                    result.BlockInstall = true;
                    result.AddError("The installer could not be read for integrity verification.");
                }
                catch (UnauthorizedAccessException)
                {
                    result.HashValid = false;
                    result.BlockInstall = true;
                    result.AddError("AppFlow does not have permission to verify the installer.");
                }

                if (!result.HashValid)
                {
                    result.BlockInstall = true;
                    result.RiskLevel = RiskLevel.High;
                    if (result.Errors.Count == 0)
                        result.AddError("SHA-256 checksum mismatch detected. Installation was blocked.");
                }
            }
        }

        result.SignatureChecked = !string.IsNullOrWhiteSpace(source.LocalInstallerPath);
        result.SignatureValid = result.SignatureChecked
            ? _integrityVerifier.HasTrustedAuthenticodeSignature(source.LocalInstallerPath!)
            : source.IsSigned;

        if (!result.SignatureValid)
        {
            result.AddWarning("A trusted Authenticode signature could not be confirmed.");
            result.RiskLevel = Max(result.RiskLevel, RiskLevel.Medium);
        }

        if (source.SourceTrustScore < 3)
        {
            result.AddWarning("The selected source has a low trust rating.");
            result.RiskLevel = RiskLevel.High;
        }

        if (!result.BlockInstall
            && (result.RiskLevel == RiskLevel.High
                || (!result.SignatureValid && !result.HashChecked)))
        {
            result.RequiresUserConfirmation = true;
            result.ConfirmationMessage =
                $"AppFlow could not fully verify {source.PackageName} from {source.SourceId}. " +
                "Review the source before continuing.";
        }

        return result;
    }

    private static RiskLevel Max(RiskLevel left, RiskLevel right) =>
        (RiskLevel)Math.Max((int)left, (int)right);
}
