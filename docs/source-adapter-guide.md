# Source Adapter Guide

AppFlow allows extending package sources easily by implementing the `ISourceAdapter` interface.

## 1. Create your Adapter Class
Create a new class in the `AppFlow.Sources` project and implement `AppFlow.Core.Interfaces.ISourceAdapter`:

```csharp
public class MyCustomAdapter : ISourceAdapter
{
    public string SourceId => "mycustom";
    public string DisplayName => "My Custom Source";
    public int TrustScore => 3; // 1 to 5 scale
    public bool IsAvailable => true; // Perform check here

    public async Task<List<PackageInfo>> SearchAsync(string query, CancellationToken ct) { ... }
    public async Task<PackageDetail> GetDetailsAsync(string packageId, CancellationToken ct) { ... }
    public async Task<ActionResult> InstallAsync(PackageAction action, IProgress<string> progress, CancellationToken ct) { ... }
    public async Task<ActionResult> UpdateAsync(PackageAction action, IProgress<string> progress, CancellationToken ct) { ... }
    public async Task<ActionResult> UninstallAsync(PackageAction action, IProgress<string> progress, CancellationToken ct) { ... }
    public async Task<ActionResult> RepairAsync(PackageAction action, IProgress<string> progress, CancellationToken ct) { ... }
    public async Task<List<PackageInfo>> GetInstalledPackagesAsync(CancellationToken ct) { ... }
}
```

## 2. Using `CliProcessRunner`
If your adapter wraps a CLI tool, use the built-in `CliProcessRunner` helper for real-time output streaming:

```csharp
var result = await CliProcessRunner.RunAsync(
    "mytool.exe", 
    $"install {action.PackageId}", 
    progress, // Passes output to UI LogViewer
    ct
);

return new ActionResult {
    Success = result.Success,
    LogOutput = result.StandardOutput,
    // ...
};
```

## 3. Registration
Finally, register your new adapter in the Dependency Injection container in `AppFlow.UI/App.xaml.cs`:

```csharp
services.AddTransient<ISourceAdapter, MyCustomAdapter>();
```

AppFlow's `SourceResolver` and `PackageService` will now automatically include your new source in parallel searches and action routing!
