namespace AppFlow.Tests.Services;

using AppFlow.Core.Interfaces;
using AppFlow.Core.Models;
using AppFlow.Core.Services;
using FluentAssertions;
using Moq;

public class SourceResolverTests
{
    [Fact]
    public async Task ResolveAsync_MultipleSourcesAvailable_ReturnsBestScore()
    {
        var low = Adapter("offline", 1, new PackageDetail { Id = "test", Name = "Test" });
        var high = Adapter("winget", 5, new PackageDetail
        {
            Id = "test",
            Name = "Test",
            IsOfficialSource = true,
            IsSigned = true,
            LatestVersion = "1.0"
        });
        var resolver = CreateResolver(low.Object, high.Object);

        var result = await resolver.ResolveAsync("test");

        result.BestMatch.Should().NotBeNull();
        result.BestMatch!.SourceId.Should().Be("winget");
        result.AllOptions.Should().HaveCount(2);
    }

    [Fact]
    public async Task ResolveAsync_PreferredSourceAvailable_SelectsItWithoutLosingRecommendation()
    {
        var low = Adapter("scoop", 3, new PackageDetail { Id = "test", Name = "Test" });
        var high = Adapter("winget", 5, new PackageDetail
        {
            Id = "test", Name = "Test", IsOfficialSource = true, IsSigned = true
        });
        var resolver = CreateResolver(low.Object, high.Object);

        var result = await resolver.ResolveAsync("test", "scoop");

        result.BestMatch!.SourceId.Should().Be("scoop");
        result.RecommendedSourceId.Should().Be("winget");
    }

    [Fact]
    public async Task ResolveAsync_OneSourceFails_OthersStillReturn()
    {
        var failed = new Mock<ISourceAdapter>();
        failed.SetupGet(x => x.IsAvailable).Returns(true);
        failed.SetupGet(x => x.SourceId).Returns("failed");
        failed.Setup(x => x.GetDetailsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("provider failed"));
        var healthy = Adapter("chocolatey", 4, new PackageDetail { Id = "test", Name = "Test" });
        var resolver = CreateResolver(failed.Object, healthy.Object);

        var result = await resolver.ResolveAsync("test");

        result.BestMatch.Should().NotBeNull();
        result.BestMatch!.SourceId.Should().Be("chocolatey");
    }

    [Fact]
    public async Task ResolveAsync_AllSourcesFail_ReturnsEmptyResult()
    {
        var failed = Adapter("winget", 5, detail: null);
        var resolver = CreateResolver(failed.Object);

        var result = await resolver.ResolveAsync("missing");

        result.BestMatch.Should().BeNull();
        result.AllOptions.Should().BeEmpty();
    }

    private static SourceResolver CreateResolver(params ISourceAdapter[] adapters) =>
        new(adapters, new SourceRegistry(), new AppSettings { QueryTimeoutSeconds = 5 });

    private static Mock<ISourceAdapter> Adapter(string id, int trust, PackageDetail? detail)
    {
        var adapter = new Mock<ISourceAdapter>();
        adapter.SetupGet(x => x.IsAvailable).Returns(true);
        adapter.SetupGet(x => x.SourceId).Returns(id);
        adapter.SetupGet(x => x.TrustScore).Returns(trust);
        adapter.Setup(x => x.GetDetailsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(detail);
        return adapter;
    }
}
