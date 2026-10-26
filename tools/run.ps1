#Requires -Version 7
<#
.SYNOPSIS
    Builds and starts the app locally, or publishes it and compiles the per-user installer.

.DESCRIPTION
    Default: builds the Recorder and the app, starts the Recorder unless this session already runs one, then
    opens the app. Both run through the signed dotnet host, so Smart App Control never meets an unsigned
    executable during development.

    -Publish publishes the app and the Recorder self-contained for win-x64 into one folder.
    -Installer also compiles installer\wtt.iss with Inno Setup into artifacts\installer\wtt-setup.exe.

.EXAMPLE
    pwsh tools/run.ps1
.EXAMPLE
    pwsh tools/run.ps1 -Data $env:TEMP\wtt-try        # a Recorder started here and the app keep their data in another folder
.EXAMPLE
    pwsh tools/run.ps1 -Installer -Version 0.2.0
.EXAMPLE
    pwsh tools/run.ps1 -Installer -SkipPublish -PublishDir C:\somewhere\published
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug',
    [string] $Data,
    [switch] $Publish,
    [switch] $Installer,
    [switch] $SkipPublish,
    [string] $Version = '0.0.0',
    [string] $PublishDir
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$appProject = Join-Path $root 'src/Wtt.App/Wtt.App.csproj'
$recorderProject = Join-Path $root 'src/Wtt.Recorder/Wtt.Recorder.csproj'
if (-not $PublishDir) { $PublishDir = Join-Path $root 'artifacts/publish' }

function Invoke-Native {
    param([string] $Name, [scriptblock] $Command)
    Write-Host "==> $Name" -ForegroundColor Cyan
    & $Command
    if ($LASTEXITCODE -ne 0) { throw "$Name failed (exit code $LASTEXITCODE)" }
}

function Assert-RecorderProject {
    if (-not (Test-Path $recorderProject)) { throw "The Recorder project is missing: $recorderProject" }
}

# The Recorder's single-instance mutex, as Wtt.Infrastructure.DataFolder names it: the shared name for the
# person's own data folder, a name of its own for any other (the first 16 hex digits of the SHA-256 of the
# full path, without a trailing separator, upper-cased, in UTF-8).
function Get-RecorderMutexName {
    param([string] $DataFolder)
    $base = 'Local\WorkTimeTracker.Recorder'
    $normalize = { param($path) [System.IO.Path]::GetFullPath($path).TrimEnd('\', '/').ToUpperInvariant() }
    $default = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'WorkTimeTracker'
    if (-not $DataFolder -or (& $normalize $DataFolder) -eq (& $normalize $default)) { return $base }
    $hash = [System.Security.Cryptography.SHA256]::HashData([System.Text.Encoding]::UTF8.GetBytes((& $normalize $DataFolder)))
    return "$base.$([Convert]::ToHexString($hash).Substring(0, 16))"
}

function Test-RecorderRunning {
    param([string] $DataFolder)
    $mutex = $null
    try {
        if ([System.Threading.Mutex]::TryOpenExisting((Get-RecorderMutexName $DataFolder), [ref] $mutex)) { $mutex.Dispose(); return $true }
        return $false
    }
    catch [System.UnauthorizedAccessException] { return $true }
}

function Find-InnoCompiler {
    $candidates = @(
        (Get-Command 'ISCC.exe' -ErrorAction SilentlyContinue)?.Source
        (Join-Path $env:LOCALAPPDATA 'Programs/Inno Setup 6/ISCC.exe')
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6/ISCC.exe')
        (Join-Path $env:ProgramFiles 'Inno Setup 6/ISCC.exe')
    )
    $found = $candidates | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
    if (-not $found) {
        throw 'Inno Setup 6 is not installed. Per user, without admin rights: winget install --id JRSoftware.InnoSetup --scope user'
    }
    return $found
}

function Publish-Programs {
    Assert-RecorderProject
    if (Test-Path $PublishDir) { Remove-Item $PublishDir -Recurse -Force }
    foreach ($project in $appProject, $recorderProject) {
        Invoke-Native "Publish $(Split-Path -LeafBase $project)" {
            dotnet publish $project --configuration Release --runtime win-x64 --self-contained true `
                -p:Version=$Version --output $PublishDir --nologo
        }
    }
}

function Build-Installer {
    $compiler = Find-InnoCompiler
    $script = Join-Path $root 'installer/wtt.iss'
    $output = Join-Path $root 'artifacts/installer'
    Invoke-Native 'Compile installer' {
        & $compiler /Qp "/DPublishDir=$((Resolve-Path $PublishDir).Path)" "/DAppVersion=$Version" "/DOutputDir=$output" $script
    }
    Write-Host "Installer: $(Join-Path $output 'wtt-setup.exe')" -ForegroundColor Green
}

function Start-Locally {
    Assert-RecorderProject
    Invoke-Native 'Build Recorder' { dotnet build $recorderProject --configuration $Configuration --nologo }
    Invoke-Native 'Build app' { dotnet build $appProject --configuration $Configuration --nologo }

    $output = "bin/$Configuration/net10.0-windows"
    $dataPath = if ($Data) { $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Data) }
    $dataFolder = if ($dataPath) { "`"$dataPath`"" }
    if (Test-RecorderRunning $dataPath) {
        Write-Host 'The Recorder already runs in this session for this data folder.'
    }
    else {
        $recorder = @("`"$(Join-Path $root "src/Wtt.Recorder/$output/wtt-recorder.dll")`"")
        if ($dataFolder) { $recorder += '--data-dir', $dataFolder }
        Start-Process dotnet -ArgumentList $recorder -WindowStyle Hidden
        Write-Host 'Recorder started.'
    }

    $app = @("`"$(Join-Path $root "src/Wtt.App/$output/wtt.dll")`"")
    if ($dataFolder) { $app += '--data', $dataFolder }
    Start-Process dotnet -ArgumentList $app
    Write-Host 'App started.'
}

if ($Publish -or $Installer) {
    if (-not $SkipPublish) { Publish-Programs }
    if ($Installer) { Build-Installer }
}
else {
    Start-Locally
}
