# ⚡ AppFlow — GOD MODE BUILD PROMPT v1.0
> Paste this directly into Cursor / GitHub Copilot / Claude Code to scaffold the full project.

---

## 🧠 CONTEXT — WHO YOU ARE

You are a senior .NET 8 Windows application architect, UX engineer, and package management expert.
You write production-quality, clean, modular C# code with zero shortcuts.
You follow SOLID principles, async-first patterns, and Windows 11 Fluent Design guidelines.
You never generate placeholder code. Every method you write must be functional.

---

## 🎯 WHAT YOU ARE BUILDING

Build a Windows desktop application named **AppFlow**.

AppFlow is a **lightweight, modern GUI frontend for Windows package management**.
It is NOT a system cleaner, NOT a PC repair tool, NOT an RMM platform.
It does ONE thing: help users discover, install, update, uninstall, and manage software packages from a single clean interface.

Core user flow:
```
Search App → View Detail → Pick Action → Execute Safely → See Result
```

---

## 🏗️ PROJECT STRUCTURE — GENERATE THIS EXACTLY

```
AppFlow/
├── AppFlow.sln
├── src/
│   ├── AppFlow.UI/                        ← WinUI 3 frontend project
│   │   ├── App.xaml
│   │   ├── App.xaml.cs
│   │   ├── MainWindow.xaml
│   │   ├── MainWindow.xaml.cs
│   │   ├── Views/
│   │   │   ├── DashboardPage.xaml
│   │   │   ├── SearchPage.xaml
│   │   │   ├── AppDetailPage.xaml
│   │   │   ├── FavoritesPage.xaml
│   │   │   ├── HistoryPage.xaml
│   │   │   ├── SourcesPage.xaml
│   │   │   └── SettingsPage.xaml
│   │   ├── ViewModels/
│   │   │   ├── DashboardViewModel.cs
│   │   │   ├── SearchViewModel.cs
│   │   │   ├── AppDetailViewModel.cs
│   │   │   └── SettingsViewModel.cs
│   │   ├── Controls/
│   │   │   ├── AppCard.xaml
│   │   │   ├── ActionPanel.xaml
│   │   │   ├── SourceBadge.xaml
│   │   │   └── LogViewer.xaml
│   │   └── Assets/
│   │
│   ├── AppFlow.Core/                      ← Business logic, models, interfaces
│   │   ├── Models/
│   │   │   ├── PackageInfo.cs
│   │   │   ├── PackageDetail.cs
│   │   │   ├── PackageAction.cs
│   │   │   ├── ActionResult.cs
│   │   │   ├── InstallRecord.cs
│   │   │   ├── SourceInfo.cs
│   │   │   └── AppSettings.cs
│   │   ├── Interfaces/
│   │   │   ├── ISourceAdapter.cs
│   │   │   ├── IPackageService.cs
│   │   │   ├── IActionEngine.cs
│   │   │   ├── ISecurityValidator.cs
│   │   │   └── ILogService.cs
│   │   ├── Services/
│   │   │   ├── PackageService.cs
│   │   │   ├── ActionEngine.cs
│   │   │   ├── SourceResolver.cs
│   │   │   ├── SecurityValidator.cs
│   │   │   └── LogService.cs
│   │   └── Enums/
│   │       ├── ActionType.cs
│   │       ├── TrustLevel.cs
│   │       └── InstallStatus.cs
│   │
│   ├── AppFlow.Sources/                   ← Package source adapters (plugin layer)
│   │   ├── WinGetAdapter.cs
│   │   ├── ChocolateyAdapter.cs
│   │   ├── ScoopAdapter.cs
│   │   ├── GitHubAdapter.cs
│   │   └── OfflineAdapter.cs
│   │
│   ├── AppFlow.Database/                  ← SQLite data access layer
│   │   ├── AppFlowDb.cs
│   │   ├── Repositories/
│   │   │   ├── InstallRecordRepository.cs
│   │   │   ├── FavoritesRepository.cs
│   │   │   └── HistoryRepository.cs
│   │   └── Migrations/
│   │       └── InitialSchema.sql
│   │
│   └── AppFlow.Tests/                     ← xUnit test project
│       ├── Services/
│       │   ├── SourceResolverTests.cs
│       │   └── SecurityValidatorTests.cs
│       └── Adapters/
│           └── WinGetAdapterTests.cs
│
├── docs/
│   ├── architecture.md
│   └── source-adapter-guide.md
└── README.md
```

