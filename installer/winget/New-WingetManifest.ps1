#Requires -Version 7
<#
.SYNOPSIS
    Fills the winget manifest templates for one release and, unless -SkipValidate, runs winget validate.

.EXAMPLE
    pwsh installer/winget/New-WingetManifest.ps1 -Version 0.2.0 -License MIT `
        -InstallerPath artifacts/installer/wtt-setup.exe `
        -InstallerUrl https://github.com/Valem/work-time-tracker/releases/download/v0.2.0/wtt-setup.exe
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Version,
    [Parameter(Mandatory)] [string] $InstallerPath,
    [Parameter(Mandatory)] [string] $InstallerUrl,
    [Parameter(Mandatory)] [string] $License,
    [string] $PackageIdentifier = 'Valem.WorkTimeTracker',
    [string] $Publisher = 'Valem',
    [string] $ReleaseDate = (Get-Date -Format 'yyyy-MM-dd'),
    [string] $OutputDirectory,
    [switch] $SkipValidate
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $root "artifacts/winget/$Version" }

$tokens = [ordered]@{
    '__PACKAGE_IDENTIFIER__' = $PackageIdentifier
    '__VERSION__'            = $Version
    '__PUBLISHER__'          = $Publisher
    '__LICENSE__'            = $License
    '__RELEASE_DATE__'       = $ReleaseDate
    '__INSTALLER_URL__'      = $InstallerUrl
    '__INSTALLER_SHA256__'   = (Get-FileHash -Algorithm SHA256 -LiteralPath $InstallerPath).Hash
}

$manifests = [ordered]@{
    'package.version.yaml'         = "$PackageIdentifier.yaml"
    'package.installer.yaml'       = "$PackageIdentifier.installer.yaml"
    'package.locale.en-US.yaml'    = "$PackageIdentifier.locale.en-US.yaml"
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
foreach ($template in $manifests.Keys) {
    $text = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot $template)
    foreach ($token in $tokens.Keys) { $text = $text.Replace($token, $tokens[$token]) }
    $text = ($text -split "`r?`n" | Where-Object { $_ -notmatch '^# Template:' }) -join "`n"
    if ($text -match '__[A-Z0-9_]+__') { throw "$template still holds $($Matches[0])" }
    Set-Content -LiteralPath (Join-Path $OutputDirectory $manifests[$template]) -Value $text -Encoding utf8NoBOM -NoNewline
}
Write-Host "Manifest: $OutputDirectory" -ForegroundColor Green

if (-not $SkipValidate) {
    winget validate --manifest $OutputDirectory --disable-interactivity
    if ($LASTEXITCODE -ne 0) { throw "winget validate failed (exit code $LASTEXITCODE)" }
}
