# Holiday Planner — Intent Brief

**Version:** v1.1.0 — approved 2026-10-30 · **Feature:** 002-holiday-planner

```text
Type:            feature in Work Time Tracker                                         [you]
Goal:            I plan my holidays for the year in the app, keep the plan up to      [you]
                 date as it changes, and see holiday days in the week overview
Actors & story:  Me / a colleague → sets Pensum, free weekdays, holiday days, canton  [you]
                 → drags the year's holidays onto the calendar → drags some away or
                 elsewhere during the year (~10 times) → sees them in the week
Inputs/Outputs:  in: Pensum, free weekdays, yearly holiday days (prefilled 20 ×       [you]
                 Pensum), canton, granted optional public holidays, holidays (whole
                 or half). out: year, days left, week marks. unchanged: recorded use
NOT:             no hand corrections, no overtime balance (both dropped); no sick or  [you]          
                 private days; no weeks, only days; no dialogs; nothing to the employer
Drivers:         zero daily effort — a few entries a year · nothing about the         [you]
                 person's hours or holidays leaves the laptop; the canton's public
                 holidays are requested per canton-year (last, this, next), per
                 reload click and after a failure (next start or 1 h later), naming
                 only country, canton and year · free, no running cost (vision)
Outcome:         1 the year on one screen, months as columns or rows as chosen; drag  [you]
                   books or removes days, a click goes whole → half → none; undoable
                 2 only workdays count — never a weekend, free weekday or public
                   holiday, none of which can be booked; a half day counts 0.5
                 3 the canton's public holidays appear each year without any entry;
                   an optional one counts once ticked
                 4 the week marks holiday, half holiday, public holiday, free day
                 5 always in view: yearly days, taken, planned, left
Constraints:     Windows 11; same app and data file; German, Swiss formats; public    [repo | you]
                 holidays from the OpenHolidays API (free, open data, no key)
Budget:          M — ≤ 4 phases, ≤ 2 review rounds per phase, ≤ 6 FRs, concept ≤ 400 [you]
```

## Decision log

| # | Question | Decision | Decided by |
|---|----------|----------|------------|
| 1 | Next feature | Holiday planner; hand corrections and overtime balance are dropped | you |
| 2 | How often holidays are entered | All at the start of the year; moved or cancelled during the year, up to ~10 times a year | you |
| 3 | Yearly holiday days | Configurable, in days only, never weeks; prefilled with 20 × Pensum (Swiss legal minimum 4 weeks); any number accepted | you |
| 4 | Week overview | Holiday days show in the week | you |
| 5 | Half days | A weekday can be a half holiday, counted 0.5 | you |
| 6 | Pensum | A setting (e.g. 80 %, 100 %) with the person's free weekdays; a free weekday never counts and cannot be booked | you |
| 7 | Public holidays | Filled in automatically each year, for all 26 cantons; the person picks their canton; a public holiday cannot be booked as holiday | you |
| 8 | Public-holiday source | OpenHolidays API: free, no key, open data; the app names it as the source | you |
| 9 | What the yearly request carries | Country, the person's canton and the year | you |
| 10 | Optional public holidays | The person ticks the ones their employer grants; a ticked one counts like a public holiday | you |
| 11 | Way of working | The whole year on one screen, each month its own panel; drag over free days books them, over holiday days removes them; a click on a day goes whole → half → none; every change can be undone; no dialogs (prototype 5) | you |
| 12 | Layout of the year | Months as columns (default) or as rows, chosen by the person | you |
| 13 | Additions kept | Bridge days marked; days left shown live while dragging; past days dimmed; public-holiday names in the cells | agent-introduced, you kept |
| 14 | Budget | M | you |
| 15 | Vision | v1.4.0: holiday planner in; hand corrections and overtime balance out; the yearly public-holiday request allowed | you |
| 17 | Requests for public holidays | Per canton-year for last, this and next year, per reload click, after a failure at the next start or 1 h later; the developer CLI's `holidays` command run by hand sends the same request; the Recorder never connects | you |
| 16 | Feature number | 002, the next free number, since corrections and balance are dropped | agent (how) |
