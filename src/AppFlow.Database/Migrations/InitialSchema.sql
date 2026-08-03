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
