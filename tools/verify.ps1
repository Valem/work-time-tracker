#Requires -Version 7
# The project's one verify command, run locally and by CI:  pwsh tools/verify.ps1
# .NET build (warnings as errors; the App's build also restores and bundles src/ui, and Wtt.BridgeTypes writes the
# bridge declarations from C#) -> .NET tests -> the UI type check against those declarations -> hygiene.
# Needs the .NET SDK and Node.js (the version in src/ui/.nvmrc). Exits non-zero on the first failing step.

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $root 'WorkTimeTracker.slnx'
$ui = Join-Path $root 'src/ui'

function Invoke-Step {
    param([string] $Name, [scriptblock] $Command)
    Write-Host "==> $Name" -ForegroundColor Cyan
    & $Command
    if ($LASTEXITCODE -ne 0) {
        Write-Host "verify FAILED: $Name" -ForegroundColor Red
        exit $LASTEXITCODE
    }
}

Invoke-Step '.NET build (warnings as errors)' { dotnet build $solution --configuration Release -warnaserror --nologo }
Invoke-Step '.NET tests' { dotnet test $solution --configuration Release --no-build --nologo }

Invoke-Step 'UI type check' { node (Join-Path $ui 'node_modules/typescript/bin/tsc') --noEmit --project $ui }

Invoke-Step 'Hygiene (public repository)' { & (Join-Path $PSScriptRoot 'check-hygiene.ps1') }

Write-Host 'verify: all checks passed' -ForegroundColor Green
