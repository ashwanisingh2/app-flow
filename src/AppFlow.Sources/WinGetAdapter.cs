namespace AppFlow.Sources;

using AppFlow.Core.Enums;
using AppFlow.Core.Interfaces;
using AppFlow.Core.Models;
using AppFlow.Sources.Helpers;
using System.Text.RegularExpressions;

public class WinGetAdapter : ISourceAdapter
{
    public string SourceId => "winget";
    public string DisplayName => "Windows Package Manager (WinGet)";
    public int TrustScore => 5;
    public bool IsAvailable => CliProcessRunner.IsToolAvailable("winget");

    public async Task<List<PackageInfo>> SearchAsync(string query, CancellationToken ct = default)
    {
        var result = await CliProcessRunner.RunAsync("winget", $"search \"{query}\" --accept-source-agreements --disable-interactivity", null, ct);
        if (!result.Success) return new List<PackageInfo>();

        return ParseTabularOutput(result.StandardOutput)
            .Select(row => new PackageInfo
            {
                Id = row.ContainsKey("Id") ? row["Id"] : row.Values.ElementAtOrDefault(1) ?? "",
                Name = row.ContainsKey("Name") ? row["Name"] : row.Values.FirstOrDefault() ?? "",
                LatestVersion = row.ContainsKey("Version") ? row["Version"] : "",
                AvailableSources = new List<string> { SourceId },
                TrustLevel = TrustLevel.Verified
            })
            .Where(p => !string.IsNullOrEmpty(p.Id))
            .ToList();
    }

