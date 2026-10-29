# ADR-008: Holiday plan — the person's inputs stored, every day and count derived

**Status:** proposed · **Decided by:** owner (bookings survive a day becoming a day off; «Mein Jahr» carries into the
next year; a change applies to the whole planned year; a canton change clears the ticks) · agent (shape, units) ·
**Introduced by:** 002-holiday-planner

## Context

The planner takes Pensum, free weekdays, yearly holiday days, canton, ticked optional public holidays and whole or
half holiday days (002 brief); it shows the year, the counts and the week's marks. ADR-004 stores facts and derives
days. The next likely changes are a half free day (90 % Pensum), a canton move during a year, several years planned
and another public-holiday source.

## Decision

- **Stored — the person's inputs, in the one data file (ADR-004):**
  - **Bookings:** one per date, the person's intent: whole or half. Only a drag, a click or an undo changes one.
  - **«Mein Jahr» settings:** Pensum, the work share of each weekday Monday–Friday (none, half or whole), the yearly
    holiday days or none (prefilled), the canton — each with the date it applies from, 1 January of the planned year;
    superseded, never edited. On a date, the settings with the latest start on or before it apply; of several with one
    start, the last saved. A year without its own settings so uses the last earlier one's. «Mein Jahr» offers each
    weekday as working or free; the half share is kept for a later half free day.
  - **Ticks:** the dates whose optional or part-canton public holiday the person marked as granted. Settings saved
    with another canton clear the ticks on the dates those settings apply to.
  - **Public-holiday years:** per canton and year, the entries in the domain's terms (ADR-007), names in every
    language the source gave, the source and the load time; replaced only whole.
  - **Layout:** the planner's chosen layout.
- **Derived, never stored:** each date's status (work share, public holiday, booking, what it counts, bridge day), a
  year's counts and a week's marks — recomputed on read by the domain core from the inputs and today's date.
- **Unit:** whole half days (`HalfDays`); work shares and public holidays count in halves, nothing finer.
- **A day that becomes a day off keeps its booking:** a free weekday, a canton, a tick or a newly loaded year makes it
  count 0 there until the date is a workday again.
- **Schema step 2** adds these tables; facts, coverage and lunch rules are untouched; migration as ADR-004.

## Rejected alternatives

| Option | Why not |
|--------|---------|
| Bookings as date ranges | A partial erase splits and merges ranges; every gesture works per day |
| Deleting bookings on a day that became a day off | A late public-holiday year or a weekday toggled back would destroy plans |
| One undated settings record | A canton or Pensum change would rewrite past years and their weeks |
| A journal of every plan change | More data about the person, and folding logic, for an undo that keeps only the last change |
| Stored counts or day statuses | Every rule fix would need a data migration (ADR-004) |
| Hundredths or decimals of a day | The brief knows whole and half days only |

## Consequences

- A half free day, a canton move with its own start date and a further year need no change to store or domain.
- In 002 a Pensum or weekday change applies to the whole planned year, days already taken included.
- A booking hidden by a day off reappears when the day is a workday again (owner).
- Schema 2 is forward-only: once migrated, a 001 build refuses the file as newer.
- Uninstall deletes the plans with the data folder (001 K3).
- **Hard to undo:** low (one versioned file).