---

## 📦 CORE DATA MODELS — GENERATE THESE FIRST

### PackageInfo.cs
```csharp
namespace AppFlow.Core.Models;

public class PackageInfo
{
    public string Id { get; set; } = string.Empty;           // "VideoLAN.VLC"
    public string Name { get; set; } = string.Empty;         // "VLC Media Player"
    public string Publisher { get; set; } = string.Empty;
    public string? InstalledVersion { get; set; }            // null if not installed
    public string? LatestVersion { get; set; }
    public bool IsInstalled { get; set; }
    public bool HasUpdate => IsInstalled
                             && InstalledVersion != null
                             && LatestVersion != null
                             && InstalledVersion != LatestVersion;
    public bool IsSigned { get; set; }
    public TrustLevel TrustLevel { get; set; }
    public List<string> AvailableSources { get; set; } = new();
    public List<ActionType> SupportedActions { get; set; } = new();
    public string? Category { get; set; }
    public List<string> Tags { get; set; } = new();
    public bool IsFavorite { get; set; }
}
```

### PackageAction.cs
```csharp
namespace AppFlow.Core.Models;

public class PackageAction
{
    public ActionType Type { get; set; }
    public string SourceId { get; set; } = string.Empty;
    public bool RequiresAdmin { get; set; }
    public bool SupportsSilent { get; set; }
    public bool SupportsPortable { get; set; }
    public string? CustomArgs { get; set; }
    public string? TargetVersion { get; set; }
}
```

### ActionResult.cs
```csharp
namespace AppFlow.Core.Models;

public class ActionResult
{
    public bool Success { get; set; }
    public int ExitCode { get; set; }
    public string LogOutput { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public string? ErrorSuggestion { get; set; }    // plain-English fix hint
    public string SourceUsed { get; set; } = string.Empty;
    public string? InstallerArgs { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public ActionType ActionPerformed { get; set; }
    public string PackageId { get; set; } = string.Empty;
}
```

### InstallRecord.cs
```csharp
namespace AppFlow.Core.Models;

// Tracks which source installed each package
// This prevents duplicate installs and update source mismatches
public class InstallRecord
{
    public string PackageId { get; set; } = string.Empty;
    public string InstalledFrom { get; set; } = string.Empty;   // "winget"
    public string InstalledVersion { get; set; } = string.Empty;
    public string LockedSource { get; set; } = string.Empty;    // future ops use this
    public DateTime InstalledAt { get; set; }
    public bool UserOverriddenSource { get; set; } = false;
}
```

---

## 🔌 ISOURCE ADAPTER INTERFACE — THE PLUGIN CONTRACT

```csharp
namespace AppFlow.Core.Interfaces;

// Every package source (WinGet, Chocolatey, Scoop etc.) implements this
// Adding a new source = create a new class implementing this interface
public interface ISourceAdapter
{
    string SourceId { get; }           // unique key: "winget", "chocolatey"
    string DisplayName { get; }        // "Windows Package Manager"
    int TrustScore { get; }            // 1-5, used in source selection scoring
    bool IsAvailable { get; }          // is this tool installed on the system?

    Task<List<PackageInfo>> SearchAsync(string query, CancellationToken ct = default);
    Task<PackageDetail> GetDetailsAsync(string packageId, CancellationToken ct = default);
    Task<ActionResult> InstallAsync(PackageAction action, IProgress<string> progress, CancellationToken ct = default);
    Task<ActionResult> UpdateAsync(PackageAction action, IProgress<string> progress, CancellationToken ct = default);
    Task<ActionResult> UninstallAsync(PackageAction action, IProgress<string> progress, CancellationToken ct = default);
    Task<ActionResult> RepairAsync(PackageAction action, IProgress<string> progress, CancellationToken ct = default);
    Task<List<PackageInfo>> GetInstalledPackagesAsync(CancellationToken ct = default);
}
```

---

## ⚙️ WINGET ADAPTER — IMPLEMENT THIS FULLY

