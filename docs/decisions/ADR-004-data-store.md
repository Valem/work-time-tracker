# ADR-004: Data — one SQLite file in the person's local profile; facts stored, days derived

**Status:** proposed · **Decided by:** owner (local, non-roaming, kept until uninstall; past weeks not
frozen) · agent (format, shape) · **Introduced by:** 001-foundation · **Amended by:** 002-holiday-planner

## Context

Data never leaves the laptop and is readable only by its person (vision). Two processes touch it: the
Recorder appends facts, the App reads them and saves settings, the holiday plan and the public holidays it
loads. Days must be recomputable when a rule or a setting changes, and 002 adds the holiday plan on top.

## Decision

- **Location:** `%LocalAppData%\WorkTimeTracker` — per user, not roaming, not synced; it inherits the
  profile's user-only access rights. The uninstaller removes it (001 K3, ADR-002).
- **Format:** one **SQLite** file in WAL mode via `Microsoft.Data.Sqlite`; schema changes are
  forward-only, versioned in the file and applied at open under SQLite's write lock.
- **Who migrates:** the Recorder, at its start (and the developer CLI, on the file it is pointed at).
  The App migrates only when no Recorder runs; while one runs, the App waits for that Recorder to bring
  the file to the App's version — up to 30 s, else it reports the file as unusable (001 D1) — so an
  older Recorder never runs on a schema it does not know.
- **Shape:** append-only **activity facts**, each with its instant and the local UTC offset in force,
  deduplicated by source key; **coverage spans**; **lunch rules** with the date they apply from; from
  schema 2 the **holiday plan's inputs** and the **loaded public-holiday years** (ADR-008).
  **Days, weeks, day statuses and holiday counts are never stored** — always derived by the domain core.
- **Retention:** nothing is pruned (001 K3): ~100–300 facts a day, years fit in a few MB.
- The plan's tables sit beside the facts; facts and recording never change for them — the Recorder only
  migrates the file to schema 2.

## Rejected alternatives

| Option | Why not |
|--------|---------|
| JSON Lines files | Two writing processes need hand-made locking and torn-line recovery; range reads by hand |
| EF Core + SQLite | A large dependency and a migration-lock protocol for a schema of four small tables |
| Roaming profile / Documents | Copied to servers or OneDrive — data would leave the laptop |
| Storing derived days | Every rule fix would need a data migration; owner cut freezing past weeks (brief decision 14) |

## Consequences

- **A rule fix shipped in an update may change past weeks** — accepted by the owner (brief decision 14).
- **Uninstall deletes all history** older than Windows' own logs, and the holiday plans; an export (vision
  idea) is the later remedy.
- **Forward-only:** a file migrated by a newer Recorder is refused as newer by an older build (001 D1); going
  back to an older release means starting from an empty data file.
- **Facts keep Windows' raw reason; names are derived on read.** Infrastructure's read path of the
  fact journal names each reason from its stored raw reason with the mapping of the running version,
  so a reason named later reaches facts already stored, and a name or cause it does not know reads as
  unknown. Stored rows are never rewritten; domain rules decide on reason names only, and the raw reason
  passes through the domain unread.
- **An upgrade migrates when its new Recorder starts:** the installer stops the old Recorder and starts
  the new one (001 S3). Should an older Recorder still run, an App opened beside it waits and then
  reports the file as unusable rather than migrating under it.
- A settings change never needs a data migration; the stored UTC offset keeps past days in place when
  the laptop's time zone changes.
- **Hard to undo:** low (one versioned file).

## Sources

1. Known folders, LocalAppData — https://learn.microsoft.com/en-us/windows/win32/shell/knownfolderid
2. SQLite WAL — https://www.sqlite.org/wal.html
