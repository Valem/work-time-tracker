#Requires -Version 7
<#
.SYNOPSIS
    Release check: the App's web view does not preconnect to its own host app.wtt.example.

.DESCRIPTION
    The runtime would otherwise reach the network with the app's virtual host name as a page starts loading; the App
    turns that off through an undocumented profile preference (ADR-006), which a runtime update could rename. Run this
    before every release on a Windows 11 machine with the WebView2 runtime:

        pwsh tools/check-preconnect.ps1

    It builds the Debug App (which honours the WEBVIEW2_* variables; Release ignores them) and starts it twice on a
    fresh scratch data folder, each time with the runtime's net log on and all traffic sent to a proxy nobody listens
    on. The variables are given to the App's process only.
      1. The check: the App as built. Passes when no net-log event names app.wtt.example.
      2. The positive control: the same start with the runtime's profile moved, by WEBVIEW2_USER_DATA_FOLDER, to a
         profile whose preference says "predict" — the App's setting does not reach it. The net log must name
         app.wtt.example, or the harness could not see a preconnect and the check proves nothing.
    Each run waits until the App's page has loaded (read over the runtime's debugging port), then closes the App.

    Exit code 0: passed. 1: the App preconnects. 2: the harness could not prove anything (no page, no net log, or the
    positive control saw nothing). It prints the runtime version and counts only. It changes no Internet setting or
    certificate, never touches %LocalAppData%\WorkTimeTracker or an installed App, stops only the processes it
    started, and removes its scratch folder.

.PARAMETER SkipBuild
    Use the Debug App already built in src/Wtt.App/bin/Debug.

.PARAMETER SettleSeconds
    How long each run stays open after its page has loaded.
