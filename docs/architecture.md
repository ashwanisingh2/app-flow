# AppFlow Architecture

## Projects

1. **AppFlow.UI**
   - WinUI 3 shell, pages, controls, and CommunityToolkit MVVM view models.
   - Owns user confirmation dialogs and presentation-only state.

2. **AppFlow.Core**
   - Domain models and source contracts.
   - Concurrent search, source scoring, mandatory validation, source locking, and action orchestration.
   - Depends on persistence interfaces rather than SQLite.

3. **AppFlow.Sources**
   - Implements `ISourceAdapter` for WinGet, Chocolatey, Scoop, GitHub Releases, and local installers.
   - CLI adapters pass discrete arguments through `ProcessStartInfo.ArgumentList` and stream stdout/stderr.

4. **AppFlow.Database**
   - Implements Core persistence contracts with SQLite and Dapper.
   - Embeds and applies the idempotent initial migration.

5. **AppFlow.Tests**
   - Tests source isolation/scoring, integrity policy, source locking, duplicate prevention, result merging, WinGet parsing, and SQLite mappings.

## Install flow

1. Search adapters run concurrently; unavailable or disabled sources are excluded.
2. Navigation preserves the package ID and source selected by the user.
3. `AppDetailViewModel` sends both the action and resolved source metadata to `IActionEngine`.
4. `ActionEngine` serializes operations per package and checks `IInstallRecordStore` for duplicates/source locks.
5. `SecurityValidator` enforces HTTPS and validates a supplied SHA-256 hash by streaming the installer. Local files are checked with WinVerifyTrust.
6. A low-trust or insufficiently signed package returns a confirmation request to the UI. A hard integrity failure cannot be overridden.
7. The selected adapter executes the action and streams output to the live log.
8. A successful install/update updates the install record; uninstall removes it.
9. Successes and failures are written through `IActionHistoryStore` and to the rolling application log.

## Source locking

`InstallRecord.LockedSource` is authoritative for update, repair, and uninstall operations. If a UI request names another source, `ActionEngine` routes it back to the locked source. Duplicate installs are rejected while a record exists.

## Cancellation and failure isolation

- Search and resolution have linked timeout/caller cancellation tokens.
- Cancellation is not swallowed inside individual adapters.
- A provider failure is isolated at the aggregation boundary so healthy providers still return results.
- Unexpected internal details are written to logs; the UI receives a plain-language message.
