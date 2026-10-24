#Requires -Version 7
# The project's one verify command, run locally and by CI:  pwsh tools/verify.ps1
# Exits non-zero on the first failing step.

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $root 'WorkTimeTracker.slnx'

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

# ==== UI checks hook (Phase-3) ==========================================================
# Phase-3 adds the UI's type check and Vitest run here, as further Invoke-Step lines
# (src/ui, Node LTS). Nothing else in this script or in CI needs to change for it.
# ========================================================================================

Write-Host 'verify: all checks passed' -ForegroundColor Green
