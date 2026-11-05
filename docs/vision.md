# Work Time Tracker — Vision

**Version:** v1.4.2 — approved 2026-11-05

## Problem
Keeping track of working hours by hand is effort that is easily forgotten, while the     [you]
Windows laptop already knows when it was in use. The hours are needed for a personal
overview and for typing them into the employer's timesheet.

## For whom
- **Me** — my daily and weekly hours, read off to type into my employer's timesheet,     [you]
  and my holidays planned for the year.
- **Colleagues and friends** — the same on their own Windows 11 laptops, installed by    [you]
  themselves, working without explanation.

## Success
- A workday appears without anyone doing anything: start, end, break and total.         [you]
- The day is counted as first activity → last activity, minus the lunch break: the       [you]
  longest lock or standby gap inside the person's lunch window, and at least their
  minimum break when there is no gap or a shorter one. A pause of 2 h or more at the
  day's end closes the day there; activity after it adds only its own minutes.
- A week can be read off and typed into the employer's timesheet as it stands.          [assumed]
- At the beginning of the year a person enters the holiday days they plan to take for    [you]
  the full year, moves or cancels them as plans change, and sees them with their
  canton's public holidays in the week.
- On first start the past days are already there, rebuilt from what Windows has kept    [agent, kept]
  (typically weeks to months, depending on the laptop).
- A colleague installs it on their own Windows 11 laptop and sees their first day        [assumed]
  without asking anyone how.

## Drivers
- **Zero daily effort** — records itself in the background. Rules out start/stop        [you]
  buttons and daily confirmations.
- **Data never leaves the laptop** — no cloud, no account, nothing about the person's    [you]
  hours or holidays sent anywhere. Rules out sync between devices and any online service
  that receives them. The app fetches the public holidays of the person's canton from an
  open source: once per canton and year for last, this and next year, once per click to
  reload them, and after a failure once more at the next start or at a need an hour
  later; each request names only the country, canton and year. The browser engine that
  shows the screens may make its own update, time and configuration checks to Microsoft;
  they carry no data.
- **Free, no running cost** — no subscription, server or paid certificate. Rules out    [you]
  paid code signing; Windows may warn on first install.

## Not
- Not employee monitoring — nothing reaches the employer except what the person types    [assumed]
  in themselves.
- Not a project, task or client-billing tracker — one work time per day, no breakdown.   [you]
- Not an integration with employer systems — hours are read off, not exported.           [you]
- Not a stopwatch — no manual start/stop.                                                [you]

## Limits
- **Running cost:** none.                                                                [you]
- **Security:** each person's data stays on their own laptop, readable only by them.     [you]
- **Platform:** Windows 11 laptops.                                                      [you]
- **Installation:** without administrator rights preferred; admin is acceptable where   [you]
  nothing else works.
- **Hard to undo:** stack and distribution channel are decided by the owner at the       [agent]
  `001-foundation` concept gate.

## Ideas
- Export a period as CSV or copy it to the clipboard.                                    [agent]
- Treat keyboard/mouse idle time as a break.                                             [agent]
