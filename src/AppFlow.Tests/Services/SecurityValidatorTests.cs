namespace AppFlow.Tests.Services;

using AppFlow.Core.Enums;
using AppFlow.Core.Models;
using AppFlow.Core.Services;
using FluentAssertions;
using System.Threading.Tasks;
using Xunit;

public class SecurityValidatorTests
{
    private readonly SecurityValidator _validator = new();

    [Fact]
    public async Task ValidateAsync_LowTrustScore_ReturnsWarning()
    {
        var action = new PackageAction { PackageId = "test" };
        var source = new SourceQueryResult { SourceTrustScore = 2, ExpectedHash = "hash" };

        var result = await _validator.ValidateAsync(action, source);

        result.Warnings.Should().Contain(w => w.Contains("low trust rating"));
        result.RiskLevel.Should().Be(RiskLevel.High);
    }

    [Fact]
    public async Task ValidateAsync_HighTrustScore_NoWarning()
    {
        var action = new PackageAction { PackageId = "test" };
        var source = new SourceQueryResult { SourceTrustScore = 5, IsSigned = true };

        var result = await _validator.ValidateAsync(action, source);

        result.Warnings.Should().BeEmpty();
        result.RiskLevel.Should().Be(RiskLevel.Low);
    }
}
