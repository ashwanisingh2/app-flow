namespace AppFlow.Sources;

using AppFlow.Core.Enums;
using AppFlow.Core.Interfaces;
using AppFlow.Core.Models;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

public sealed class GitHubAdapter : ISourceAdapter
{
    private static readonly Regex RepositoryId = new(
        @"^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly HttpClient HttpClient = CreateHttpClient();
    private readonly IInstallerIntegrityVerifier _integrityVerifier;

    public GitHubAdapter(IInstallerIntegrityVerifier integrityVerifier) =>
        _integrityVerifier = integrityVerifier;

    public string SourceId => "github";
    public string DisplayName => "GitHub Releases";
    public int TrustScore => 2;
    public bool IsAvailable => true;

    public Task<List<PackageInfo>> SearchAsync(string query, CancellationToken ct = default) =>
        Task.FromResult(new List<PackageInfo>());

    public async Task<PackageDetail?> GetDetailsAsync(string packageId, CancellationToken ct = default)
    {
        if (!RepositoryId.IsMatch(packageId)) return null;

        try
        {
            var url = $"https://api.github.com/repos/{packageId}/releases/latest";
            using var response = await HttpClient.GetAsync(url, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return null;

            var release = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct)
                .ConfigureAwait(false);
            if (release.ValueKind != JsonValueKind.Object) return null;

            var detail = new PackageDetail
            {
                Id = packageId,
                SourceId = SourceId,
                SourceTrustScore = TrustScore,
                TrustLevel = TrustLevel.Low,
                Name = release.TryGetProperty("name", out var name)
                    ? name.GetString() ?? packageId
                    : packageId,
                LatestVersion = release.TryGetProperty("tag_name", out var tag)
                    ? tag.GetString() ?? "latest"
                    : "latest",
                Description = release.TryGetProperty("body", out var body)
                    ? body.GetString() ?? string.Empty
                    : string.Empty,
                Homepage = $"https://github.com/{packageId}",
                AvailableSources = new List<string> { SourceId }
            };

            if (release.TryGetProperty("assets", out var assets))
            {
                var installer = assets.EnumerateArray()
                    .Select(asset => new
                    {
                        Name = asset.TryGetProperty("name", out var assetName)
                            ? assetName.GetString() ?? string.Empty
                            : string.Empty,
                        Url = asset.TryGetProperty("browser_download_url", out var assetUrl)
                            ? assetUrl.GetString()
                            : null
                    })
                    .Where(asset => asset.Url is not null
                                    && (asset.Name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase)
                                        || asset.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)))
                    .OrderBy(asset => asset.Name.Contains("x64", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                    .FirstOrDefault();

                detail.DownloadUrl = installer?.Url;
                detail.SupportsSilent = installer?.Name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase) == true;
            }

            return detail;
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public Task<ActionResult> InstallAsync(
        PackageAction action,
        IProgress<string> progress,
        CancellationToken ct = default) =>
        DownloadAndRunAsync(action, ActionType.Install, progress, ct);

    public Task<ActionResult> UpdateAsync(
        PackageAction action,
        IProgress<string> progress,
        CancellationToken ct = default) =>
        DownloadAndRunAsync(action, ActionType.Update, progress, ct);

    public Task<ActionResult> UninstallAsync(
        PackageAction action,
        IProgress<string> progress,
        CancellationToken ct = default) =>
        Task.FromResult(Failure(
            action,
            ActionType.Uninstall,
            "GitHub releases cannot reliably uninstall an application.",
            "Use the original package manager or Windows Settings."));

    public Task<ActionResult> RepairAsync(
        PackageAction action,
        IProgress<string> progress,
        CancellationToken ct = default) =>
        DownloadAndRunAsync(action, ActionType.Repair, progress, ct);

    public Task<List<PackageInfo>> GetInstalledPackagesAsync(CancellationToken ct = default) =>
        Task.FromResult(new List<PackageInfo>());

    private async Task<ActionResult> DownloadAndRunAsync(
        PackageAction action,
        ActionType type,
        IProgress<string> progress,
        CancellationToken ct)
    {
        progress.Report("Fetching release information from GitHub...");
        var detail = await GetDetailsAsync(action.PackageId, ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(detail?.DownloadUrl))
        {
            return Failure(
                action,
                type,
                "No supported .exe or .msi installer was found in the latest release.");
        }

        var extension = Path.GetExtension(new Uri(detail.DownloadUrl).AbsolutePath);
        var tempFile = Path.Combine(Path.GetTempPath(), $"AppFlow-{Guid.NewGuid():N}{extension}");

        try
        {
            progress.Report("Downloading installer...");
            using (var response = await HttpClient.GetAsync(
                       detail.DownloadUrl,
                       HttpCompletionOption.ResponseHeadersRead,
                       ct).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                await using var input = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                await using var output = new FileStream(
                    tempFile,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    81920,
                    useAsync: true);
                await input.CopyToAsync(output, ct).ConfigureAwait(false);
            }

            var signatureTrusted = _integrityVerifier.HasTrustedAuthenticodeSignature(tempFile);
            if (!signatureTrusted && !action.UserConfirmedRisk)
            {
                return Failure(
                    action,
                    type,
                    "The downloaded GitHub installer does not have a trusted Authenticode signature.",
                    "Only continue after verifying the repository and publisher.");
            }

            progress.Report(signatureTrusted
                ? "Authenticode signature verified."
                : "Warning: continuing with the unsigned installer after confirmation.");
            progress.Report("Starting installer...");

            var startInfo = CreateInstallerStartInfo(tempFile, action.SupportsSilent);
            using var process = Process.Start(startInfo);
            if (process is null)
                return Failure(action, type, "Windows could not start the downloaded installer.");

            await process.WaitForExitAsync(ct).ConfigureAwait(false);
            var success = process.ExitCode is 0 or 1641 or 3010;
            return new ActionResult
            {
                Success = success,
                ExitCode = process.ExitCode,
                LogOutput = success ? "Installer completed successfully." : "Installer returned a failure exit code.",
                ErrorMessage = success ? null : "The installer did not complete successfully.",
                ErrorSuggestion = success ? null : "Review the installer UI or run AppFlow as administrator.",
                ActionPerformed = type,
                PackageId = action.PackageId,
                SourceUsed = SourceId
            };
        }
        finally
        {
            TryDelete(tempFile);
        }
    }

    private static ProcessStartInfo CreateInstallerStartInfo(string installerPath, bool silent)
    {
        if (installerPath.EndsWith(".msi", StringComparison.OrdinalIgnoreCase))
        {
            var msi = new ProcessStartInfo
            {
                FileName = "msiexec.exe",
                UseShellExecute = true
            };
            msi.ArgumentList.Add("/i");
            msi.ArgumentList.Add(installerPath);
            if (silent)
            {
                msi.ArgumentList.Add("/qn");
                msi.ArgumentList.Add("/norestart");
            }
            return msi;
        }

        var executable = new ProcessStartInfo
        {
            FileName = installerPath,
            UseShellExecute = true
        };
        if (silent) executable.ArgumentList.Add("/S");
        return executable;
    }

    private static ActionResult Failure(
        PackageAction action,
        ActionType type,
        string message,
        string? suggestion = null) => new()
    {
        Success = false,
        ExitCode = -1,
        ErrorMessage = message,
        ErrorSuggestion = suggestion,
        ActionPerformed = type,
        PackageId = action.PackageId,
        SourceUsed = "github"
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
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("AppFlow", "1.0"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }
}
