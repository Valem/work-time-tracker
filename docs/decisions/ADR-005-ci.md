# ADR-005: Build and CI — public GitHub repository with GitHub Actions

**Status:** proposed · **Decided by:** owner (public GitHub repository, brief decision 12; what the tests
are) · agent (pipeline) · **Introduced by:** 001-foundation

## Context

Releases must be reproducible and reviewed; each release needs an installer on a GitHub Release and a
winget manifest (ADR-002). No running cost is accepted. The repository holds no secrets.

## Decision

- **Pull request and push:** a `windows-latest` workflow runs the project's one verify command
  (`pwsh tools/verify.ps1`: .NET build with warnings as errors → `dotnet test` → hygiene check; the UI's
  type check joins it with the UI). Public repository: Actions minutes are free and unlimited.
- **Tests:** exactly one test per business rule, named `BR_nnn_...`, in `tests/Wtt.Rules.Tests` — a rule
  is encoded as written; a rule whose wording looks wrong is reported, never adjusted. One architecture
  test with negative probes, `tests/Wtt.Architecture.Tests`, enforces the feature concept's dependency
  view: Domain and Application reference neither Windows nor I/O. No other tests: acceptance criteria
  are proven by live checks of the built programs, with synthetic history replayed through the
  product's own importer (`wtt-cli import-xml`) where a scenario needs one. A UI test exists only where a business rule's
  test needs the UI.
- **Version tag:** the workflow builds the self-contained executables, the per-user installer and the
  developer CLI, and publishes them on a GitHub Release with their SHA-256 hashes, using only the
  workflow's own token.
- **winget:** the owner submits the release to `microsoft/winget-pkgs` from their GitHub account
  (`wingetcreate update --submit`); no personal token is stored in the repository.
- **Personal data never enters the repository:** committed test fixtures are synthetic event logs
  built from the observed patterns; real exports of anyone's event logs stay local, git-ignored, and
  are used only by local runs. The hygiene check (`tools/check-hygiene.ps1`) fails on a tracked
  `*.evtx` or `*.db` file, an email address other than the public noreply identity, or a SID that is
  not synthetic — in a file's content or its name — and on a file holding a NUL byte unless
  `tools/hygiene-allowed-binaries.txt` lists it (UTF-16 and UTF-32 text with a byte-order mark is read
  as text); a private term list kept outside git joins the check where present; findings print as
  file:line only, a match inside a file name as ***.

## Rejected alternatives

| Option | Why not |
|--------|---------|
| Private repository | winget installs from a public URL; the owner chose public |
| Automated winget submission from CI | Needs a stored personal access token for a fork |
| No CI, local builds | Releases unreproducible and unreviewed |

## Consequences

- GitHub holds the source code and the installers in public; no app data.
- Behaviour outside the business rules — import, store, bridge, Recorder, installer — has no automated
  regression test; a change there is proven by a live check. That includes the WebView2 lockdown
  (NFR-001: navigation, content security policy, local crash reports) and the refusal of a data file
  written by a newer version: QA's network probe and live runs prove them each phase.
- **Hard to undo:** low — the workflow and the verify script move to any CI.