    public async Task<PackageDetail> GetDetailsAsync(string packageId, CancellationToken ct = default)
    {
        var result = await CliProcessRunner.RunAsync("winget", $"show --id {packageId} --accept-source-agreements --disable-interactivity", null, ct);
        var detail = new PackageDetail { Id = packageId, SourceId = SourceId, SourceTrustScore = TrustScore, IsOfficialSource = true };

        if (!result.Success) return detail;

        var lines = result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var match = Regex.Match(line, @"^([^:]+):\s*(.+)$");
            if (match.Success)
            {
                var key = match.Groups[1].Value.Trim();
                var value = match.Groups[2].Value.Trim();
                switch (key)
                {
                    case "Version": detail.LatestVersion = value; break;
                    case "Publisher": detail.Publisher = value; break;
                    case "Description": detail.Description = value; break;
                    case "Homepage": detail.Homepage = value; break;
                    case "License": detail.License = value; break;
                    case "Installer SHA256": detail.ExpectedHash = value; break;
                    case "Installer Url": detail.DownloadUrl = value; break;
                }
            }
        }
        detail.IsSigned = !string.IsNullOrEmpty(detail.ExpectedHash);
        detail.SupportsSilent = true;
        return detail;
    }

    public async Task<ActionResult> InstallAsync(PackageAction action, IProgress<string> progress, CancellationToken ct = default)
    {
        var args = $"install --id {action.PackageId} --accept-source-agreements --accept-package-agreements --disable-interactivity";
        if (action.SupportsSilent) args += " --silent";
        if (!string.IsNullOrEmpty(action.TargetVersion)) args += $" --version {action.TargetVersion}";

        var result = await CliProcessRunner.RunAsync("winget", args, progress, ct, 300000);
        return new ActionResult
        {
            Success = result.Success,
            ExitCode = result.ExitCode,
            LogOutput = result.StandardOutput + result.StandardError,
            ErrorMessage = result.Success ? null : ParseErrorSuggestion(result.ExitCode, result.StandardError),
            SourceUsed = SourceId,
            ActionPerformed = ActionType.Install,
            PackageId = action.PackageId,
            InstallerArgs = args
        };
    }

    public async Task<ActionResult> UpdateAsync(PackageAction action, IProgress<string> progress, CancellationToken ct = default)
    {
        var args = $"upgrade --id {action.PackageId} --accept-source-agreements --accept-package-agreements --disable-interactivity";
        if (action.SupportsSilent) args += " --silent";
        
        var result = await CliProcessRunner.RunAsync("winget", args, progress, ct, 300000);
        return new ActionResult
        {
            Success = result.Success,
            ExitCode = result.ExitCode,
            LogOutput = result.StandardOutput + result.StandardError,
            ErrorMessage = result.Success ? null : ParseErrorSuggestion(result.ExitCode, result.StandardError),
            SourceUsed = SourceId,
            ActionPerformed = ActionType.Update,
            PackageId = action.PackageId,
            InstallerArgs = args
        };
    }

    public async Task<ActionResult> UninstallAsync(PackageAction action, IProgress<string> progress, CancellationToken ct = default)
    {
        var args = $"uninstall --id {action.PackageId} --accept-source-agreements --disable-interactivity";
        if (action.SupportsSilent) args += " --silent";

        var result = await CliProcessRunner.RunAsync("winget", args, progress, ct, 300000);
        return new ActionResult
        {
            Success = result.Success,
            ExitCode = result.ExitCode,
            LogOutput = result.StandardOutput + result.StandardError,
            ErrorMessage = result.Success ? null : ParseErrorSuggestion(result.ExitCode, result.StandardError),
            SourceUsed = SourceId,
            ActionPerformed = ActionType.Uninstall,
            PackageId = action.PackageId,
            InstallerArgs = args
        };
    }

    public Task<ActionResult> RepairAsync(PackageAction action, IProgress<string> progress, CancellationToken ct = default)
    {
        return Task.FromResult(new ActionResult
        {
            Success = false,
            ErrorMessage = "Repair not supported for WinGet",
            SourceUsed = SourceId,
            ActionPerformed = ActionType.Repair,
            PackageId = action.PackageId
        });
    }

    public async Task<List<PackageInfo>> GetInstalledPackagesAsync(CancellationToken ct = default)
    {
        var result = await CliProcessRunner.RunAsync("winget", "list --accept-source-agreements --disable-interactivity", null, ct);
        if (!result.Success) return new List<PackageInfo>();

        return ParseTabularOutput(result.StandardOutput)
            .Select(row => new PackageInfo
            {
                Id = row.ContainsKey("Id") ? row["Id"] : "",
                Name = row.ContainsKey("Name") ? row["Name"] : "",
                InstalledVersion = row.ContainsKey("Version") ? row["Version"] : "",
                LatestVersion = row.ContainsKey("Available") ? row["Available"] : "",
                IsInstalled = true,
                AvailableSources = new List<string> { SourceId }
            })
            .Where(p => !string.IsNullOrEmpty(p.Id))
            .ToList();
    }

    private List<Dictionary<string, string>> ParseTabularOutput(string output)
    {
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                          .Select(l => l.TrimEnd())
                          .ToList();
        
        var dashLineIdx = lines.FindIndex(l => l.StartsWith("---"));
        if (dashLineIdx <= 0) return new List<Dictionary<string, string>>();

        var headerLine = lines[dashLineIdx - 1];
        var dashes = lines[dashLineIdx];
        var columns = new List<(string Name, int Start, int Length)>();

        int start = 0;
        var parts = dashes.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            var len = part.Length;
            var name = headerLine.Substring(start, Math.Min(len, headerLine.Length - start)).Trim();
            columns.Add((name, start, len));
            start += len + 1; // +1 for the space
        }

        var results = new List<Dictionary<string, string>>();
        for (int i = dashLineIdx + 1; i < lines.Count; i++)
        {
            var line = lines[i];
            var dict = new Dictionary<string, string>();
            foreach (var col in columns)
            {
                if (col.Start < line.Length)
                {
                    var val = line.Substring(col.Start, Math.Min(col.Length, line.Length - col.Start)).Trim();
                    dict[col.Name] = val;
                }
            }
            results.Add(dict);
        }
        return results;
    }

    private string? ParseErrorSuggestion(int exitCode, string stderr)
    {
        if (stderr.Contains("admin", StringComparison.OrdinalIgnoreCase))
            return "Administrator privileges required. Please restart AppFlow as Admin.";
        if (stderr.Contains("network", StringComparison.OrdinalIgnoreCase))
            return "Network error. Please check your internet connection.";
        return "Command failed. See log output for details.";
    }
}
