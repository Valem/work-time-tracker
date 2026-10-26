# ADR-003: Activity source — a live per-user Recorder first, Windows history second

**Status:** proposed · **Decided by:** owner (live Recorder + history model) · agent (sources,
precedence) · **Introduced by:** 001-foundation

## Context

Start and end must match the person's first and last use within one minute, Windows waking itself
must never count, and past days must appear on first start (001 brief, outcomes 2, 5). Evidence from
read-only checks of Windows 11 laptops (2026-10-24):

| Finding | Consequence |
|---------|-------------|
| Lock/unlock is in `Microsoft-Windows-Winlogon/Operational` event 811, subscriber `Sens`, code 4 = lock, 5 = unlock — readable without admin; the log is small (1 MB by default), so it reaches back weeks | Lock-only pauses can be rebuilt only that far back; the codes are undocumented |
| The System log reaches back further (20 MB by default, months); Kernel-Power 506/507 give Modern Standby enter/exit with a reason | Older days rebuild from standby only |
| Many standby exits logged as "Input Keyboard" or "Power Button" have no unlock and return to standby within 2 min (Windows waking itself at night) | A reason alone is no proof of the person |
| An idle lock comes one display timeout after the last input; Security-log lock audit is off and admin-only | History cannot give the true end of use; only live last-input can |

## Decision

- **Live:** the Recorder (ADR-002) receives session notifications (lock, unlock, sign-in, sign-out) and
  power notifications (suspend, resume, lid); when use ends it reads the session's last-input time
  (`GetLastInputInfo`) and stores the end's **cause** — Win+L, lid, sign-out, or the timer that fired
  (idle lock, screen-saver lock, display-off standby, machine inactivity limit — managed laptops may set
  several) with its length, inferred from the idle time against the timers in force; Windows names
  neither the timer nor the power button, so a standby nothing explains is stored as unknown. At start
  it records the session's logon time, so coverage begins at sign-in when autostart ran within 5 minutes
  of it — a later start leaves the stretch before it to history, marked rebuilt; history facts for the
  stretch between sign-in and its own start are merged without marking the day rebuilt. Every fact keeps
  its instant and the local UTC offset; coverage is extended every minute.
- **History:** `Winlogon/Operational` 811 (Sens 4/5), and from the System log Kernel-Power 506/507,
  42/107, Power-Troubleshooter 1, Kernel-General 12/13, Winlogon 7001/7002, User32 1074. Each record
  is imported once, keyed by channel + record id + time (record ids restart when a log is cleared);
  the cursor is a time with an overlap, so history outlives Windows' log. Only events inside this
  user's own sign-in sessions (by SID and session) count — the System log is machine-wide. The Recorder
  imports at every start and whenever it detects a gap in its own coverage — which covers first start.
- **Precedence:** inside the Recorder's coverage only live facts count; outside it, history facts.
- **Interpretation lives only in the domain core** (person proof, span ends, midnight, lunch); the
  Recorder and the importer store facts without judging them. The rules are BR-001…BR-011 of the
  001 concept.

## Rejected alternatives

| Option | Why not |
|--------|---------|
| History only, no resident process | Ends late by the lock timeout; no lock data beyond ~7 weeks — fails outcome 2 |
| Windows service + user helper | A service in session 0 cannot see the user's input; adds admin install and IPC |
| Security-log audit events 4800/4801 | Audit policy off by default; needs admin to enable and to read |

## Consequences

- A process runs from sign-in (visible under Startup apps); it must never keep the system awake.
- Days rebuilt from history are less precise and are marked so (001 K1).
- The Winlogon codes may change with a Windows update: fixture tests detect it; live recording is
  unaffected.
- Raw activity times stay on the laptop until uninstall (001 K3; ADR-004).

## Sources

1. WTSRegisterSessionNotification — https://learn.microsoft.com/en-us/windows/win32/api/wtsapi32/nf-wtsapi32-wtsregistersessionnotification
2. Desktop Activity Moderator / PBT_APMSUSPEND — https://learn.microsoft.com/en-us/windows/compatibility/desktop-activity-moderator · https://learn.microsoft.com/en-us/windows/win32/power/pbt-apmsuspend
3. GetLastInputInfo — https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getlastinputinfo
