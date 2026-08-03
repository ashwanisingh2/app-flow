namespace AppFlow.Tests.Services;

using AppFlow.Core.Enums;
using AppFlow.Core.Interfaces;
using AppFlow.Core.Models;
using AppFlow.Core.Services;
using FluentAssertions;
using Moq;

public class SecurityValidatorTests
{
    private readonly Mock<IInstallerIntegrityVerifier> _integrity = new();

    [Fact]
    public async Task ValidateAsync_UnsignedPackage_AddsWarningAndRequiresConfirmationWithoutHash()
    {
        var validator = new SecurityValidator(_integrity.Object);
        var source = new SourceQueryResult
        {
            PackageName = "Test",
            SourceId = "winget",
            SourceTrustScore = 5,
            IsSigned = false
        };

        var result = await validator.ValidateAsync(new PackageAction(), source);

        result.Warnings.Should().ContainSingle(message => message.Contains("Authenticode"));
        result.RiskLevel.Should().Be(RiskLevel.Medium);
        result.RequiresUserConfirmation.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_HashMismatch_BlocksInstall()
    {
        _integrity.Setup(x => x.VerifySha256Async(
                "https://example.test/app.exe", "ABC", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var validator = new SecurityValidator(_integrity.Object);
        var source = new SourceQueryResult
        {
            PackageName = "Test",
            SourceId = "winget",
            SourceTrustScore = 5,
            IsSigned = true,
            DownloadUrl = "https://example.test/app.exe",
            ExpectedHash = "ABC"
        };

        var result = await validator.ValidateAsync(new PackageAction(), source);

        result.BlockInstall.Should().BeTrue();
        result.HashChecked.Should().BeTrue();
        result.HashValid.Should().BeFalse();
        result.Errors.Should().Contain(message => message.Contains("checksum mismatch"));
    }

    [Fact]
    public async Task ValidateAsync_HighRiskSource_RequiresConfirmation()
    {
        var validator = new SecurityValidator(_integrity.Object);
        var source = new SourceQueryResult
        {
            PackageName = "Test",
            SourceId = "github",
            SourceTrustScore = 2,
            IsSigned = true
        };

        var result = await validator.ValidateAsync(new PackageAction(), source);

        result.RiskLevel.Should().Be(RiskLevel.High);
        result.RequiresUserConfirmation.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_ValidHashAndTrustedSource_AllowsInstall()
    {
        _integrity.Setup(x => x.VerifySha256Async(
                "https://example.test/app.exe", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var validator = new SecurityValidator(_integrity.Object);
        var source = new SourceQueryResult
        {
            PackageName = "Test",
            SourceId = "winget",
            SourceTrustScore = 5,
            IsSigned = true,
            DownloadUrl = "https://example.test/app.exe",
            ExpectedHash = new string('A', 64)
        };

        var result = await validator.ValidateAsync(new PackageAction(), source);

        result.IsValid.Should().BeTrue();
        result.HashValid.Should().BeTrue();
        result.RequiresUserConfirmation.Should().BeFalse();
    }
}
