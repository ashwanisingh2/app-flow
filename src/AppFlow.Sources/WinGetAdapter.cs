namespace AppFlow.Sources;

using AppFlow.Core.Enums;
using AppFlow.Core.Interfaces;
using AppFlow.Core.Models;
using AppFlow.Sources.Helpers;
using System.Text.RegularExpressions;

public sealed class WinGetAdapter : ISourceAdapter
{
    private readonly Lazy<bool> _availability =
        new(() => CliProcessRunner.IsToolAvailable("winget"), LazyThreadSafetyMode.ExecutionAndPublication);

    public string SourceId => "winget";
    public string DisplayName => "Windows Package Manager (WinGet)";
    public int TrustScore => 5;
    public bool IsAvailable => _availability.Value;

    public async Task<List<PackageInfo>> SearchAsync(string query, CancellationToken ct = default)
    {
        var result = await CliProcessRunner.RunAsync(
            "winget",
            new[] { "search", query, "--source", "winget", "--accept-source-agreements", "--disable-interactivity" },
            ct: ct).ConfigureAwait(false);
        if (!result.Success) return new List<PackageInfo>();

        return ParseTabularOutput(result.StandardOutput)
            .Select(row => new PackageInfo
            {
                Id = Value(row, "Id", 1),
                Name = Value(row, "Name", 0),
                LatestVersion = NullIfEmpty(Value(row, "Version", 2)),
                SourceId = SourceId,
                AvailableSources = new List<string> { SourceId },
                SourcePackageIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [SourceId] = Value(row, "Id", 1)
                },
                TrustLevel = TrustLevel.Verified,
                SupportedActions = new List<ActionType> { ActionType.Install }
            })
            .Where(p => !string.IsNullOrWhiteSpace(p.Id))
            .ToList();
    }

    public async Task<PackageDetail?> GetDetailsAsync(string packageId, CancellationToken ct = default)
    {
        var result = await CliProcessRunner.RunAsync(
            "winget",
            new[]
            {
                "show", "--id", packageId, "--exact", "--source", "winget",
                "--accept-source-agreements", "--disable-interactivity"
            },
            ct: ct).ConfigureAwait(false);
        if (!result.Success) return null;

        var detail = new PackageDetail
        {
            Id = packageId,
            Name = packageId,
            SourceId = SourceId,
            SourceTrustScore = TrustScore,
            IsOfficialSource = true,
            TrustLevel = TrustLevel.Verified,
            SupportsSilent = true,
            AvailableSources = new List<string> { SourceId }
        };

        foreach (var rawLine in result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.TrimEnd('\r');
            var found = Regex.Match(line, @"^Found\s+(.+?)\s+\[[^]]+\]", RegexOptions.IgnoreCase);
            if (found.Success)
            {
                detail.Name = found.Groups[1].Value.Trim();
                continue;
            }

            var match = Regex.Match(line, @"^\s*([^:]+):\s*(.*)$");
            if (!match.Success) continue;

            var key = match.Groups[1].Value.Trim();
            var value = match.Groups[2].Value.Trim();
            switch (key.ToLowerInvariant())
            {
                case "version": detail.LatestVersion = value; break;
                case "publisher": detail.Publisher = value; break;
                case "description": detail.Description = value; break;
                case "homepage": detail.Homepage = value; break;
                case "license": detail.License = value; break;
                case "installer sha256": detail.ExpectedHash = value; break;
                case "installer url": detail.DownloadUrl = value; break;
                case "release notes": detail.ReleaseNotes = value; break;
            }
        }

        // A manifest hash proves integrity, not an Authenticode signature.
        detail.IsSigned = false;
        detail.IsTrustedPublisher = false;
        return detail;
    }

    public Task<ActionResult> InstallAsync(
        PackageAction action,
        IProgress<string> progress,
        CancellationToken ct = default)
    {
        var args = BaseActionArguments("install", action);
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
        RunActionAsync(BaseActionArguments("upgrade", action), action, ActionType.Update, progress, ct);

    public Task<ActionResult> UninstallAsync(
        PackageAction action,
        IProgress<string> progress,
        CancellationToken ct = default)
    {
        var args = new List<string>
        {
            "uninstall", "--id", action.PackageId, "--exact", "--source", "winget",
            "--accept-source-agreements", "--disable-interactivity"
        };
        if (action.SupportsSilent) args.Add("--silent");
        return RunActionAsync(args, action, ActionType.Uninstall, progress, ct);
    }

    public Task<ActionResult> RepairAsync(
        PackageAction action,
        IProgress<string> progress,
        CancellationToken ct = default) =>
        RunActionAsync(BaseActionArguments("repair", action), action, ActionType.Repair, progress, ct);

    public async Task<List<PackageInfo>> GetInstalledPackagesAsync(CancellationToken ct = default)
    {
        var result = await CliProcessRunner.RunAsync(
            "winget",
            new[] { "list", "--source", "winget", "--accept-source-agreements", "--disable-interactivity" },
            ct: ct).ConfigureAwait(false);
        if (!result.Success) return new List<PackageInfo>();

        return ParseTabularOutput(result.StandardOutput)
            .Select(row =>
            {
                var id = Value(row, "Id", 1);
                return new PackageInfo
                {
                    Id = id,
                    Name = Value(row, "Name", 0),
                    InstalledVersion = NullIfEmpty(Value(row, "Version", 2)),
                    LatestVersion = row.TryGetValue("Available", out var available)
                        ? NullIfEmpty(available)
                        : null,
                    IsInstalled = true,
                    SourceId = SourceId,
                    AvailableSources = new List<string> { SourceId },
                    SourcePackageIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        [SourceId] = id
                    },
                    TrustLevel = TrustLevel.Verified,
                    SupportedActions = new List<ActionType>
                    {
                        ActionType.Update, ActionType.Uninstall, ActionType.Repair
                    }
                };
            })
            .Where(p => !string.IsNullOrWhiteSpace(p.Id))
            .ToList();
    }

    internal static List<IReadOnlyDictionary<string, string>> ParseTabularOutput(string output)
    {
        var lines = output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => StripAnsi(l).TrimEnd('\r', ' '))
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        var separatorIndex = lines.FindIndex(IsSeparator);
        if (separatorIndex <= 0) return new List<IReadOnlyDictionary<string, string>>();

        var header = lines[separatorIndex - 1];
        var headerTokens = Regex.Matches(header, @"\S+")
            .Select(m => (Name: m.Value, Start: m.Index))
            .ToList();
        if (headerTokens.Count < 2) return new List<IReadOnlyDictionary<string, string>>();

        var columns = headerTokens
            .Select((token, index) => (
                token.Name,
                token.Start,
                Length: index + 1 < headerTokens.Count
                    ? headerTokens[index + 1].Start - token.Start
                    : int.MaxValue))
            .ToList();

        var rows = new List<IReadOnlyDictionary<string, string>>();
        foreach (var line in lines.Skip(separatorIndex + 1))
        {
            if (IsSeparator(line)) continue;
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var ordered = new List<string>();

            foreach (var column in columns)
            {
                var value = column.Start >= line.Length
                    ? string.Empty
                    : line.Substring(
                        column.Start,
                        Math.Min(column.Length, line.Length - column.Start)).Trim();
                values[column.Name] = value;
                ordered.Add(value);
            }

            for (var i = 0; i < ordered.Count; i++)
                values[$"#{i}"] = ordered[i];
            rows.Add(values);
        }
        return rows;
    }

    private static async Task<ActionResult> RunActionAsync(
        IReadOnlyList<string> args,
        PackageAction action,
        ActionType actionType,
        IProgress<string> progress,
        CancellationToken ct)
    {
        var result = await CliProcessRunner.RunAsync(
            "winget", args, progress, ct, timeoutMs: 300000).ConfigureAwait(false);
        return new ActionResult
        {
            Success = result.Success,
            ExitCode = result.ExitCode,
            LogOutput = result.StandardOutput + result.StandardError,
            ErrorMessage = result.Success ? null : "WinGet could not complete the requested action.",
            ErrorSuggestion = result.Success ? null : ParseErrorSuggestion(result.StandardError),
            SourceUsed = "winget",
            ActionPerformed = actionType,
            PackageId = action.PackageId,
            InstallerArgs = CliProcessRunner.FormatArguments(args)
        };
    }

    private static List<string> BaseActionArguments(string verb, PackageAction action)
    {
        var args = new List<string>
        {
            verb, "--id", action.PackageId, "--exact", "--source", "winget",
            "--accept-source-agreements", "--accept-package-agreements", "--disable-interactivity"
        };
        if (action.SupportsSilent) args.Add("--silent");
        return args;
    }

    private static string Value(IReadOnlyDictionary<string, string> row, string name, int index) =>
        row.TryGetValue(name, out var named)
            ? named
            : row.TryGetValue($"#{index}", out var positional) ? positional : string.Empty;

    private static string? NullIfEmpty(string value) =>
        string.IsNullOrWhiteSpace(value) || value == "-" ? null : value;

    private static bool IsSeparator(string line)
    {
        var trimmed = line.Trim();
        return trimmed.Length >= 3 && trimmed.All(c => c is '-' or ' ');
    }

    private static string StripAnsi(string value) =>
        Regex.Replace(value, "\\x1B(?:[@-Z\\\\-_]|\\[[0-?]*[ -/]*[@-~])", string.Empty);

    private static string ParseErrorSuggestion(string stderr)
    {
        if (stderr.Contains("admin", StringComparison.OrdinalIgnoreCase)
            || stderr.Contains("elevation", StringComparison.OrdinalIgnoreCase))
            return "Administrator privileges are required. Restart AppFlow as administrator.";
        if (stderr.Contains("network", StringComparison.OrdinalIgnoreCase)
            || stderr.Contains("internet", StringComparison.OrdinalIgnoreCase))
            return "Check your internet connection and try again.";
        if (stderr.Contains("not found", StringComparison.OrdinalIgnoreCase))
            return "Refresh the source and verify the package ID.";
        return "Review the live log, refresh WinGet sources, and try again.";
    }
}
