namespace AppFlow.Sources;

using AppFlow.Core.Enums;
using AppFlow.Core.Interfaces;
using AppFlow.Core.Models;
using AppFlow.Sources.Helpers;
using System.Text.RegularExpressions;

public sealed class ScoopAdapter : ISourceAdapter
{
    private static readonly Lazy<string?> ScoopScript =
        new(FindScoopScript, LazyThreadSafetyMode.ExecutionAndPublication);

    public string SourceId => "scoop";
    public string DisplayName => "Scoop";
    public int TrustScore => 3;
    public bool IsAvailable => OperatingSystem.IsWindows() && ScoopScript.Value is not null;

    public async Task<List<PackageInfo>> SearchAsync(string query, CancellationToken ct = default)
    {
        var result = await RunScoopAsync(
            new[] { "search", query }, ct: ct).ConfigureAwait(false);
        if (!result.Success) return new List<PackageInfo>();

        var packages = new List<PackageInfo>();
        foreach (var fields in ParseRows(result.StandardOutput))
        {
            if (fields.Count < 2) continue;
            var id = fields[0];
            packages.Add(CreatePackage(id, fields[1], installed: false));
        }
        return packages;
    }

    public async Task<PackageDetail?> GetDetailsAsync(string packageId, CancellationToken ct = default)
    {
        var result = await RunScoopAsync(
            new[] { "info", packageId }, ct: ct).ConfigureAwait(false);
        if (!result.Success) return null;

        var detail = new PackageDetail
        {
            Id = packageId,
            Name = packageId,
            SourceId = SourceId,
            SourceTrustScore = TrustScore,
            TrustLevel = TrustLevel.Medium,
            SupportsSilent = true,
            AvailableSources = new List<string> { SourceId }
        };

        foreach (var line in result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split(':', 2);
            if (parts.Length != 2) continue;
            var key = parts[0].Trim().ToLowerInvariant();
            var value = parts[1].Trim();
            switch (key)
            {
                case "name": detail.Name = value; break;
                case "version": detail.LatestVersion = value; break;
                case "description": detail.Description = value; break;
                case "website":
                case "homepage": detail.Homepage = value; break;
                case "license": detail.License = value; break;
            }
        }
        return detail;
    }

    public Task<ActionResult> InstallAsync(
        PackageAction action,
        IProgress<string> progress,
        CancellationToken ct = default)
    {
        var package = string.IsNullOrWhiteSpace(action.TargetVersion)
            ? action.PackageId
            : $"{action.PackageId}@{action.TargetVersion}";
        return RunActionAsync(new[] { "install", package }, action, ActionType.Install, progress, ct);
    }

    public Task<ActionResult> UpdateAsync(
        PackageAction action,
        IProgress<string> progress,
        CancellationToken ct = default) =>
        RunActionAsync(new[] { "update", action.PackageId }, action, ActionType.Update, progress, ct);

    public Task<ActionResult> UninstallAsync(
        PackageAction action,
        IProgress<string> progress,
        CancellationToken ct = default) =>
        RunActionAsync(new[] { "uninstall", action.PackageId }, action, ActionType.Uninstall, progress, ct);

    public async Task<ActionResult> RepairAsync(
        PackageAction action,
        IProgress<string> progress,
        CancellationToken ct = default)
    {
        var uninstall = await RunActionAsync(
            new[] { "uninstall", action.PackageId }, action, ActionType.Repair, progress, ct).ConfigureAwait(false);
        if (!uninstall.Success)
        {
            uninstall.ErrorMessage = "Scoop could not remove the existing package during repair.";
            return uninstall;
        }

        return await RunActionAsync(
            new[] { "install", action.PackageId }, action, ActionType.Repair, progress, ct).ConfigureAwait(false);
    }

