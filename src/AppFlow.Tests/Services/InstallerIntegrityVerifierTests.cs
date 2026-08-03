namespace AppFlow.Tests.Services;

using AppFlow.Core.Services;
using FluentAssertions;
using System.Security.Cryptography;

public sealed class InstallerIntegrityVerifierTests
{
    [Fact]
    public async Task VerifySha256Async_LocalFileWithMatchingHash_ReturnsTrue()
    {
        var path = Path.Combine(Path.GetTempPath(), $"appflow-hash-{Guid.NewGuid():N}.bin");
        var content = "AppFlow integrity test"u8.ToArray();
        await File.WriteAllBytesAsync(path, content);
        try
        {
            var expected = Convert.ToHexString(SHA256.HashData(content));
            var verifier = new InstallerIntegrityVerifier();

            var valid = await verifier.VerifySha256Async(path, expected);

            valid.Should().BeTrue();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task VerifySha256Async_LocalFileWithWrongHash_ReturnsFalse()
    {
        var path = Path.Combine(Path.GetTempPath(), $"appflow-hash-{Guid.NewGuid():N}.bin");
        await File.WriteAllTextAsync(path, "content");
        try
        {
            var verifier = new InstallerIntegrityVerifier();

            var valid = await verifier.VerifySha256Async(path, new string('0', 64));

            valid.Should().BeFalse();
        }
        finally
        {
            File.Delete(path);
        }
    }
}