```csharp
namespace AppFlow.Sources;

// Use WinGet COM API (Microsoft.Management.Deployment) as primary
// Fall back to CLI process wrapper only if COM API unavailable
public class WinGetAdapter : ISourceAdapter
{
    public string SourceId => "winget";
    public string DisplayName => "Windows Package Manager (WinGet)";
    public int TrustScore => 5;
    public bool IsAvailable => CheckWinGetAvailable();

    // IMPLEMENT: Use Microsoft.Management.Deployment COM API
    // NuGet: Microsoft.WinGet.Client
    // DO NOT use Process.Start("winget",...) as primary method
    // CLI wrapper is only acceptable as fallback

    public async Task<List<PackageInfo>> SearchAsync(string query, CancellationToken ct = default)
    {
        // TODO: implement via COM API
        // PackageManager mgr = new PackageManager();
        // var results = await mgr.FindPackagesAsync(query);
        throw new NotImplementedException();
    }

    // ... implement all interface methods

    private bool CheckWinGetAvailable()
    {
        // Check if winget.exe exists in PATH or known locations
        throw new NotImplementedException();
    }
}
```

---

## 🧮 SOURCE RESOLVER — THE SCORING ENGINE

```csharp
namespace AppFlow.Core.Services;

// Queries all available sources in PARALLEL
// Scores each result and auto-selects the best one
// User can still manually override
public class SourceResolver
{
    private readonly List<ISourceAdapter> _adapters;

    public async Task<SourceResolutionResult> ResolveAsync(string packageId, CancellationToken ct = default)
    {
        // Step 1: Query all sources in parallel (NOT sequential)
        var tasks = _adapters
            .Where(a => a.IsAvailable)
            .Select(a => QuerySourceSafeAsync(a, packageId, ct));

        var results = await Task.WhenAll(tasks);

        // Step 2: Score each result
        var scored = results
            .Where(r => r != null)
            .Select(r => new ScoredResult
            {
                Result = r!,
                Score = CalculateScore(r!)
            })
            .OrderByDescending(s => s.Score)
            .ToList();

        // Step 3: Return best + all alternatives (user can switch)
        return new SourceResolutionResult
        {
            BestMatch = scored.FirstOrDefault()?.Result,
            AllOptions = scored,
            RecommendedSourceId = scored.FirstOrDefault()?.Result.SourceId
        };
    }

    private int CalculateScore(SourceQueryResult result)
    {
        int score = 0;
        if (result.IsSigned)          score += 3;
        if (result.IsOfficialSource)  score += 2;
        if (result.IsLatestVersion)   score += 1;
        if (result.IsTrustedPublisher) score += 2;
        if (result.SupportsSilent)    score += 1;
        score += result.SourceTrustScore;   // adapter's base trust score
        return score;
    }

    private async Task<SourceQueryResult?> QuerySourceSafeAsync(
        ISourceAdapter adapter, string packageId, CancellationToken ct)
    {
        try { return await adapter.GetDetailsAsync(packageId, ct) as SourceQueryResult; }
        catch { return null; }   // one source failing must not block others
    }
}
```

---

## 🔒 SECURITY VALIDATOR

```csharp
namespace AppFlow.Core.Services;

public class SecurityValidator : ISecurityValidator
{
    // Before ANY install, run this validation pipeline
    public async Task<ValidationResult> ValidateAsync(PackageAction action, SourceQueryResult source)
    {
        var result = new ValidationResult();

        // Check 1: Digital signature
        if (!source.IsSigned)
        {
            result.AddWarning("Package is unsigned. Proceed with caution.");
            result.RiskLevel = RiskLevel.Medium;
        }

        // Check 2: Source trust
        if (source.SourceTrustScore < 3)
        {
            result.AddWarning("Source has low trust rating.");
            result.RiskLevel = RiskLevel.High;
        }

        // Check 3: Require confirmation for high-risk
        if (result.RiskLevel == RiskLevel.High)
        {
            result.RequiresUserConfirmation = true;
            result.ConfirmationMessage =
                $"This package is from an untrusted source ({source.SourceId}). " +
                "Are you sure you want to install it?";
        }

        // Check 4: Hash verification (if hash available)
        if (!string.IsNullOrEmpty(source.ExpectedHash))
        {
            result.HashValid = await VerifyHashAsync(source.DownloadUrl, source.ExpectedHash);
            if (!result.HashValid)
            {
                result.BlockInstall = true;
                result.AddError("Hash mismatch detected. Installation blocked for security.");
            }
        }

        return result;
    }
}
```

