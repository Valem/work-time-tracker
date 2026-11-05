# Work Time Tracker

Works out your working time from when you use your Windows 11 laptop: each day's start, end, lunch break
and total, and each week's total, ready for your timesheet. A year planner books your holidays around your
canton's public holidays.

## What it does

- **Days and weeks from what Windows already knows.** It reads your laptop's own sign-in, lock and sleep
  history, so past days show from the first start, as far back as Windows keeps that history. From then on
  a small background program, started at sign-in, records each lock and unlock.
- **Lunch and breaks by your rule.** You set a lunch window and minimum breaks by day length; a pause
  outside the window counts as work.
- **Holiday planner.** Book holidays by dragging across the year; your canton's public holidays are counted
  in, with days taken, planned and left.

## Privacy

- Your times stay on your laptop, in your own profile (`%LocalAppData%\WorkTimeTracker`), readable only by
  you. Uninstalling removes them.
- The app sends nothing about you. Its only request fetches your canton's public holidays for a year from
  [OpenHolidays](https://www.openholidaysapi.org). The Microsoft Edge WebView2 runtime that draws its screens
  makes its own update checks to Microsoft.
- No account, no telemetry, no update code: updates come through winget.

## Install

```powershell
winget install Valem.WorkTimeTracker
```

Installs per user, without admin rights. Update with `winget upgrade Valem.WorkTimeTracker`; remove with
`winget uninstall Valem.WorkTimeTracker`, which removes your data too.

## Before you rely on it

- **Tested only on Windows 11 (x64) so far**, on one laptop and in Windows Sandbox.
- **German screens, Swiss formats and cantons** only.
- **Lock your screen when you leave** (Win+L): on many laptops a screen that just turns off counts as in
  use until the laptop locks.
- The installer is **not code-signed** yet; Windows may warn when you run it directly.

## Build from source

Needs the .NET 10 SDK, Node.js 24 and PowerShell 7.

```powershell
pwsh tools/verify.ps1     # build, tests, UI type check, hygiene
pwsh tools/run.ps1        # build and start the app
```

## Contributing

Bug reports and ideas are welcome as GitHub issues. Please open an issue before a pull request, so we can
agree on the change first. A pull request must pass `pwsh tools/verify.ps1`.

Never attach real usage data or Windows event-log exports to an issue: they show when you work. Describe the
times instead, or invent them.

## Licence

[MIT](LICENSE). Public-holiday data © OpenHolidays contributors,
[ODbL 1.0](https://github.com/openpotato/openholidaysapi.data).
