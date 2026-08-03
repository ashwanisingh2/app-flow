# Source Adapter Guide

A package source is added by implementing `AppFlow.Core.Interfaces.ISourceAdapter` in `AppFlow.Sources`.

```csharp
public sealed class MySourceAdapter : ISourceAdapter
{
    public string SourceId => "my-source";
    public string DisplayName => "My Source";
    public int TrustScore => 3; // 0-5
    public bool IsAvailable => CheckAvailabilityOnce();

    public Task<List<PackageInfo>> SearchAsync(string query, CancellationToken ct = default) { /* ... */ }
    public Task<PackageDetail?> GetDetailsAsync(string packageId, CancellationToken ct = default) { /* ... */ }
    public Task<ActionResult> InstallAsync(PackageAction action, IProgress<string> progress, CancellationToken ct = default) { /* ... */ }
    public Task<ActionResult> UpdateAsync(PackageAction action, IProgress<string> progress, CancellationToken ct = default) { /* ... */ }
    public Task<ActionResult> UninstallAsync(PackageAction action, IProgress<string> progress, CancellationToken ct = default) { /* ... */ }
    public Task<ActionResult> RepairAsync(PackageAction action, IProgress<string> progress, CancellationToken ct = default) { /* ... */ }
    public Task<List<PackageInfo>> GetInstalledPackagesAsync(CancellationToken ct = default) { /* ... */ }
}
```

`GetDetailsAsync` must return `null` when the exact package does not exist. Returning an empty detail object creates false-positive source matches.

## Safe CLI invocation

Never build one interpolated command string. Pass each argument separately so package IDs and search terms cannot become options:

```csharp
var result = await CliProcessRunner.RunAsync(
    "mytool.exe",
    new[] { "install", action.PackageId, "--yes" },
    progress,
    ct,
    timeoutMs: 300_000);
```

Adapters should:

- honor cancellation;
- stream both stdout and stderr;
- use the process exit code as the source of success;
- return the requested `ActionType` (including Update and Repair);
- avoid buffering installer downloads in memory;
- return plain-language errors and suggestions;
- set `SourceId`, `AvailableSources`, and `SourcePackageIds` on every `PackageInfo`.

## Registration

Register an adapter as a singleton in `AppFlow.UI/App.xaml.cs`:

```csharp
services.AddSingleton<ISourceAdapter, MySourceAdapter>();
```

`SourceResolver`, `PackageService`, and the Sources settings page will discover it automatically.
