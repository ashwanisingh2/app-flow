CREATE TABLE IF NOT EXISTS SchemaMigrations (
    Version     INTEGER PRIMARY KEY,
    AppliedAt   TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS InstallRecords (
    PackageId        TEXT PRIMARY KEY COLLATE NOCASE,
    InstalledFrom    TEXT NOT NULL,
    InstalledVersion TEXT NOT NULL,
    LockedSource     TEXT NOT NULL,
    InstalledAt      TEXT NOT NULL,
    UserOverridden   INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS Favorites (
    PackageId    TEXT PRIMARY KEY COLLATE NOCASE,
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
    Key   TEXT PRIMARY KEY COLLATE NOCASE,
    Value TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_history_package ON ActionHistory(PackageId COLLATE NOCASE);
CREATE INDEX IF NOT EXISTS idx_history_timestamp ON ActionHistory(Timestamp);

INSERT OR IGNORE INTO SchemaMigrations (Version, AppliedAt)
VALUES (1, strftime('%Y-%m-%dT%H:%M:%fZ', 'now'));
