# Foundation — Intent Brief

**Version:** v1.3.0 — approved 2026-10-26 · **Feature:** 001-foundation

```text
Type:            new project (001-foundation)                                       [you]
Goal:            My workdays appear by themselves — start, end, lunch break, total —  [you]
                 per day and per week, on any Windows 11 laptop it is installed on.
Actors & story:  Me / a colleague → installs it once → sets lunch window and minimum   [you]
                 break tiers → works as usual → reads off the day or week for the
                 employer's timesheet.
Inputs/Outputs:  in: when the person used the laptop, as Windows records it; their     [you | repo]
                 lunch window and minimum break tiers. out: day and week view.
NOT:             no hand corrections, non-work days, overtime balance, holiday         [you]
                 planner or export yet — features 002–004. No start/stop buttons.
Drivers:         zero daily effort · data never leaves the laptop · free, no running  [you]
                 cost (from the vision)
Outcome:         1 a fresh install shows today's workday; setup is only lunch window   [you]
                   and minimum break tiers
                 2 start/end = first/last use of the laptop that day, within 1 minute;
                   everything in between is work except lunch; a last pause of 2 h or
                   more ends the day where it starts, and use after it adds only its
                   own minutes; Windows waking itself never counts
                 3 lunch: the longest lock/standby pause touching the window is
                   deducted whole, but at least the tier minimum for the day's length
                   (person sets tiers, e.g. > 5:30 → 0:15, > 7:00 → 0:30, > 9:00 → 1:00)
                 4 week view: each day's start, end, break, total, plus the week total
                 5 on first start, past days back to what Windows still keeps appear
                 6 recording goes on across restarts and standby, untouched
                 7 nothing is sent over the network
                 8 work past midnight counts on the next day from 00:00; the pause
                   from then until the next use is not work
Constraints:     Windows 11; target laptops may be company-managed; install without   [you]
                 admin rights preferred
Budget:          M — ≤ 4 phases, ≤ 2 review rounds per phase, ≤ 6 FRs, concept ≤ 400  [you]
Evidence:        target laptops' events and install policies → tools/wtt-check.ps1    [agent]
```

## Decision log

| # | Question | Decision | Decided by |
|---|----------|----------|------------|
| 1 | Scope of 001 | Automatic days only; corrections + non-work days 002, overtime balance 003, holiday planner 004 | you |
| 2 | Work past midnight | Split at midnight; the night pause after it is not work | you |
| 3 | Budget | M | you |
| 4 | Pauses outside lunch, e.g. evening use | Count as work: first use → last use, minus lunch — except a last pause of 2 h or more: the day ends where it starts, and use after it (e.g. a shutdown at home) adds only its own minutes (M1) | you |
| 5 | Minimum break on short days | Minimum by day length, in tiers the person configures | agent-introduced, you kept |
| 6 | Lunch pause crossing the window edge | Deducted whole | you |
| 7 | Install rights | Without admin preferred; admin acceptable where nothing else works | you |
| 8 | What counts as activity | The person using the laptop: use starts at unlock or sign-in, never at a wake (Windows logs some of its own night wakes as "keyboard") | agent (how) |
| 9 | Night pause | Only after work running past midnight; early use on other days starts the day like evening use ends it | you |
| 10 | Week | Starts Monday | agent (how) |
| 11 | Day length that picks the break tier | Net, after the break (as in working-time law): one more minute never lowers the total | you |
| 12 | Distribution | Public GitHub repo, installer on its releases, listed in winget's community repository; the Microsoft Store can be blocked on company-managed laptops, winget works there | you |
| 13 | Additions kept | Prefilled settings (11:30–14:00, example tiers); the day view explains ignored wakes and the lunch deduction | agent-introduced, you kept |
| 14 | Freezing past weeks | Cut: a rule fix in an update may change past weeks | you |
| 15 | Fingerprint wake | Counts as a person, like keyboard and mouse (M1) | you |