---

## 🗄️ SQLITE DATABASE SCHEMA

```sql
-- Run this on first launch
-- File: AppFlow.Database/Migrations/InitialSchema.sql

CREATE TABLE IF NOT EXISTS InstallRecords (
    PackageId        TEXT PRIMARY KEY,
    InstalledFrom    TEXT NOT NULL,
    InstalledVersion TEXT NOT NULL,
    LockedSource     TEXT NOT NULL,
    InstalledAt      TEXT NOT NULL,
    UserOverridden   INTEGER DEFAULT 0
);

CREATE TABLE IF NOT EXISTS Favorites (
    PackageId    TEXT PRIMARY KEY,
    AddedAt      TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS ActionHistory (
    Id               INTEGER PRIMARY KEY AUTOINCREMENT,
    PackageId        TEXT NOT NULL,
    PackageName      TEXT NOT NULL,
    ActionType       TEXT NOT NULL,
    SourceUsed       TEXT NOT NULL,
    Success          INTEGER NOT NULL,
    ExitCode         INTEGER,
    ErrorMessage     TEXT,
    InstallerArgs    TEXT,
    LogOutput        TEXT,
    Timestamp        TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS Settings (
    Key   TEXT PRIMARY KEY,
    Value TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_history_package ON ActionHistory(PackageId);
CREATE INDEX IF NOT EXISTS idx_history_timestamp ON ActionHistory(Timestamp);
```

---

## 🎨 UI REQUIREMENTS — WINUI 3

Build the UI with these exact requirements:

**MainWindow.xaml:**
- NavigationView (left sidebar, compact by default)
- Nav items: Dashboard, Search, Favorites, History, Sources, Settings
- Search bar pinned at top of content area
- Theme toggle (Dark/Light) in titlebar area

**SearchPage.xaml:**
- Full-width search TextBox with instant filtering (debounce 300ms)
- Results as ItemsRepeater with AppCard controls
- Filter chips: All / Installed / Updates Available / Favorites
- Source filter dropdown

**AppDetailPage.xaml layout:**
```
┌─────────────────────────────────────────────────┐
│  [App Icon]  App Name          [★ Favorite]     │
│              Publisher · Category                │
├─────────────────┬───────────────────────────────┤
│  VERSION INFO   │  ACTION PANEL                 │
│  Installed: X   │  [Install]  [Update]          │
│  Latest:    Y   │  [Uninstall] [Repair]         │
│  Source:    Z   │  [Silent ▾] [Portable ▾]      │
│  Signed: ✅     │  Source: [WinGet ▾]           │
├─────────────────┴───────────────────────────────┤
│  LIVE LOG OUTPUT                                │
│  > Downloading package...                       │
│  > Verifying signature...                       │
│  > Installing...                                │
│  ████████████░░░░  67%                          │
└─────────────────────────────────────────────────┘
```

---

## ⚡ PERFORMANCE REQUIREMENTS — HARD TARGETS

These are NOT suggestions. These are pass/fail criteria:

| Metric | Target | How to achieve |
|--------|--------|----------------|
| Cold startup | < 2 seconds | Lazy-load adapters, async init |
| Search results | < 300ms | Debounce + local cache first |
| RAM idle | < 100 MB | No background polling |
| RAM during install | < 200 MB | Stream logs, don't buffer |
| App size on disk | < 50 MB | Self-contained but trimmed |
| Source query timeout | 5 seconds max | CancellationToken on all queries |

---

## 🧪 TEST REQUIREMENTS

Write tests for these scenarios minimum:

```csharp
// SourceResolverTests.cs
[Fact] public async Task ResolveAsync_MultipleSourcesAvailable_ReturnsBestScore()
[Fact] public async Task ResolveAsync_OneSourceFails_OthersStillReturn()
[Fact] public async Task ResolveAsync_AllSourcesFail_ReturnsEmptyResult()

// SecurityValidatorTests.cs
[Fact] public async Task ValidateAsync_UnsignedPackage_AddsWarning()
[Fact] public async Task ValidateAsync_HashMismatch_BlocksInstall()
[Fact] public async Task ValidateAsync_HighRiskSource_RequiresConfirmation()

// InstallRecordRepositoryTests.cs
[Fact] public async Task SaveRecord_SamePackageTwice_PreventsSecondInstall()
[Fact] public async Task GetLockedSource_ReturnsOriginalInstallSource()
```

