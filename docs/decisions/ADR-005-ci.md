# ADR-005: Build and CI — public GitHub repository with GitHub Actions

**Status:** proposed · **Decided by:** owner (public GitHub repository, brief decision 12) · agent
(pipeline) · **Introduced by:** 001-foundation

## Context

Releases must be reproducible and reviewed; each release needs an installer on a GitHub Release and a
winget manifest (ADR-002). No running cost is accepted. The repository holds no secrets.

## Decision

- **Pull request and push:** a `windows-latest` workflow runs the project's one verify command
  (`pwsh tools/verify.ps1`: .NET build with warnings as errors, `dotnet test`, the UI's type check and
  tests). Public repository: Actions minutes are free and unlimited.
- **Version tag:** the workflow builds the self-contained executables, the per-user installer and the
  developer CLI, and publishes them on a GitHub Release with their SHA-256 hashes, using only the
  workflow's own token.
- **winget:** the owner submits the release to `microsoft/winget-pkgs` from their GitHub account
  (`wingetcreate update --submit`); no personal token is stored in the repository.
- **Personal data never enters the repository:** committed test fixtures are synthetic event logs
  built from the observed patterns; real exports of anyone's event logs stay local, git-ignored, and
  are used only by local runs.

## Rejected alternatives

| Option | Why not |
|--------|---------|
| Private repository | winget installs from a public URL; the owner chose public |
| Automated winget submission from CI | Needs a stored personal access token for a fork |
| No CI, local builds | Releases unreproducible and unreviewed |

## Consequences

- GitHub holds the source code and the installers in public; no app data.
- **Hard to undo:** low — the workflow and the verify script move to any CI.
