namespace AppFlow.Sources;

using AppFlow.Core.Enums;
using AppFlow.Core.Interfaces;
using AppFlow.Core.Models;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

public class GitHubAdapter : ISourceAdapter
{
    public string SourceId => "github";
    public string DisplayName => "GitHub Releases";
    public int TrustScore => 2;
    public bool IsAvailable => true;

    private static readonly HttpClient _http = new() { DefaultRequestHeaders = { { "User-Agent", "AppFlow-Client" } } };

    public Task<List<PackageInfo>> SearchAsync(string query, CancellationToken ct = default)
    {
        // GitHub doesn't have a simple package search without knowing the repo format
        return Task.FromResult(new List<PackageInfo>());
    }

    public async Task<PackageDetail> GetDetailsAsync(string packageId, CancellationToken ct = default)
    {
        var detail = new PackageDetail { Id = packageId, SourceId = SourceId, SourceTrustScore = TrustScore };
        try
        {
            var url = $"https://api.github.com/repos/{packageId}/releases/latest";
            var release = await _http.GetFromJsonAsync<JsonElement>(url, ct);
            
            detail.LatestVersion = release.GetProperty("tag_name").GetString() ?? "latest";
            detail.Name = release.GetProperty("name").GetString() ?? packageId;
            detail.Description = release.GetProperty("body").GetString() ?? "";
            
            if (release.TryGetProperty("assets", out var assets))
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.GetProperty("name").GetString() ?? "";
                    if (name.EndsWith(".exe") || name.EndsWith(".msi"))
                    {
                        detail.DownloadUrl = asset.GetProperty("browser_download_url").GetString();
                        break;
                    }
                }
            }
        }
        catch { }
        return detail;
    }

    public async Task<ActionResult> InstallAsync(PackageAction action, IProgress<string> progress, CancellationToken ct = default)
    {
        progress?.Report("Fetching release info from GitHub...");
        var detail = await GetDetailsAsync(action.PackageId, ct);
        
        if (string.IsNullOrEmpty(detail.DownloadUrl))
            return new ActionResult { Success = false, ErrorMessage = "No suitable installer (.exe/.msi) found in latest release.", ActionPerformed = ActionType.Install, PackageId = action.PackageId, SourceUsed = SourceId };

        var tempFile = Path.Combine(Path.GetTempPath(), Path.GetFileName(detail.DownloadUrl));
        
        try
        {
            progress?.Report($"Downloading {detail.DownloadUrl}...");
            var bytes = await _http.GetByteArrayAsync(detail.DownloadUrl, ct);
            await File.WriteAllBytesAsync(tempFile, bytes, ct);

            progress?.Report("Running installer...");
            var psi = new ProcessStartInfo { FileName = tempFile, UseShellExecute = true };
            if (action.SupportsSilent) psi.Arguments = "/S /quiet /norestart"; // Best effort silent flags
            
            using var process = Process.Start(psi);
            if (process != null) await process.WaitForExitAsync(ct);

            return new ActionResult { Success = true, LogOutput = "Installation completed.", ActionPerformed = ActionType.Install, PackageId = action.PackageId, SourceUsed = SourceId };
        }
        catch (Exception ex)
        {
            return new ActionResult { Success = false, ErrorMessage = ex.Message, ActionPerformed = ActionType.Install, PackageId = action.PackageId, SourceUsed = SourceId };
        }
    }

    public Task<ActionResult> UpdateAsync(PackageAction action, IProgress<string> progress, CancellationToken ct = default)
        => InstallAsync(action, progress, ct);

    public Task<ActionResult> UninstallAsync(PackageAction action, IProgress<string> progress, CancellationToken ct = default)
        => Task.FromResult(new ActionResult { Success = false, ErrorMessage = "Uninstall not supported via GitHub adapter. Use Windows Settings.", ActionPerformed = ActionType.Uninstall, PackageId = action.PackageId, SourceUsed = SourceId });

    public Task<ActionResult> RepairAsync(PackageAction action, IProgress<string> progress, CancellationToken ct = default)
        => InstallAsync(action, progress, ct);

    public Task<List<PackageInfo>> GetInstalledPackagesAsync(CancellationToken ct = default)
        => Task.FromResult(new List<PackageInfo>());
}