---

## 📋 NUGET PACKAGES TO ADD

```xml
<!-- AppFlow.UI.csproj -->
<PackageReference Include="Microsoft.WindowsAppSDK" Version="1.5.*" />
<PackageReference Include="CommunityToolkit.WinUI.UI.Controls" Version="8.*" />
<PackageReference Include="CommunityToolkit.Mvvm" Version="8.*" />

<!-- AppFlow.Core.csproj -->
<PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="8.*" />
<PackageReference Include="Microsoft.Extensions.Logging" Version="8.*" />
<PackageReference Include="Serilog" Version="4.*" />

<!-- AppFlow.Sources.csproj -->
<PackageReference Include="Microsoft.WinGet.Client" Version="1.*" />

<!-- AppFlow.Database.csproj -->
<PackageReference Include="Microsoft.Data.Sqlite" Version="8.*" />
<PackageReference Include="Dapper" Version="2.*" />

<!-- AppFlow.Tests.csproj -->
<PackageReference Include="xunit" Version="2.*" />
<PackageReference Include="Moq" Version="4.*" />
<PackageReference Include="FluentAssertions" Version="6.*" />
```

---

## 🚀 BUILD ORDER — FOLLOW THIS SEQUENCE

```
Phase 1 — Foundation (Week 1)
  ✅ Step 1: Create solution + all projects
  ✅ Step 2: Generate all Models (PackageInfo, ActionResult, etc.)
  ✅ Step 3: Define all Interfaces (ISourceAdapter, etc.)
  ✅ Step 4: Create SQLite schema + migrations
  ✅ Step 5: Implement InstallRecord duplicate-prevention logic

Phase 2 — Source Integration (Week 2)
  ✅ Step 6: Implement WinGetAdapter (COM API)
  ✅ Step 7: Implement SourceResolver (parallel queries + scoring)
  ✅ Step 8: Implement SecurityValidator (signature + hash)
  ✅ Step 9: Implement ActionEngine (install/update/uninstall flow)
  ✅ Step 10: Write unit tests for all services

Phase 3 — UI (Week 3)
  ✅ Step 11: MainWindow + NavigationView
  ✅ Step 12: SearchPage + AppCard control
  ✅ Step 13: AppDetailPage + ActionPanel
  ✅ Step 14: LogViewer control (real-time streaming)
  ✅ Step 15: HistoryPage + FavoritesPage

Phase 4 — Polish (Week 4)
  ✅ Step 16: Dark/Light mode toggle
  ✅ Step 17: Keyboard shortcuts + command palette
  ✅ Step 18: Export package list (JSON)
  ✅ Step 19: Chocolatey + Scoop adapters
  ✅ Step 20: Performance profiling + optimization
```

---

## ⛔ ABSOLUTE RULES — NEVER VIOLATE

1. **Never install from same package twice** — always check InstallRecord first
2. **Never skip signature validation** — even if user is in a hurry
3. **Never query sources sequentially** — always use Task.WhenAll parallel
4. **Never hardcode paths** — use Environment.GetFolderPath()
5. **Never catch Exception globally** — handle specific exceptions per layer
6. **Never block the UI thread** — every operation must be async
7. **Never add system cleaner, registry cleaner, or driver updater** — ever
8. **Source locking is sacred** — the source that installed a package owns its future updates
9. **Always show plain-English error + fix suggestion** — never raw exception strings
10. **Admin elevation must show shield icon (🛡️) in UI** — user must always know

---

## 🎯 DEFINITION OF DONE

AppFlow MVP is complete when:
- [ ] User can search any app and see results in < 300ms
- [ ] User can install an app via WinGet with real-time log output
- [ ] App prevents duplicate installs from different sources
- [ ] All installs are validated for signature before execution
- [ ] Uninstall uses the same source that performed the install
- [ ] Action history is logged to SQLite and viewable in HistoryPage
- [ ] App starts cold in under 2 seconds
- [ ] RAM stays under 100MB at idle
- [ ] Dark mode and Light mode both work correctly
- [ ] All unit tests pass

---

*AppFlow GOD MODE Prompt v1.0 — Generated for Ashwani Singh*
*Target: Senior DevOps / SysAdmin toolkit project*