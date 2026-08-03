namespace AppFlow.Tests.Services;

using AppFlow.Core.Models;
using AppFlow.Database;
using AppFlow.Database.Repositories;
using FluentAssertions;

public sealed class InstallRecordRepositoryTests : IAsyncLifetime
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"appflow-tests-{Guid.NewGuid():N}.db");
    private AppFlowDb _database = null!;
    private InstallRecordRepository _repository = null!;

    public async Task InitializeAsync()
    {
        _database = new AppFlowDb(_dbPath);
        await _database.InitializeAsync();
        _repository = new InstallRecordRepository(_database);
    }

    [Fact]
    public async Task SaveRecord_SamePackageTwice_UpdatesSingleRecord()
    {
        await _repository.SaveAsync(Record("1.0"));
        await _repository.SaveAsync(Record("2.0"));

        var all = await _repository.GetAllAsync();

        all.Should().ContainSingle();
        all[0].InstalledVersion.Should().Be("2.0");
    }

    [Fact]
    public async Task GetLockedSource_ReturnsOriginalInstallSource()
    {
        await _repository.SaveAsync(Record("1.0"));

        var source = await _repository.GetLockedSourceAsync("Vendor.App");

        source.Should().Be("winget");
    }

    [Fact]
    public async Task UserOverrideColumn_RoundTripsToModelProperty()
    {
        var record = Record("1.0");
        record.UserOverriddenSource = true;
        await _repository.SaveAsync(record);

        var loaded = await _repository.GetByPackageIdAsync("vendor.app");

        loaded.Should().NotBeNull();
        loaded!.UserOverriddenSource.Should().BeTrue();
    }

    public Task DisposeAsync()
    {
        TryDelete(_dbPath);
        TryDelete(_dbPath + "-wal");
        TryDelete(_dbPath + "-shm");
        return Task.CompletedTask;
    }

    private static InstallRecord Record(string version) => new()
    {
        PackageId = "Vendor.App",
        InstalledFrom = "winget",
        InstalledVersion = version,
        LockedSource = "winget",
        InstalledAt = DateTime.UtcNow
    };

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (IOException)
        {
        }
    }
}
