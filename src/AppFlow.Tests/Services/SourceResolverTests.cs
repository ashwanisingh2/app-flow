namespace AppFlow.Tests.Services;

using AppFlow.Core.Interfaces;
using AppFlow.Core.Models;
using AppFlow.Core.Services;
using FluentAssertions;
using Moq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

public class SourceResolverTests
{
    [Fact]
    public async Task ResolveAsync_ShouldReturnBestScoredSource()
    {
        // Arrange
        var adapters = new List<ISourceAdapter>();

        var mockA = new Mock<ISourceAdapter>();
        mockA.Setup(x => x.IsAvailable).Returns(true);
        mockA.Setup(x => x.SourceId).Returns("offline");
        mockA.Setup(x => x.TrustScore).Returns(1);
        mockA.Setup(x => x.GetDetailsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(new PackageDetail { SourceId = "offline", SourceTrustScore = 1 });

        var mockB = new Mock<ISourceAdapter>();
        mockB.Setup(x => x.IsAvailable).Returns(true);
        mockB.Setup(x => x.SourceId).Returns("winget");
        mockB.Setup(x => x.TrustScore).Returns(5);
        mockB.Setup(x => x.GetDetailsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(new PackageDetail { SourceId = "winget", SourceTrustScore = 5, IsOfficialSource = true, ExpectedHash = "hash123" });

        adapters.Add(mockA.Object);
        adapters.Add(mockB.Object);

        var resolver = new SourceResolver(adapters);

        // Act
        var result = await resolver.ResolveAsync("test-package");

        // Assert
        result.BestMatch.Should().NotBeNull();
        result.BestMatch!.SourceId.Should().Be("winget"); // Because winget has higher trust and official source + hash
    }

    [Fact]
    public async Task ResolveAsync_OneAdapterFails_ShouldStillReturnFromOthers()
    {
        // Arrange
        var mockFail = new Mock<ISourceAdapter>();
        mockFail.Setup(x => x.IsAvailable).Returns(true);
        mockFail.Setup(x => x.GetDetailsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new System.Exception("Network error"));

        var mockSuccess = new Mock<ISourceAdapter>();
        mockSuccess.Setup(x => x.IsAvailable).Returns(true);
        mockSuccess.Setup(x => x.SourceId).Returns("choco");
        mockSuccess.Setup(x => x.GetDetailsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new PackageDetail { SourceId = "choco", SourceTrustScore = 4 });

        var resolver = new SourceResolver(new[] { mockFail.Object, mockSuccess.Object });

        // Act
        var result = await resolver.ResolveAsync("test");

        // Assert
        result.BestMatch.Should().NotBeNull();
        result.BestMatch!.SourceId.Should().Be("choco");
    }
}
