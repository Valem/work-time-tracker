# ADR-004: Data — one SQLite file in the person's local profile; facts stored, days derived

**Status:** proposed · **Decided by:** owner (local, non-roaming, kept until uninstall; past weeks not
frozen) · agent (format, shape) · **Introduced by:** 001-foundation

## Context

Data never leaves the laptop and is readable only by its person (vision). Two processes touch it: the
Recorder appends facts, the App reads and saves settings. Days must be recomputable when a rule or a
setting changes, and 002–004 add corrections, day marks, targets and planned days on top.

## Decision

- **Location:** `%LocalAppData%\WorkTimeTracker` — per user, not roaming, not synced; it inherits the
  profile's user-only access rights. The uninstaller removes it (001 K3, ADR-002).
- **Format:** one **SQLite** file in WAL mode via `Microsoft.Data.Sqlite`; schema changes are
  forward-only, versioned in the file and applied at open under SQLite's write lock, so whichever
  process opens first migrates.
- **Shape:** append-only **activity facts**, each with its instant and the local UTC offset in force,
  deduplicated by source key; **coverage spans**; **lunch rules** with the date they apply from.
  **Days, weeks and later balances are never stored** — always derived by the domain core.
- **Retention:** nothing is pruned (001 K3): ~100–300 facts a day, years fit in a few MB.
- 002–004 add input tables beside the facts; facts and the Recorder never change for them.

## Rejected alternatives

| Option | Why not |
|--------|---------|
| JSON Lines files | Two writing processes need hand-made locking and torn-line recovery; range reads by hand |
| EF Core + SQLite | A large dependency and a migration-lock protocol for a three-table schema |
| Roaming profile / Documents | Copied to servers or OneDrive — data would leave the laptop |
| Storing derived days | Every rule fix would need a data migration; owner cut freezing past weeks (brief decision 14) |

## Consequences

- **A rule fix shipped in an update may change past weeks** — accepted by the owner (brief decision 14).
- **Uninstall deletes all history** older than Windows' own logs; an export (vision idea) is the later
  remedy.
- A settings change never needs a data migration; the stored UTC offset keeps past days in place when
  the laptop's time zone changes.
- **Hard to undo:** low (one versioned file).

## Sources

1. Known folders, LocalAppData — https://learn.microsoft.com/en-us/windows/win32/shell/knownfolderid
2. SQLite WAL — https://www.sqlite.org/wal.html
