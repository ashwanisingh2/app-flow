# ⚡ AppFlow

AppFlow is a WinUI 3 desktop frontend for Windows package managers. It searches, installs, updates, repairs, and removes packages through WinGet, Chocolatey, Scoop, GitHub Releases, and local installers from one interface.

## Features

- **Unified search:** Enabled sources are queried concurrently, with cancellation, a five-second default timeout, and a short-lived query cache.
- **Source locking:** AppFlow records the source used for an install and routes future updates, repairs, and uninstalls through that source.
- **Mandatory safety checks:** HTTPS enforcement, streaming SHA-256 validation, source trust warnings, and Authenticode checks for local/downloaded installers.
- **Explicit risk confirmation:** An unsigned or low-trust installer cannot run until the user accepts a warning. Integrity failures cannot be overridden.
- **Action history:** Every attempted package action is written to SQLite and displayed in the History and Dashboard pages.
- **Favorites, source preferences, and settings:** All are backed by SQLite.
- **Safe CLI execution:** Package-manager arguments use `ProcessStartInfo.ArgumentList`; package IDs are never interpolated into a shell command.
- **Framework-dependent WinUI deployment:** Avoids bundling the complete Windows App Runtime into every build.

## Architecture

AppFlow uses five projects:

1. `AppFlow.UI` — WinUI 3 presentation and CommunityToolkit MVVM view models
2. `AppFlow.Core` — domain models, security, source resolution, action orchestration, and persistence contracts
3. `AppFlow.Sources` — WinGet, Chocolatey, Scoop, GitHub, and offline source adapters
4. `AppFlow.Database` — SQLite/Dapper repositories and migrations
5. `AppFlow.Tests` — xUnit service, adapter-parser, action-engine, and repository tests

See [`docs/architecture.md`](docs/architecture.md) for the action and data flows.

## Requirements

- Windows 10 1809+ or Windows 11
- .NET 8 SDK
- Visual Studio 2022 with **.NET Desktop Development** and **Windows App SDK C#** workloads
- Windows App Runtime 1.5 for framework-dependent deployment
- At least one supported package manager (`winget`, `choco`, or Scoop) for online package operations

## Build and run

1. Open `AppFlow.sln` in Visual Studio 2022.
2. Select the `x64` solution platform.
3. Set `AppFlow.UI` as the startup project.
4. Build and run with F5.

The WinUI project should be built on Windows. Core tests can be run with:

```powershell
dotnet test src/AppFlow.Tests/AppFlow.Tests.csproj
```

## Local data

AppFlow stores its database and rolling logs below the current user's local application-data directory:

```text
%LOCALAPPDATA%\AppFlow\
```

Installed-package exports are written to:

```text
%USERPROFILE%\Documents\AppFlow\Exports\
```
