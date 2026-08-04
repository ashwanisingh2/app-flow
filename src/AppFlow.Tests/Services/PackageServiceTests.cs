namespace AppFlow.Tests.Services;

using AppFlow.Core.Interfaces;
using AppFlow.Core.Models;
using AppFlow.Core.Services;
using FluentAssertions;
using Moq;

public class PackageServiceTests
{
    [Fact]
    public async Task SearchAllSources_MergesExactIdsAndPreservesSources()
    {
        var winget = SearchAdapter("winget", AppFlow.Core.Enums.TrustLevel.Verified);
        var scoop = SearchAdapter("scoop", AppFlow.Core.Enums.TrustLevel.Medium);
        var settings = new AppSettings { QueryTimeoutSeconds = 5, MaxSearchResults = 50 };
        var registry = new SourceRegistry();
        var adapters = new[] { winget.Object, scoop.Object };
        var resolver = new SourceResolver(adapters, registry, settings);
        var service = new PackageService(adapters, resolver, registry, settings);

        var results = await service.SearchAllSourcesAsync("test");

        results.Should().ContainSingle();
        results[0].SourceId.Should().Be("winget");
        results[0].AvailableSources.Should().BeEquivalentTo("winget", "scoop");
    }

    [Fact]
    public async Task SearchAllSources_CorrelatesInstalledState()
    {
        var adapter = SearchAdapter("winget", AppFlow.Core.Enums.TrustLevel.Verified);
        adapter.Setup(x => x.GetInstalledPackagesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PackageInfo>
            {
                new()
                {
                    Id = "Vendor.App",
                    SourceId = "winget",
                    IsInstalled = true,
                    InstalledVersion = "1.0",
                    LatestVersion = "2.0"
                }
            });
        var settings = new AppSettings { QueryTimeoutSeconds = 5, MaxSearchResults = 50 };
        var registry = new SourceRegistry();
        var resolver = new SourceResolver(new[] { adapter.Object }, registry, settings);
        var service = new PackageService(new[] { adapter.Object }, resolver, registry, settings);

        var results = await service.SearchAllSourcesAsync("test");

        results.Should().ContainSingle();
        results[0].IsInstalled.Should().BeTrue();
        results[0].HasUpdate.Should().BeTrue();
    }

    [Fact]
    public void HasUpdate_EmptyLatestVersion_DoesNotReportUpdate()
    {
        var package = new PackageInfo
        {
            IsInstalled = true,
            InstalledVersion = "1.0",
            LatestVersion = string.Empty
        };

        package.HasUpdate.Should().BeFalse();
    }

    private static Mock<ISourceAdapter> SearchAdapter(
        string source,
        AppFlow.Core.Enums.TrustLevel trust)
    {
        var adapter = new Mock<ISourceAdapter>();
        adapter.SetupGet(x => x.SourceId).Returns(source);
        adapter.SetupGet(x => x.IsAvailable).Returns(true);
        adapter.Setup(x => x.GetInstalledPackagesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PackageInfo>());
        adapter.Setup(x => x.SearchAsync("test", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PackageInfo>
            {
                new()
                {
                    Id = "Vendor.App",
                    Name = "App",
                    SourceId = source,
                    TrustLevel = trust,
                    AvailableSources = new List<string> { source }
                }
            });
        return adapter;
    }
}
