namespace AppFlow.Sources;

using AppFlow.Core.Enums;
using AppFlow.Core.Interfaces;
using AppFlow.Core.Models;
using AppFlow.Sources.Helpers;

public class ScoopAdapter : ISourceAdapter
{
    public string SourceId => "scoop";
    public string DisplayName => "Scoop";
    public int TrustScore => 3;
    public bool IsAvailable => CliProcessRunner.IsToolAvailable("scoop");

    public async Task<List<PackageInfo>> SearchAsync(string query, CancellationToken ct = default)
    {
        var result = await CliProcessRunner.RunAsync("scoop", $"search \"{query}\"", null, ct);
        if (!result.Success) return new List<PackageInfo>();

        var packages = new List<PackageInfo>();
        var lines = result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            if (line.Contains("WARN") || line.StartsWith("'")) continue;
            var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                packages.Add(new PackageInfo
                {
                    Id = parts[0].Trim(),
                    Name = parts[0].Trim(),
                    LatestVersion = parts[1].Trim(),
                    AvailableSources = new List<string> { SourceId },
                    TrustLevel = TrustLevel.Medium
                });
            }
        }
        return packages;
    }

    public async Task<PackageDetail> GetDetailsAsync(string packageId, CancellationToken ct = default)
    {
        var result = await CliProcessRunner.RunAsync("scoop", $"info {packageId}", null, ct);
        var detail = new PackageDetail { Id = packageId, Name = packageId, SourceId = SourceId, SourceTrustScore = TrustScore };
        
        if (result.Success)
        {
            foreach (var line in result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = line.Split(':', 2);
                if (parts.Length == 2)
                {
                    var key = parts[0].Trim();
                    var val = parts[1].Trim();
                    if (key == "Version") detail.LatestVersion = val;
                    if (key == "Description") detail.Description = val;
                    if (key == "Homepage") detail.Homepage = val;
                    if (key == "License") detail.License = val;
                }
            }
        }
        return detail;
    }

    public async Task<ActionResult> InstallAsync(PackageAction action, IProgress<string> progress, CancellationToken ct = default)
    {
        var result = await CliProcessRunner.RunAsync("scoop", $"install {action.PackageId}", progress, ct, 300000);
        return CreateResult(result, action, ActionType.Install);
    }

    public async Task<ActionResult> UpdateAsync(PackageAction action, IProgress<string> progress, CancellationToken ct = default)
    {
        var result = await CliProcessRunner.RunAsync("scoop", $"update {action.PackageId}", progress, ct, 300000);
        return CreateResult(result, action, ActionType.Update);
    }

    public async Task<ActionResult> UninstallAsync(PackageAction action, IProgress<string> progress, CancellationToken ct = default)
    {
        var result = await CliProcessRunner.RunAsync("scoop", $"uninstall {action.PackageId}", progress, ct, 300000);
        return CreateResult(result, action, ActionType.Uninstall);
    }

    public async Task<ActionResult> RepairAsync(PackageAction action, IProgress<string> progress, CancellationToken ct = default)
    {
        await UninstallAsync(action, progress, ct);
        return await InstallAsync(action, progress, ct);
    }

    public async Task<List<PackageInfo>> GetInstalledPackagesAsync(CancellationToken ct = default)
    {
        var result = await CliProcessRunner.RunAsync("scoop", "list", null, ct);
        if (!result.Success) return new List<PackageInfo>();

        var packages = new List<PackageInfo>();
        var lines = result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(2); // Skip header
        foreach (var line in lines)
        {
            var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
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
