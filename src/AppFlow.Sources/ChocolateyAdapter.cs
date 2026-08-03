namespace AppFlow.Sources;

using AppFlow.Core.Enums;
using AppFlow.Core.Interfaces;
using AppFlow.Core.Models;
using AppFlow.Sources.Helpers;

public sealed class ChocolateyAdapter : ISourceAdapter
{
    private readonly Lazy<bool> _availability =
        new(() => CliProcessRunner.IsToolAvailable("choco"), LazyThreadSafetyMode.ExecutionAndPublication);

    public string SourceId => "chocolatey";
    public string DisplayName => "Chocolatey";
    public int TrustScore => 4;
    public bool IsAvailable => _availability.Value;

    public async Task<List<PackageInfo>> SearchAsync(string query, CancellationToken ct = default)
    {
        var result = await CliProcessRunner.RunAsync(
            "choco", new[] { "search", query, "--limit-output" }, ct: ct).ConfigureAwait(false);
        if (!result.Success) return new List<PackageInfo>();

        return ParsePackageLines(result.StandardOutput)
            .Select(item => CreatePackage(item.Id, item.Version, installed: false))
            .ToList();
    }

    public async Task<PackageDetail?> GetDetailsAsync(string packageId, CancellationToken ct = default)
    {
        var result = await CliProcessRunner.RunAsync(
            "choco",
            new[] { "search", packageId, "--exact", "--limit-output" },
            ct: ct).ConfigureAwait(false);
        if (!result.Success) return null;

        var package = ParsePackageLines(result.StandardOutput)
            .FirstOrDefault(p => p.Id.Equals(packageId, StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(package.Id)) return null;

        return new PackageDetail
        {
            Id = package.Id,
            Name = package.Id,
            LatestVersion = package.Version,
            SourceId = SourceId,
            SourceTrustScore = TrustScore,
            TrustLevel = TrustLevel.High,
            SupportsSilent = true,
            AvailableSources = new List<string> { SourceId }
        };
    }

    public Task<ActionResult> InstallAsync(
        PackageAction action,
        IProgress<string> progress,
        CancellationToken ct = default)
    {
        var args = new List<string> { "install", action.PackageId, "--yes", "--no-progress", "--limit-output" };
        if (!string.IsNullOrWhiteSpace(action.TargetVersion))
        {
            args.Add("--version");
            args.Add(action.TargetVersion);
        }
        return RunActionAsync(args, action, ActionType.Install, progress, ct);
    }

    public Task<ActionResult> UpdateAsync(
        PackageAction action,
        IProgress<string> progress,
        CancellationToken ct = default) =>
        RunActionAsync(
            new[] { "upgrade", action.PackageId, "--yes", "--no-progress", "--limit-output" },
            action,
            ActionType.Update,
            progress,
            ct);

    public Task<ActionResult> UninstallAsync(
        PackageAction action,
        IProgress<string> progress,
        CancellationToken ct = default) =>
        RunActionAsync(
            new[] { "uninstall", action.PackageId, "--yes", "--limit-output" },
            action,
            ActionType.Uninstall,
            progress,
            ct);

    public Task<ActionResult> RepairAsync(
        PackageAction action,
        IProgress<string> progress,
        CancellationToken ct = default) =>
        RunActionAsync(
            new[] { "install", action.PackageId, "--yes", "--force", "--no-progress", "--limit-output" },
            action,
            ActionType.Repair,
            progress,
            ct);

    public async Task<List<PackageInfo>> GetInstalledPackagesAsync(CancellationToken ct = default)
    {
        var installedTask = GetLocalPackagesAsync(ct);
        var outdatedTask = CliProcessRunner.RunAsync(
            "choco",
            new[] { "outdated", "--limit-output", "--ignore-unfound" },
            ct: ct);

        await Task.WhenAll(installedTask, outdatedTask).ConfigureAwait(false);
        var installed = await installedTask.ConfigureAwait(false);
        var outdatedResult = await outdatedTask.ConfigureAwait(false);
        var updates = ParseOutdatedLines(outdatedResult.StandardOutput)
            .ToDictionary(x => x.Id, x => x.AvailableVersion, StringComparer.OrdinalIgnoreCase);

        return installed.Select(item =>
        {
            var package = CreatePackage(item.Id, item.Version, installed: true);
            package.InstalledVersion = item.Version;
            package.LatestVersion = updates.TryGetValue(item.Id, out var latest) ? latest : item.Version;
            package.SupportedActions = new List<ActionType>
            {
                ActionType.Update, ActionType.Uninstall, ActionType.Repair
            };
            return package;
        }).ToList();
    }

    private async Task<List<(string Id, string Version)>> GetLocalPackagesAsync(CancellationToken ct)
    {
        var result = await CliProcessRunner.RunAsync(
            "choco", new[] { "list", "--local-only", "--limit-output" }, ct: ct).ConfigureAwait(false);
        if (!result.Success)
        {
            result = await CliProcessRunner.RunAsync(
                "choco", new[] { "list", "--limit-output" }, ct: ct).ConfigureAwait(false);
        }
        return result.Success ? ParsePackageLines(result.StandardOutput) : new List<(string, string)>();
    }

    private static async Task<ActionResult> RunActionAsync(
        IEnumerable<string> arguments,
        PackageAction action,
        ActionType type,
        IProgress<string> progress,
        CancellationToken ct)
    {
        var args = arguments.ToList();
        var result = await CliProcessRunner.RunAsync(
            "choco", args, progress, ct, timeoutMs: 300000).ConfigureAwait(false);
        return new ActionResult
        {
            Success = result.Success,
            ExitCode = result.ExitCode,
            LogOutput = result.StandardOutput + result.StandardError,
            ErrorMessage = result.Success ? null : "Chocolatey could not complete the requested action.",
            ErrorSuggestion = result.Success ? null : "Review the live log and verify administrator access.",
            SourceUsed = "chocolatey",
            ActionPerformed = type,
            PackageId = action.PackageId,
            InstallerArgs = CliProcessRunner.FormatArguments(args)
        };
    }

    private PackageInfo CreatePackage(string id, string version, bool installed) => new()
    {
        Id = id,
        Name = id,
        LatestVersion = version,
        IsInstalled = installed,
        SourceId = SourceId,
        AvailableSources = new List<string> { SourceId },
        SourcePackageIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [SourceId] = id
        },
        TrustLevel = TrustLevel.High,
        SupportedActions = installed
            ? new List<ActionType> { ActionType.Update, ActionType.Uninstall, ActionType.Repair }
            : new List<ActionType> { ActionType.Install }
    };

    private static List<(string Id, string Version)> ParsePackageLines(string output) =>
        output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => !line.StartsWith("Chocolatey ", StringComparison.OrdinalIgnoreCase))
            .Select(line => line.Split('|'))
            .Where(parts => parts.Length >= 2 && !string.IsNullOrWhiteSpace(parts[0]))
            .Select(parts => (parts[0].Trim(), parts[1].Trim()))
            .ToList();

    private static IEnumerable<(string Id, string AvailableVersion)> ParseOutdatedLines(string output)
    {
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Trim().Split('|');
            if (parts.Length >= 3 && !string.IsNullOrWhiteSpace(parts[0]))
                yield return (parts[0].Trim(), parts[2].Trim());
        }
    }
}
