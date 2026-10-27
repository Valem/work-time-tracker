# ADR-006: UI — Preact + TypeScript in WebView2, a message bridge, and a locked-down, silent web view

**Status:** proposed · **Decided by:** owner (web UI in WebView2) · agent (library, bridge, lockdown)
· **Introduced by:** 001-foundation

## Context

The owner chose a web UI hosted in WebView2 (ADR-001). 001 needs three screens — week, day timeline,
lunch settings — and the series adds a day-correction form (002), a balance panel (003) and a year
planner (004). The app may send nothing over the network (outcome 7), but WebView2 is a Chromium runtime
with its own background services, and **no documented switch silences them all** [3]; SmartScreen
checks every navigation unless disabled [1]. Content served through a virtual-host folder mapping
does not raise `WebResourceRequested`, so request filtering cannot be the guard [2].

## Decision

- **Library:** Preact with TypeScript, bundled by Vite on Node LTS; its type check runs in the verify
  command (ADR-005). The screens follow the 001 concept's descriptions (week, day timeline, settings,
  recording status).
- **Serving:** the built assets ship in the install folder and load from a virtual host mapped to
  it; no dev server or remote URL in a release build.
- **Bridge:** typed JSON request/response messages over `chrome.webview.postMessage` and
  `WebMessageReceived`, one contract defined once — C# records and a closed set of refusal codes in
  the App, serialized with one shared `JsonSerializerOptions` (Web defaults: camelCase, enums as
  strings). The host answers only its own page's origin and known message types, validates every
  payload, and refuses with a code, never a text. No host objects injected into script.
- **Declarations:** a small in-repo build-time program, `Wtt.BridgeTypes`, runs after the App compiles,
  reads those records, codes and serializer options, and writes the TypeScript declarations the UI
  type check compiles against, so a drift between the two fails the verify command. It won over
  generator packages because about ten records and one enum need no dependency, and reading the
  serializer options themselves keeps the wire names and the declared names identical by construction.
- **Text and formats:** in the UI, a registry of locales, each one language's texts (every screen text
  and one per refusal code) joined with one region's formats (date, weekday, week, time, duration),
  over the bridge's plain values — never `Intl` or a JS `Date`. The types make every locale supply all
  of them, so adding a language or a format adds a definition and changes no screen (open/closed,
  owner). One place in the host decides the active locale, sets the host's UI culture from it — the
  host's own few messages come from its .NET resources — and passes its name to the UI, which looks it
  up in the registry. 001 ships German with Swiss formats (de-CH) only, always active.
- **Lockdown, all required:**
  - navigation outside the app host is cancelled; new windows are refused;
  - a Content-Security-Policy of `'self'` only — no remote scripts, styles, fonts or connections;
  - reputation checking (SmartScreen) off for the app's own pages; autofill and password saving off;
  - the WebView2 user-data folder lives in the app's data folder (ADR-004);
  - crash reports stay local: `IsCustomCrashReportingEnabled = true` for WebView2, and both executables
    are excluded from Windows Error Reporting per user (`WerAddExcludedApplication`).
- **Runtime:** the Evergreen WebView2 runtime that ships with Windows 11; its updates are Windows'
  own (Edge Update), not the app's.
- **Proof, not assumption:** the network traffic of the app's whole process tree, WebView2 children
  included, is measured on real runs (001 concept, Phase-2 probe, Phases 3 and 4). The only connections
  allowed are the runtime's own update and network-time checks to Microsoft (owner, after the Phase-2
  probe found them); any other connection is a finding.

## Rejected alternatives

| Option | Why not |
|--------|---------|
| No framework (plain TypeScript + DOM, like the prototype) | Fine for three read-only screens; 002's editing and 004's year planner would hand-build state and re-rendering |
| React | Same component model as Preact at ~10× the bundle |
| Vue, Svelte | Their own template compilers and conventions for no gain here |
| Lit / web components | Lower-level; more hand-written state for the same screens |
| Host objects (`AddHostObjectToScript`) | COM-visible classes, harder to version and test than plain messages |
| Declaration generator packages (TypeGen, Reinforced.Typings, Tapper) | A dependency and its own naming configuration, which must be kept matching the serializer's, for about ten records |
| Hand-written TypeScript declarations | A drift from the C# records would pass the type check and surface only at run time |

## Consequences

- Two languages meet only at the bridge contract.
- If any other runtime connection cannot be eliminated by configuration, it is a **blocker for the owner**:
  the options then are a Fixed Version WebView2 runtime in the installer (~+150 MB, its own updates)
  or a native UI.
- **Hard to undo:** medium — the screens are rewritten on a UI change; the core and bridge contract
  stay.

## Sources

1. CoreWebView2Settings.IsReputationCheckingRequired — https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2settings.isreputationcheckingrequired
2. Virtual hosts and WebResourceRequested — https://learn.microsoft.com/en-us/microsoft-edge/webview2/how-to/webresourcerequested · https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2.setvirtualhostnametofoldermapping
3. WebView2 browser flags (diagnostic use; no supported switch for background networking) — https://learn.microsoft.com/microsoft-edge/webview2/concepts/webview-features-flags
4. WerAddExcludedApplication — https://learn.microsoft.com/en-us/windows/win32/api/werapi/nf-werapi-weraddexcludedapplication
5. CoreWebView2EnvironmentOptions.IsCustomCrashReportingEnabled — https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2environmentoptions.iscustomcrashreportingenabled