    public async Task<List<PackageInfo>> GetInstalledPackagesAsync(CancellationToken ct = default)
    {
        var listTask = RunScoopAsync(new[] { "list" }, ct: ct);
        var statusTask = RunScoopAsync(new[] { "status" }, ct: ct);
        await Task.WhenAll(listTask, statusTask).ConfigureAwait(false);

        var listResult = await listTask.ConfigureAwait(false);
        if (!listResult.Success) return new List<PackageInfo>();

        var updates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var statusResult = await statusTask.ConfigureAwait(false);
        if (statusResult.Success)
        {
            foreach (var fields in ParseRows(statusResult.StandardOutput))
            {
                if (fields.Count >= 3)
                    updates[fields[0]] = fields[2];
            }
        }

        var packages = new List<PackageInfo>();
        foreach (var fields in ParseRows(listResult.StandardOutput))
        {
            if (fields.Count < 2) continue;
            var package = CreatePackage(fields[0], fields[1], installed: true);
            package.InstalledVersion = fields[1];
            package.LatestVersion = updates.TryGetValue(fields[0], out var latest) ? latest : fields[1];
            packages.Add(package);
        }
        return packages;
    }

    private PackageInfo CreatePackage(string id, string version, bool installed) => new()
    {
        Id = id,
        Name = id,
        InstalledVersion = installed ? version : null,
        LatestVersion = version,
        IsInstalled = installed,
        SourceId = SourceId,
        AvailableSources = new List<string> { SourceId },
        SourcePackageIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [SourceId] = id
        },
        TrustLevel = TrustLevel.Medium,
        SupportedActions = installed
            ? new List<ActionType> { ActionType.Update, ActionType.Uninstall, ActionType.Repair }
            : new List<ActionType> { ActionType.Install }
    };

    private static async Task<ActionResult> RunActionAsync(
        IEnumerable<string> arguments,
        PackageAction action,
        ActionType type,
        IProgress<string> progress,
        CancellationToken ct)
    {
        var args = arguments.ToList();
        var result = await RunScoopAsync(
            args, progress, ct, timeoutMs: 300000).ConfigureAwait(false);
        return new ActionResult
        {
            Success = result.Success,
            ExitCode = result.ExitCode,
            LogOutput = result.StandardOutput + result.StandardError,
            ErrorMessage = result.Success ? null : "Scoop could not complete the requested action.",
            ErrorSuggestion = result.Success ? null : "Review the live log and run 'scoop checkup'.",
            SourceUsed = "scoop",
            ActionPerformed = type,
            PackageId = action.PackageId,
            InstallerArgs = CliProcessRunner.FormatArguments(args)
        };
    }

    private static Task<ProcessResult> RunScoopAsync(
        IEnumerable<string> arguments,
        IProgress<string>? progress = null,
        CancellationToken ct = default,
        int timeoutMs = 30000)
    {
        var script = ScoopScript.Value
            ?? throw new InvalidOperationException("Scoop could not be found for the current user.");
        var powershellArguments = new List<string>
        {
            "-NoLogo", "-NoProfile", "-NonInteractive",
            "-ExecutionPolicy", "Bypass", "-File", script
        };
        powershellArguments.AddRange(arguments);
        return CliProcessRunner.RunAsync(
            "powershell.exe", powershellArguments, progress, ct, timeoutMs);
    }

    private static string? FindScoopScript()
    {
        var roots = new[]
        {
            Environment.GetEnvironmentVariable("SCOOP"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "scoop")
        };

        foreach (var root in roots.Where(root => !string.IsNullOrWhiteSpace(root)))
        {
            var candidates = new[]
            {
                Path.Combine(root!, "apps", "scoop", "current", "bin", "scoop.ps1"),
                Path.Combine(root!, "shims", "scoop.ps1")
            };
            var match = candidates.FirstOrDefault(File.Exists);
            if (match is not null) return match;
        }
        return null;
    }

    private static IEnumerable<IReadOnlyList<string>> ParseRows(string output)
    {
        var afterHeader = false;
        foreach (var rawLine in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim().TrimEnd('\r');
            if (line.StartsWith("WARN", StringComparison.OrdinalIgnoreCase)) continue;
            if (line.Length >= 3 && line.All(c => c is '-' or ' '))
            {
                afterHeader = true;
                continue;
            }
            if (!afterHeader) continue;

            var fields = Regex.Split(line, @"\s{2,}")
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToList();
            if (fields.Count < 2)
                fields = line.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
            if (fields.Count >= 2)
                yield return fields;
        }
    }
}