#>
[CmdletBinding()]
param(
    [switch] $SkipBuild,
    [int] $SettleSeconds = 5,
    [int] $TimeoutSeconds = 90
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$appProject = Join-Path $root 'src/Wtt.App/Wtt.App.csproj'
$appDll = Join-Path $root 'src/Wtt.App/bin/Debug/net10.0-windows/wtt.dll'
$hostName = 'app.wtt.example'
$predictionOn = 0

class HarnessFailure : System.Exception {
    HarnessFailure([string] $message) : base($message) {}
}

function Get-FreePort {
    $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
    $listener.Start()
    try { return $listener.LocalEndpoint.Port } finally { $listener.Stop() }
}

function Get-ProcessTree {
    param([int] $RootId, [datetime] $RootStart)
    $all = Get-CimInstance Win32_Process -Property ProcessId, ParentProcessId, CreationDate
    $tree = [System.Collections.Generic.List[object]]::new()
    $parents = [System.Collections.Generic.Queue[object]]::new()
    $parents.Enqueue(@{ Id = $RootId; Created = $RootStart })
    while ($parents.Count -gt 0) {
        $parent = $parents.Dequeue()
        foreach ($child in $all | Where-Object { $_.ParentProcessId -eq $parent.Id -and $_.ProcessId -ne $parent.Id -and $_.CreationDate -ge $parent.Created }) {
            $tree.Add($child)
            $parents.Enqueue(@{ Id = [int]$child.ProcessId; Created = $child.CreationDate })
        }
    }
    return $tree
}

function Stop-Started {
    param($Processes)
    foreach ($started in $Processes) {
        $process = Get-Process -Id $started.ProcessId -ErrorAction SilentlyContinue
        if ($process -and [Math]::Abs(($process.StartTime - $started.CreationDate).TotalSeconds) -lt 1) { $process.Kill() }
    }
}

function Invoke-Cdp {
    param([string] $WebSocketUrl, [string] $Expression)
    $socket = [System.Net.WebSockets.ClientWebSocket]::new()
    $socket.Options.Proxy = $null
    $timeout = [System.Threading.CancellationTokenSource]::new([TimeSpan]::FromSeconds(10))
    try {
        $socket.ConnectAsync([uri]$WebSocketUrl, $timeout.Token).GetAwaiter().GetResult()
        $request = @{ id = 1; method = 'Runtime.evaluate'; params = @{ expression = $Expression; returnByValue = $true } } |
            ConvertTo-Json -Compress
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($request)
        $socket.SendAsync([ArraySegment[byte]]::new($bytes), 'Text', $true, $timeout.Token).GetAwaiter().GetResult()
        $buffer = [byte[]]::new(65536)
        $reply = [System.IO.MemoryStream]::new()
        do {
            $received = $socket.ReceiveAsync([ArraySegment[byte]]::new($buffer), $timeout.Token).GetAwaiter().GetResult()
            $reply.Write($buffer, 0, $received.Count)
        } until ($received.EndOfMessage)
        return ([System.Text.Encoding]::UTF8.GetString($reply.ToArray()) | ConvertFrom-Json).result.result.value
    } finally {
        $socket.Dispose()
        $timeout.Dispose()
    }
}

function Wait-PageLoaded {
    param([System.Diagnostics.Process] $App, [int] $Port)
    $loaded = "document.readyState === 'complete' && location.host === '$hostName' && document.getElementById('app')?.childElementCount > 0"
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    while ([DateTime]::UtcNow -lt $deadline) {
        if ($App.HasExited) { throw [HarnessFailure]::new("the App exited (code $($App.ExitCode)) before its page loaded") }
        try {
            $page = Invoke-RestMethod -Uri "http://127.0.0.1:$Port/json/list" -NoProxy -TimeoutSec 5 |
                Where-Object { $_.type -eq 'page' -and ([uri]$_.url).Host -eq $hostName } | Select-Object -First 1
            if ($page -and (Invoke-Cdp -WebSocketUrl $page.webSocketDebuggerUrl -Expression $loaded) -eq $true) { return }
        } catch [System.Net.Http.HttpRequestException], [System.Net.WebSockets.WebSocketException], [System.OperationCanceledException] {
        }
        Start-Sleep -Milliseconds 500
    }
    throw [HarnessFailure]::new("the App's page did not load within $TimeoutSeconds s")
}

function Invoke-AppRun {
    param([string] $Folder, [string] $ProfileFolder)
    $runName = Split-Path $Folder -Leaf
    $data = Join-Path $Folder 'data'
    $netLog = Join-Path $Folder 'netlog.json'
    New-Item -ItemType Directory -Force $data | Out-Null
    $debugPort = Get-FreePort
    $deadProxy = Get-FreePort

    $start = [System.Diagnostics.ProcessStartInfo]::new((Get-Command dotnet).Source)
    $start.ArgumentList.Add($appDll)
    $start.ArgumentList.Add('--data')
    $start.ArgumentList.Add($data)
    $start.UseShellExecute = $false
    foreach ($name in @($start.Environment.Keys | Where-Object { $_ -like 'WEBVIEW2_*' })) { $start.Environment.Remove($name) | Out-Null }
    $start.Environment['WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS'] =
        "--log-net-log=`"$netLog`" --net-log-capture-mode=Everything --proxy-server=http://127.0.0.1:$deadProxy --remote-debugging-port=$debugPort"
    if ($ProfileFolder) { $start.Environment['WEBVIEW2_USER_DATA_FOLDER'] = $ProfileFolder }

    $app = [System.Diagnostics.Process]::Start($start)
    $started = @()
    try {
        Wait-PageLoaded -App $app -Port $debugPort
        Start-Sleep -Seconds $SettleSeconds
        $started = Get-ProcessTree -RootId $app.Id -RootStart $app.StartTime
        $app.Refresh()
        $null = $app.CloseMainWindow()
        if (-not $app.WaitForExit(20000)) { $app.Kill($true) }
        $deadline = [DateTime]::UtcNow.AddSeconds(20)
        while ([DateTime]::UtcNow -lt $deadline -and ($started | Where-Object { Get-Process -Id $_.ProcessId -ErrorAction SilentlyContinue })) {
            Start-Sleep -Milliseconds 250
        }
    } finally {
        if (-not $app.HasExited) { $app.Kill($true) }
        Stop-Started $started
    }
    $profileMark = Join-Path $runName ($ProfileFolder ? (Split-Path $ProfileFolder -Leaf) : 'data')
    return Read-NetLog -Path $netLog -ProfileMark (Join-Path (Split-Path $scratch -Leaf) $profileMark)
}

function Read-NetLog {
    param([string] $Path, [string] $ProfileMark)
    if (-not (Test-Path $Path)) { throw [HarnessFailure]::new('the runtime wrote no net log') }
    try { $log = [System.Text.Json.JsonDocument]::Parse([System.IO.File]::ReadAllText($Path)) }
    catch { throw [HarnessFailure]::new('the net log is incomplete (the runtime did not close it)') }
    try {
        $constants = $log.RootElement.GetProperty('constants')
        $client = $constants.GetProperty('clientInfo')
        $profileInUse = [regex]::Match($client.GetProperty('command_line').GetString(), '--user-data-dir="(?<folder>[^"]*)"').Groups['folder'].Value
        if (-not $profileInUse.Contains($ProfileMark + [System.IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
            throw [HarnessFailure]::new('the runtime opened another profile than the run gave it')
        }

        $eventNames = @{}
        foreach ($type in $constants.GetProperty('logEventTypes').EnumerateObject()) { $eventNames[$type.Value.GetInt32()] = $type.Name }
        $naming = @{}
        $events = 0
        foreach ($event in $log.RootElement.GetProperty('events').EnumerateArray()) {
            $events++
            if ($event.GetRawText().Contains($hostName, [StringComparison]::OrdinalIgnoreCase)) {
                $name = $eventNames[$event.GetProperty('type').GetInt32()]
                $naming[$name] = 1 + ($naming[$name] ?? 0)
            }
        }
        if ($events -eq 0) { throw [HarnessFailure]::new('the net log holds no events') }
        return [pscustomobject]@{
            Runtime = $client.GetProperty('version').GetString()
            Events  = $events
            Naming  = ($naming.Values | Measure-Object -Sum).Sum ?? 0
            ByType  = ($naming.GetEnumerator() | Sort-Object Name | ForEach-Object { "$($_.Name) $($_.Value)" }) -join ', '
        }
    } finally {
        $log.Dispose()
    }
}

function New-PredictingProfile {
    param([string] $Folder)
    $profileFolder = Join-Path $Folder 'EBWebView/Default'
    New-Item -ItemType Directory -Force $profileFolder | Out-Null
    @{ net = @{ network_prediction_options = $predictionOn } } | ConvertTo-Json -Compress |
        Set-Content -Path (Join-Path $profileFolder 'Preferences') -Encoding utf8NoBOM -NoNewline
    return $Folder
}

if (-not $SkipBuild) {
    Write-Host '==> Building the Debug App' -ForegroundColor Cyan
    dotnet build $appProject --configuration Debug --nologo --verbosity quiet
    if ($LASTEXITCODE -ne 0) { Write-Host 'check-preconnect: the Debug App did not build' -ForegroundColor Red; exit 2 }
}
if (-not (Test-Path $appDll)) { Write-Host 'check-preconnect: no Debug App built (run without -SkipBuild)' -ForegroundColor Red; exit 2 }

$scratch = Join-Path ([System.IO.Path]::GetTempPath()) ("wtt-preconnect-" + [guid]::NewGuid().ToString('N'))
try {
    Write-Host '==> Check: the App as built' -ForegroundColor Cyan
    $check = Invoke-AppRun -Folder (Join-Path $scratch 'check')
    Write-Host "    page loaded; $($check.Events) net-log events, $($check.Naming) name $hostName"

    Write-Host '==> Positive control: a profile that predicts' -ForegroundColor Cyan
    $controlFolder = Join-Path $scratch 'control'
    $control = Invoke-AppRun -Folder $controlFolder -ProfileFolder (New-PredictingProfile (Join-Path $controlFolder 'profile'))
    Write-Host "    page loaded; $($control.Events) net-log events, $($control.Naming) name $hostName ($($control.ByType))"

    if ($control.Naming -eq 0) {
        Write-Host "check-preconnect: HARNESS FAILED — the positive control saw no event naming $hostName on WebView2 runtime $($control.Runtime); the check proves nothing" -ForegroundColor Red
        exit 2
    }
    if ($check.Naming -gt 0) {
        Write-Host "check-preconnect: FAILED — the App names $hostName in $($check.Naming) net-log events on WebView2 runtime $($check.Runtime) ($($check.ByType))" -ForegroundColor Red
        exit 1
    }
    Write-Host "check-preconnect: passed — no preconnect to $hostName on WebView2 runtime $($check.Runtime) (positive control: $($control.Naming) events)" -ForegroundColor Green
    exit 0
} catch [HarnessFailure] {
    Write-Host "check-preconnect: HARNESS FAILED — $($_.Exception.Message)" -ForegroundColor Red
    exit 2
} finally {
    for ($attempt = 0; $attempt -lt 10 -and (Test-Path $scratch); $attempt++) {
        Remove-Item -Recurse -Force $scratch -ErrorAction SilentlyContinue
        if (Test-Path $scratch) { Start-Sleep -Milliseconds 500 }
    }
}
