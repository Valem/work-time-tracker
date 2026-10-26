# ADR-002: Distribution — per-user installer on GitHub Releases, listed in winget's community repository

**Status:** proposed · **Decided by:** owner (channel, public repo, per-user, unsigned at first) ·
agent (installer type, Recorder autostart) · **owner, decided 2026-10-24:** the per-machine fallback · **Introduced by:** 001-foundation

## Context

Colleagues install it themselves and it must work without explanation (vision). The drivers rule out
paid code signing, a running cost and any network code in the app. The Microsoft Store can be blocked on
company-managed laptops while winget works there (brief decision 12). winget installs an
MSIX only with a trusted signature, so an unsigned MSIX is not an option. The Recorder must start at
every sign-in, without admin rights.

## Decision

- **Installer:** an **Inno Setup** installer built **per user** (`PrivilegesRequired=lowest`): it
  installs into `%LocalAppData%\Programs`, adds an entry under Installed apps, supports silent switches
  (winget installer type `inno`, scope `user`), upgrades in place under the same app id, and closes and
  restarts the Recorder around an upgrade. Direct install: `wtt-setup.exe /VERYSILENT /CURRENTUSER`.
- **Per-machine fallback (owner approved 2026-10-24; used only where a target laptop's evidence shows a block):** the installer is built
  with `PrivilegesRequiredOverridesAllowed=commandline`, so with admin rights it can install into
  Program Files (winget `--scope machine`) where AppLocker blocks executables in `%LocalAppData%`. It
  then writes the `Run` value under `HKLM\Software\Microsoft\Windows\CurrentVersion\Run` (so a
  separate admin account used to elevate gets nothing in its own profile), and the App or Recorder
  creates each person's data folder at first start. Uninstalling in this mode cannot remove other
  users' data folders, so 001 K3 holds only partly there.
- **Channel:** the installer is attached to a **GitHub Release** of the public repository (ADR-005) and
  listed in **winget's community repository** (`microsoft/winget-pkgs`): `winget install <id>`,
  updates by `winget upgrade`. The app contains **no update and no network code** (outcome 7).
- **Recorder autostart:** a per-user `Run` value under `HKCU\…\CurrentVersion\Run`, written by the
  installer and removed by the uninstaller; visible and switchable in Settings → Apps → Startup. The
  App starts the Recorder when it finds it stopped.
- **Uninstall** (per user) removes the program, the `Run` value and the data folder (001 K3, ADR-004).
- **Unsigned at first.** Free signing through SignPath Foundation (open-source projects) is a later
  option, the owner's to take.

## Rejected alternatives

| Option | Why not |
|--------|---------|
| Microsoft Store (MSIX) | Can be blocked on company-managed laptops |
| MSIX through winget or sideloading | Needs a CA-trusted signature — a running cost |
| Per-user MSI (WiX) | Per-user MSI has awkward upgrade and elevation quirks; Inno handles per-user natively |
| Portable / zip in winget | No uninstaller to remove the autostart entry and the data |
| Logon scheduled task for the Recorder | An at-logon trigger needs admin to register |
| Velopack or another self-updater | The update check is a network call from the app (outcome 7) |

## Consequences — what this commits the owner to

- A public GitHub repository and one pull request to `microsoft/winget-pkgs` per release, from the
  owner's GitHub account; moderators validate each submission. Checked before the first submission:
  `winget validate`, winget-pkgs `Tools/SandboxTest.ps1` (Windows Sandbox on the development laptop) and
  a Defender scan of the installed folder. (`winget install --manifest` needs an admin-only setting.)
- **Residual risk:** winget-pkgs' antivirus "Installers Scan" may flag an unsigned executable that
  starts itself at sign-in. Mitigation: SignPath Foundation signing, the owner's to take.
- **Residual risk:** on a company-managed laptop, the Defender attack-surface-reduction rule for
  removable media blocks an unsigned executable run from USB. The installer runs the app from the local
  disk, so it is not affected; signing would also clear it.
- **Residual risk:** where IT enforces the attack-surface-reduction rule that blocks executables unless
  they meet a prevalence, age or trusted-list criterion, Microsoft's cloud reputation decides, and each
  new release may be blocked until it builds reputation. Nothing on our side reliably clears it: the
  remedy is an IT exclusion, and signing helps only through reputation. A blocked Recorder shows as a
  recording gap (001 K2), filled from Windows history.
- **Unsigned:** a laptop with Smart App Control enforcing blocks the installed app until it is signed
  (development on such a laptop uses the signed `dotnet` host, never by turning Smart App Control
  off). AppLocker or WDAC rules on a managed laptop may block executables in
  `%LocalAppData%` — the target laptop's evidence (`tools/wtt-check.ps1`) settles this.
- **Hard to undo:** low — the winget id and the install folder carry over to a signed installer later.

## Sources

1. winget manifest schema, installer types and scope — https://learn.microsoft.com/en-us/windows/package-manager/package/manifest
2. Submitting to winget-pkgs — https://learn.microsoft.com/en-us/windows/package-manager/package/repository
3. Inno Setup `PrivilegesRequired` / overrides — https://jrsoftware.org/ishelp/topic_setup_privilegesrequired.htm · https://jrsoftware.org/ishelp/topic_setup_privilegesrequiredoverridesallowed.htm
7. winget-pkgs SandboxTest — https://github.com/microsoft/winget-pkgs/blob/master/Tools/SandboxTest.ps1
4. Run and RunOnce registry keys — https://learn.microsoft.com/en-us/windows/win32/setupapi/run-and-runonce-registry-keys
5. Smart App Control FAQ — https://support.microsoft.com/windows/smart-app-control-faq-285ea03d-fa88-4d56-882e-6698afdb7003
6. SignPath Foundation — https://signpath.org/terms
8. Attack surface reduction rules reference and FAQ — https://learn.microsoft.com/defender-endpoint/attack-surface-reduction-rules-reference · https://learn.microsoft.com/defender-endpoint/attack-surface-reduction-faq
