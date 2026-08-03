# ⚡ AppFlow

AppFlow is a modern WinUI 3 desktop application that serves as a unified GUI frontend for Windows package managers (WinGet, Chocolatey, Scoop, and more). It allows users to search, install, update, and manage software packages from a single, beautiful interface with real-time log output, security validation, and source locking.

## Features
- **Unified Search:** Search across WinGet, Chocolatey, Scoop, and local Offline files simultaneously.
- **Source Locking:** Ensures that updates to a package always pull from the original source it was installed from.
- **Security Validation:** Pre-install checks for source trust scores and digital signatures.
- **Action History:** Keeps a detailed local SQLite log of all install/update/uninstall actions.
- **Favorites:** Star your favorite packages for quick access.
- **Mica Design:** Native Windows 11 Mica backdrop integration with smooth UI elements.

## Architecture
AppFlow uses a clean, 5-layer architecture:
1. `AppFlow.UI` (WinUI 3 Frontend, MVVM via CommunityToolkit)
2. `AppFlow.Core` (Business Logic, Services, Models)
3. `AppFlow.Sources` (Plugin layer for CLI wrappers and API clients)
4. `AppFlow.Database` (SQLite storage with Dapper)
5. `AppFlow.Tests` (xUnit tests)

See `docs/architecture.md` for more details.

## Build Requirements
- **.NET 8 SDK**
- **Visual Studio 2022** (with the ".NET Desktop Development" and "Windows App SDK C#" workloads installed)
- **Windows 10 (1809+) or Windows 11**

*Note: Raw `dotnet build` from CLI on the UI project without Visual Studio components will fail due to missing `Microsoft.Build.Packaging.Pri.Tasks` which is required for WinUI 3 PRI resource compilation.*

## Usage
1. Open `AppFlow.sln` in Visual Studio 2022.
2. Set `AppFlow.UI` as the Startup Project.
3. Build and Run (F5).
