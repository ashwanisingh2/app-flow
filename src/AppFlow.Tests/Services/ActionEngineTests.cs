namespace AppFlow.Tests.Services;

using AppFlow.Core.Enums;
using AppFlow.Core.Interfaces;
using AppFlow.Core.Models;
using AppFlow.Core.Services;
using FluentAssertions;
using Moq;

public class ActionEngineTests
{
    [Fact]
    public async Task Install_WhenRecordExists_PreventsDuplicateInstall()
    {
        var adapter = Adapter("winget");
        var records = new Mock<IInstallRecordStore>();
        records.Setup(x => x.GetByPackageIdAsync("pkg", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstallRecord { PackageId = "pkg", LockedSource = "winget" });
        var engine = CreateEngine(new[] { adapter.Object }, records);
        var action = new PackageAction
        {
            Type = ActionType.Install,
            PackageId = "pkg",
            PackageName = "Package",
            SourceId = "winget"
        };

        var result = await engine.ExecuteAsync(action, new Progress<string>());

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("already managed");
        adapter.Verify(x => x.InstallAsync(
            It.IsAny<PackageAction>(), It.IsAny<IProgress<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Install_WhenSourceReportsExistingPackage_ImportsAndPreventsDuplicate()
    {
        var adapter = Adapter("winget");
        adapter.Setup(x => x.GetInstalledPackagesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PackageInfo>
            {
                new() { Id = "pkg", InstalledVersion = "1.0", IsInstalled = true }
            });
        var records = new Mock<IInstallRecordStore>();
        records.Setup(x => x.GetByPackageIdAsync("pkg", It.IsAny<CancellationToken>()))
            .ReturnsAsync((InstallRecord?)null);
        var engine = CreateEngine(new[] { adapter.Object }, records);

        var result = await engine.ExecuteAsync(new PackageAction
        {
            Type = ActionType.Install,
            PackageId = "pkg",
            PackageName = "Package",
            SourceId = "winget"
        }, new Progress<string>());

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("already installed");
        records.Verify(x => x.SaveAsync(
            It.Is<InstallRecord>(record => record.LockedSource == "winget"),
            It.IsAny<CancellationToken>()), Times.Once);
        adapter.Verify(x => x.InstallAsync(
            It.IsAny<PackageAction>(), It.IsAny<IProgress<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Update_UsesOriginalLockedSource()
    {
        var winget = Adapter("winget");
        winget.Setup(x => x.GetDetailsAsync("pkg", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PackageDetail
            {
                Id = "pkg", Name = "Package", LatestVersion = "2.0", SourceId = "winget", IsSigned = true
            });
        winget.Setup(x => x.UpdateAsync(
                It.IsAny<PackageAction>(), It.IsAny<IProgress<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ActionResult { Success = true });
        var chocolatey = Adapter("chocolatey");

        var records = new Mock<IInstallRecordStore>();
        records.Setup(x => x.GetByPackageIdAsync("pkg", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstallRecord
            {
                PackageId = "pkg",
                InstalledFrom = "winget",
                InstalledVersion = "1.0",
                LockedSource = "winget",
                InstalledAt = DateTime.UtcNow
            });
        var engine = CreateEngine(new[] { winget.Object, chocolatey.Object }, records);
        var action = new PackageAction
        {
            Type = ActionType.Update,
            PackageId = "pkg",
            PackageName = "Package",
            SourceId = "chocolatey"
        };

        var result = await engine.ExecuteAsync(action, new Progress<string>());

        result.Success.Should().BeTrue();
        result.SourceUsed.Should().Be("winget");
        winget.Verify(x => x.UpdateAsync(
            It.Is<PackageAction>(a => a.SourceId == "winget"),
            It.IsAny<IProgress<string>>(),
            It.IsAny<CancellationToken>()), Times.Once);
        chocolatey.Verify(x => x.UpdateAsync(
            It.IsAny<PackageAction>(), It.IsAny<IProgress<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Install_ConfirmationRequired_DoesNotRunAdapterBeforeConfirmation()
    {
        var adapter = Adapter("github", trust: 2);
        var records = new Mock<IInstallRecordStore>();
        records.Setup(x => x.GetByPackageIdAsync("owner/repo", It.IsAny<CancellationToken>()))
            .ReturnsAsync((InstallRecord?)null);
        var validator = new Mock<ISecurityValidator>();
        validator.Setup(x => x.ValidateAsync(
                It.IsAny<PackageAction>(), It.IsAny<SourceQueryResult>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult
            {
                RequiresUserConfirmation = true,
                ConfirmationMessage = "Confirm"
            });
        var engine = CreateEngine(new[] { adapter.Object }, records, validator);
        var source = new SourceQueryResult
        {
            PackageId = "owner/repo", PackageName = "Repo", SourceId = "github"
        };

        var result = await engine.ValidateAndExecuteAsync(
            new PackageAction
            {
                Type = ActionType.Install,
                PackageId = "owner/repo",
                PackageName = "Repo",
                SourceId = "github"
            },
            source,
            new Progress<string>());

        result.RequiresConfirmation.Should().BeTrue();
        adapter.Verify(x => x.InstallAsync(
            It.IsAny<PackageAction>(), It.IsAny<IProgress<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static ActionEngine CreateEngine(
        IEnumerable<ISourceAdapter> adapters,
        Mock<IInstallRecordStore> records,
        Mock<ISecurityValidator>? validator = null)
    {
        records.Setup(x => x.SaveAsync(It.IsAny<InstallRecord>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        records.Setup(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        if (validator is null)
        {
            validator = new Mock<ISecurityValidator>();
            validator.Setup(x => x.ValidateAsync(
                    It.IsAny<PackageAction>(), It.IsAny<SourceQueryResult>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ValidationResult());
        }

        var history = new Mock<IActionHistoryStore>();
        history.Setup(x => x.LogActionAsync(
                It.IsAny<ActionResult>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return new ActionEngine(
            adapters,
            validator.Object,
            records.Object,
            history.Object,
            Mock.Of<ILogService>(),
            new SourceRegistry());
    }

    private static Mock<ISourceAdapter> Adapter(string sourceId, int trust = 5)
    {
        var adapter = new Mock<ISourceAdapter>();
        adapter.SetupGet(x => x.SourceId).Returns(sourceId);
        adapter.SetupGet(x => x.DisplayName).Returns(sourceId);
        adapter.SetupGet(x => x.TrustScore).Returns(trust);
        adapter.SetupGet(x => x.IsAvailable).Returns(true);
        adapter.Setup(x => x.GetInstalledPackagesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PackageInfo>());
        return adapter;
    }
}
