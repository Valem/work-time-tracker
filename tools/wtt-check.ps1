# Work Time Tracker — read-only Windows check
# Counts which session/power events this laptop's Windows records. It reads event counts and
# power settings only: no names, no file contents, no network, and it changes nothing.
# Run:  powershell -ExecutionPolicy Bypass -File .\wtt-check.ps1
# Then send back the file wtt-check-result.txt written next to this script.

$since = (Get-Date).AddDays(-30)
$out = New-Object System.Collections.Generic.List[string]

$os = Get-CimInstance Win32_OperatingSystem
$out.Add("Windows: $($os.Caption) build $($os.BuildNumber)")
$out.Add("Domain-joined (corporate): " + (Get-CimInstance Win32_ComputerSystem).PartOfDomain)
$out.Add("Fast Startup on: " + ((Get-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Power' -Name HiberbootEnabled -ErrorAction SilentlyContinue).HiberbootEnabled -eq 1))
$out.Add("Sleep states: " + ((powercfg /a | Select-String -Pattern 'S0 Low Power Idle|Standby \(S3\)|Hibernate' | Select-Object -First 3) -join ' | ').Trim())

$dli = (Get-ItemProperty 'HKCU:\Control Panel\Desktop' -Name DelayLockInterval -ErrorAction SilentlyContinue).DelayLockInterval
$out.Add("Sign-in required after sleep: " + $(if ($null -eq $dli) { 'default' } elseif ([uint32]$dli -eq [uint32]::MaxValue) { 'never' } elseif ($dli -eq 0) { 'every time' } else { "after $dli s away" }))
$sac =(Get-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Control\CI\Policy' -Name VerifiedAndReputablePolicyState -ErrorAction SilentlyContinue).VerifiedAndReputablePolicyState
$out.Add("Smart App Control: " + $(switch ($sac) { 0 {'off'} 1 {'on (blocks unsigned apps)'} 2 {'evaluation'} default {'unknown'} }))
$storePolicy = Get-ItemProperty 'HKLM:\SOFTWARE\Policies\Microsoft\WindowsStore' -ErrorAction SilentlyContinue
$out.Add("Microsoft Store installed: " + [bool](Get-AppxPackage -Name Microsoft.WindowsStore -ErrorAction SilentlyContinue) +
    "  blocked by policy: " + [bool]($storePolicy.RemoveWindowsStore -eq 1 -or $storePolicy.DisableStoreApps -eq 1))
$ai = Get-ItemProperty 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\AppInstaller' -ErrorAction SilentlyContinue
$out.Add("winget policies (unset = allowed): AppInstaller=$($ai.EnableAppInstaller) StoreSource=$($ai.EnableMicrosoftStoreSource) DefaultSource=$($ai.EnableDefaultSource) AdditionalSources=$($ai.EnableAdditionalSources)")
$winget = Get-Command winget -ErrorAction SilentlyContinue
if ($winget) {
    $out.Add("winget version: " + (& winget --version 2>$null))
    $out.Add("winget sources: " + ((& winget source list 2>$null | Select-Object -Skip 2 | ForEach-Object { ($_ -split '\s+')[0] }) -join ', '))
} else { $out.Add("winget: not available") }
$appx = Get-ItemProperty 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\Appx' -ErrorAction SilentlyContinue
$out.Add("App package sideloading policy (unset = allowed): AllowAllTrustedApps=$($appx.AllowAllTrustedApps) BlockNonAdminUserInstall=$($appx.BlockNonAdminUserInstall)")
$out.Add("AppLocker exe rules present: " + [bool](Test-Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\SrpV2\Exe\*'))
$out.Add("Code integrity policies active (Smart App Control counts as one): " + @(Get-ChildItem "$env:windir\System32\CodeIntegrity\CiPolicies\Active\*.cip" -ErrorAction SilentlyContinue).Count)

$sys = Get-WinEvent -ListLog System
$oldest = (Get-WinEvent -LogName System -Oldest -MaxEvents 1).TimeCreated
$out.Add("System log size MB: $([math]::Round($sys.MaximumSizeInBytes/1MB,1))  oldest event: $($oldest.ToString('yyyy-MM-dd'))")
$out.Add("")
$out.Add("Event counts, last 30 days:")

$checks = @(
    @('System', 'Microsoft-Windows-Kernel-General', 12, 'OS start'),
    @('System', 'Microsoft-Windows-Kernel-General', 13, 'OS shutdown'),
    @('System', 'Microsoft-Windows-Kernel-Power', 42, 'Sleep entered'),
    @('System', 'Microsoft-Windows-Kernel-Power', 506, 'Modern Standby enter'),
    @('System', 'Microsoft-Windows-Kernel-Power', 507, 'Modern Standby exit'),
    @('System', 'Microsoft-Windows-Kernel-Power', 566, 'Session state change'),
    @('System', 'Microsoft-Windows-Power-Troubleshooter', 1, 'Wake from sleep'),
    @('System', 'Microsoft-Windows-Winlogon', 7001, 'Sign-in'),
    @('System', 'Microsoft-Windows-Winlogon', 7002, 'Sign-out'),
    @('System', 'User32', 1074, 'Shutdown/restart requested'),
    @('Microsoft-Windows-TerminalServices-LocalSessionManager/Operational', 'Microsoft-Windows-TerminalServices-LocalSessionManager', 21, 'Session logon'),
    @('Microsoft-Windows-TerminalServices-LocalSessionManager/Operational', 'Microsoft-Windows-TerminalServices-LocalSessionManager', 25, 'Session reconnect')
)
foreach ($c in $checks) {
    try { $n = @(Get-WinEvent -FilterHashtable @{ LogName = $c[0]; ProviderName = $c[1]; Id = $c[2]; StartTime = $since } -ErrorAction Stop).Count }
    catch { $n = 0 }
    $out.Add(("  {0,-28} {1,5}" -f $c[3], $n))
}

try { $n = @(Get-WinEvent -FilterHashtable @{ LogName = 'Security'; Id = 4800, 4801; StartTime = $since } -ErrorAction Stop).Count; $lock = "$n" }
catch { $lock = 'none / not readable' }
$out.Add(("  {0,-28} {1}" -f 'Lock/unlock (audit)', $lock))

try {
    $wl = Get-WinEvent -FilterHashtable @{ LogName = 'Microsoft-Windows-Winlogon/Operational'; Id = 811 } -ErrorAction Stop
    $sens = $wl | ForEach-Object { ([xml]$_.ToXml()).Event.EventData.Data } |
        Where-Object { $_.Name -eq 'Event' } | Group-Object '#text'
    $codes = ($sens | ForEach-Object { "code $($_.Name): $($_.Count)" }) -join ', '
    $out.Add(("  {0,-28} {1}  (oldest {2})" -f 'Winlogon 811 lock/unlock', $codes, ($wl | Select-Object -Last 1).TimeCreated.ToString('yyyy-MM-dd')))
} catch { $out.Add(("  {0,-28} {1}" -f 'Winlogon 811 lock/unlock', 'none / not readable')) }

$out.Add("")
$out.Add("Modern Standby exit reasons, last 30 days:")
try {
    Get-WinEvent -FilterHashtable @{ LogName = 'System'; ProviderName = 'Microsoft-Windows-Kernel-Power'; Id = 507; StartTime = $since } -ErrorAction Stop |
        ForEach-Object { if ($_.Message -match 'Reason:\s*(.+)') { $Matches[1].Trim() } else { '(none)' } } |
        Group-Object | Sort-Object Count -Descending |
        ForEach-Object { $out.Add(("  {0,-28} {1,5}" -f $_.Name, $_.Count)) }
} catch { $out.Add("  none") }

$file = Join-Path $PSScriptRoot 'wtt-check-result.txt'
$out | Set-Content -Path $file -Encoding utf8
$out
"`nWritten to $file"
