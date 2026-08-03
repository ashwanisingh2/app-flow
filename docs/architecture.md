# AppFlow Architecture

## Layer Overview

AppFlow is divided into 5 distinct projects to ensure separation of concerns:

1. **AppFlow.UI (WinUI 3)**
   - Responsible solely for presentation and view logic.
   - Uses `CommunityToolkit.Mvvm` for ViewModels.
   - Implements Windows 11 Mica backdrop design.

2. **AppFlow.Core (.NET 8 Class Library)**
   - Contains all the domain models, enums, and interfaces.
   - Contains core business logic services: `ActionEngine`, `SourceResolver`, `SecurityValidator`.
   - Has no dependencies on UI or Database frameworks.

3. **AppFlow.Sources (.NET 8 Class Library)**
   - Implements the `ISourceAdapter` plugin interface from Core.
   - Contains CLI wrappers (`CliProcessRunner`) to interface with `winget`, `choco`, `scoop`, etc.
   - Contains HTTP logic for `GitHubAdapter`.

4. **AppFlow.Database (.NET 8 Class Library)**
   - SQLite integration using `Microsoft.Data.Sqlite` and `Dapper`.
   - Handles the `appflow.db` lifecycle (creation and migrations).
   - Exposes Repositories (`InstallRecordRepository`, `FavoritesRepository`, `HistoryRepository`).

5. **AppFlow.Tests (xUnit)**
   - Unit tests for the core logic, resolvers, validators, and adapters using `Moq` and `FluentAssertions`.

## Data Flow (Install Action)
1. **User clicks Install** in `AppDetailPage` (UI)
2. `AppDetailViewModel` creates a `PackageAction` request and passes it to `IActionEngine` (Core).
3. `IActionEngine` runs the `ISecurityValidator` (Core) pipeline to ensure the package is safe.
4. `IActionEngine` looks up the correct `ISourceAdapter` from `AppFlow.Sources`.
5. The Adapter (e.g., `WinGetAdapter`) uses `CliProcessRunner` to execute the CLI command in the background.
6. The CLI standard output is streamed back via `IProgress<string>` to the ViewModel for the UI LogViewer.
7. Upon completion, `IActionEngine` writes an `InstallRecord` to the Database (via Repo) to lock the source for future updates.
8. The `ActionHistory` is recorded to the SQLite Database.
