namespace AppFlow.Core.Interfaces;

/// <summary>
/// Verifies installer integrity without loading the entire installer into memory.
/// </summary>
public interface IInstallerIntegrityVerifier
{
    Task<bool> VerifySha256Async(
        string location,
        string expectedHash,
        CancellationToken ct = default);

    bool HasTrustedAuthenticodeSignature(string filePath);
}
