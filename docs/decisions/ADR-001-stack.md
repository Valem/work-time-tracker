# ADR-001: Stack — .NET 10 LTS (C#) with a native host window and a web UI in WebView2

**Status:** proposed · **Decided by:** owner (web UI over WPF, .NET) · agent (host framework, packages)
· **Introduced by:** 001-foundation

## Context

The tracker needs Windows-only capabilities — session lock/unlock and power notifications, the last
input time, the event logs — in a per-user background process, plus a window to read days and weeks.
The vision limits it to Windows 11 laptops and zero running cost. No domain skill covers a Windows
desktop stack, so the baseline was researched (sources below). The owner chose a web UI hosted in
WebView2 over WPF, knowing it brings two toolchains, a UI↔core bridge, heavier memory, and WebView2
network traffic that must be configured and proven silent (ADR-006).

## Decision

- **.NET 10 LTS, C#** for everything that is not the UI: the domain core and application layer as
  plain `net10.0` libraries without Windows calls; the infrastructure adapters, the headless
  **Recorder** and the **App** host as `net10.0-windows`. LTS support runs to November 2028 [1].
- **App host: Windows Forms with the WebView2 control.** The window holds one WebView2 and nothing
  else; all screens are web (ADR-006).
- **Recorder:** a windowless process with a hidden top-level window, never shown, for session and power
  notifications — a message-only window would miss the end-of-session messages that sign-out, shutdown
  and an installer's close request send.
- **Non-baseline packages:** `Microsoft.Web.WebView2` (the control), `Microsoft.Windows.CsWin32`
  (source-generated Win32 signatures instead of hand-written P/Invoke), `System.Diagnostics.EventLog`
  (event-log reader), `Microsoft.Data.Sqlite` (ADR-004). Tests: xUnit.
- Published self-contained into the per-user installer (ADR-002): no .NET runtime install for anyone.
  The developer CLI is published the same way, as one executable that runs without an install. On a
  laptop with Smart App Control enforcing, `dotnet run` launches the unsigned apphost, which Smart App
  Control blocks; `dotnet <path>\<project>.dll` uses the signed host, but Smart App Control may also
  check the unsigned assemblies it loads — a Phase-1 day-one check. If it fails, development runs in
  Windows Sandbox or a VM. Smart App Control is never turned off (it cannot be turned back on).

## Rejected alternatives

| Option | Why not |
|--------|---------|
| WPF UI (no web) | The owner chose the web UI |
| WPF as the host | More framework than one embedded control needs; no gain over Windows Forms |
| WinUI 3 host | Adds the Windows App SDK runtime, whose runtime telemetry is undocumented — a second unknown against outcome 7 |
| Tauri | A third language (Rust) for the same Win32 calls |
| Electron | Bundles its own Chromium (100 MB+) and network stack; Node runtime beside .NET |

## Consequences

- Two toolchains (dotnet + Node/npm) on every build machine and in CI; one verify command wraps both.
- The UI and the core meet only at the bridge contract (ADR-006); the core stays testable without
  Windows or a browser.
- One .NET LTS move before November 2028 (next LTS: .NET 12, November 2027).
- **Hard to undo:** medium — the core survives any UI change; the UI technology is ADR-006's.

## Sources

1. .NET support policy — https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core
2. WebView2 WinForms control — https://learn.microsoft.com/en-us/microsoft-edge/webview2/get-started/winforms
