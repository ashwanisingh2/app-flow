namespace AppFlow.Sources;

using AppFlow.Core.Enums;
using AppFlow.Core.Interfaces;
using AppFlow.Core.Models;
using AppFlow.Sources.Helpers;

public class ChocolateyAdapter : ISourceAdapter
{
    public string SourceId => "chocolatey";
    public string DisplayName => "Chocolatey";
    public int TrustScore => 4;
    public bool IsAvailable => CliProcessRunner.IsToolAvailable("choco");

    public async Task<List<PackageInfo>> SearchAsync(string query, CancellationToken ct = default)
    {
        var result = await CliProcessRunner.RunAsync("choco", $"search \"{query}\" --limit-output", null, ct);
        if (!result.Success) return new List<PackageInfo>();

        var packages = new List<PackageInfo>();
        foreach (var line in result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split('|');
            if (parts.Length >= 2)
            {
                packages.Add(new PackageInfo
                {
                    Id = parts[0].Trim(),
                    Name = parts[0].Trim(),
                    LatestVersion = parts[1].Trim(),
                    AvailableSources = new List<string> { SourceId },
                    TrustLevel = TrustLevel.High
                });
            }
        }
        return packages;
    }

    public async Task<PackageDetail> GetDetailsAsync(string packageId, CancellationToken ct = default)
    {
        // Choco info gives details, but search is faster for basic resolution
        var search = await SearchAsync(packageId, ct);
        var pkg = search.FirstOrDefault(p => p.Id.Equals(packageId, StringComparison.OrdinalIgnoreCase));
        
        return new PackageDetail
        {
            Id = packageId,
            Name = pkg?.Name ?? packageId,
            LatestVersion = pkg?.LatestVersion ?? "",
            SourceId = SourceId,
            SourceTrustScore = TrustScore,
            SupportsSilent = true
        };
    }

    public async Task<ActionResult> InstallAsync(PackageAction action, IProgress<string> progress, CancellationToken ct = default)
    {
        var result = await CliProcessRunner.RunAsync("choco", $"install {action.PackageId} --yes --no-progress", progress, ct, 300000);
        return CreateResult(result, action, ActionType.Install);
    }

    public async Task<ActionResult> UpdateAsync(PackageAction action, IProgress<string> progress, CancellationToken ct = default)
    {
        var result = await CliProcessRunner.RunAsync("choco", $"upgrade {action.PackageId} --yes --no-progress", progress, ct, 300000);
        return CreateResult(result, action, ActionType.Update);
    }

    public async Task<ActionResult> UninstallAsync(PackageAction action, IProgress<string> progress, CancellationToken ct = default)
    {
        var result = await CliProcessRunner.RunAsync("choco", $"uninstall {action.PackageId} --yes", progress, ct, 300000);
        return CreateResult(result, action, ActionType.Uninstall);
    }

    public async Task<ActionResult> RepairAsync(PackageAction action, IProgress<string> progress, CancellationToken ct = default)
    {
        var result = await CliProcessRunner.RunAsync("choco", $"install {action.PackageId} --yes --force", progress, ct, 300000);
        return CreateResult(result, action, ActionType.Repair);
    }

    public async Task<List<PackageInfo>> GetInstalledPackagesAsync(CancellationToken ct = default)
    {
        var result = await CliProcessRunner.RunAsync("choco", "list --limit-output --local-only", null, ct);
        if (!result.Success) return new List<PackageInfo>();

        var packages = new List<PackageInfo>();
        foreach (var line in result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split('|');
            if (parts.Length >= 2)
            {
                packages.Add(new PackageInfo
                {
                    Id = parts[0].Trim(),
                    Name = parts[0].Trim(),
                    InstalledVersion = parts[1].Trim(),
                    IsInstalled = true,
                    AvailableSources = new List<string> { SourceId }
                });
            }
        }
        return packages;
    }

    private ActionResult CreateResult(ProcessResult result, PackageAction action, ActionType type)
    {
        return new ActionResult
        {
            Success = result.Success,
            ExitCode = result.ExitCode,
            LogOutput = result.StandardOutput + result.StandardError,
            ErrorMessage = result.Success ? null : "Command failed. See log.",
            SourceUsed = SourceId,
            ActionPerformed = type,
            PackageId = action.PackageId
        };
    }
}
