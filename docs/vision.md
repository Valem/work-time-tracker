# Work Time Tracker — Vision

**Version:** v1.3.0 — approved 2026-10-26

## Problem
Keeping track of working hours by hand is effort that is easily forgotten, while the     [you]
Windows laptop already knows when it was in use. The hours are needed for a personal
overview and for typing them into the employer's timesheet.

## For whom
- **Me** — my daily and weekly hours and my overtime balance, read off to type into my   [you]
  employer's timesheet, and my holidays planned for the year.
- **Colleagues and friends** — the same on their own Windows 11 laptops, installed by    [you]
  themselves, working without explanation.

## Success
- A workday appears without anyone doing anything: start, end, break and total.         [you]
- The day is counted as first activity → last activity, minus the lunch break: the       [you]
  longest lock or standby gap inside the person's lunch window, and at least their
  minimum break when there is no gap or a shorter one. A pause of 2 h or more at the
  day's end closes the day there; activity after it adds only its own minutes.
- A week can be read off and typed into the employer's timesheet as it stands, apart    [assumed]
  from days corrected by hand.
- A running overtime balance against each person's target hours, which stays right      [agent, kept]
  over holidays, vacation, sick days and private use because those days can be marked.
- At the beginning of the year a person enters the holiday days they plan to take for    [you]
  the full year.
- On first start the past days are already there, rebuilt from what Windows has kept    [agent, kept]
  (typically weeks to months, depending on the laptop).
- A colleague installs it on their own Windows 11 laptop and sees their first day        [assumed]
  without asking anyone how.

## Drivers
- **Zero daily effort** — records itself in the background. Rules out start/stop        [you]
  buttons and daily confirmations.
- **Data never leaves the laptop** — no cloud, no account, nothing sent anywhere.        [you]
  Rules out sync between devices and any online service. The browser engine that shows
  the screens may make its own update and time checks to Microsoft; they carry no data.
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
- Show remaining holiday days against the yearly entitlement.                            [agent]
