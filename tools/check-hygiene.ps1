#Requires -Version 7
# Hygiene check of the public repository, run by tools/verify.ps1:  pwsh tools/check-hygiene.ps1
# Checks every file git tracks or would add (untracked, not ignored) for real usage data and personal data:
#   - event-log exports and data files (*.evtx, *.db, *.db-shm, *.db-wal)
#   - email addresses, except the public commit identity and reserved example domains
#   - Windows SIDs naming a real machine, domain or tenant
#   - the owner's private patterns, when .git/info/private-patterns exists (never tracked, absent in CI)
#   - binary files: text with a UTF-16 or UTF-32 byte-order mark is read as text; any other file holding a NUL byte
#     fails unless tools/hygiene-allowed-binaries.txt lists it
# Addresses, SIDs and private patterns are looked for in each file's name as well as its content.
# Findings are reported as file:line only, never the matched text: CI logs are public. A match inside a printed
# file name is shown as ***.

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

$publicIdentity = '5167291+Valem@users.noreply.github.com'
$emailPattern = [regex]'(?<![A-Za-z0-9._%+-])[A-Za-z0-9._%+-]+@(?<domain>[A-Za-z0-9-]+(?:\.[A-Za-z0-9-]+)*\.[A-Za-z]{2,})'
# RFC 2606/6761 reserved names for examples and tests; "Function@library.dll" is how Inno Setup imports a DLL function.
$allowedDomain = [regex]'(?i)(^|\.)example\.(com|net|org)$|\.(example|test|invalid|localhost|dll)$'
# A SID names a machine or domain (S-1-5-21-a-b-c[-rid]) or an Entra tenant user (S-1-12-1-a-b-c-d). Synthetic
# fixtures use sub-authorities that are one digit followed by zeros (1000000000, 2, 9); real ones are random.
$sidPatterns = @(
    [regex]'(?i)\bS-1-5-21-(\d+)-(\d+)-(\d+)'
    [regex]'(?i)\bS-1-12-1-(\d+)-(\d+)-(\d+)-(\d+)'
)
$syntheticSubAuthority = [regex]'^[1-9]0*$'
$dataFile = [regex]'(?i)\.(evtx|db|db-shm|db-wal)$'

function Get-PrivatePatterns {
    $path = git -C $root rev-parse --git-path info/private-patterns
    if (-not [IO.Path]::IsPathRooted($path)) { $path = Join-Path $root $path }
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { return @() }
    @(Get-Content -LiteralPath $path -Encoding utf8 |
        ForEach-Object { $_.Trim() } |
        Where-Object { $_ -and -not $_.StartsWith('#') })
}

function Get-AllowedBinaries {
    $path = Join-Path $PSScriptRoot 'hygiene-allowed-binaries.txt'
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { return @() }
    @(Get-Content -LiteralPath $path -Encoding utf8 |
        ForEach-Object { $_.Trim() } |
        Where-Object { $_ -and -not $_.StartsWith('#') })
}

function Test-Prefix([byte[]] $bytes, [byte[]] $prefix) {
    if ($bytes.Length -lt $prefix.Length) { return $false }
    for ($i = 0; $i -lt $prefix.Length; $i++) { if ($bytes[$i] -ne $prefix[$i]) { return $false } }
    $true
}

# The file's text, decoded by its byte-order mark (UTF-8 without one), or $null for a binary file.
function Read-Text([byte[]] $bytes) {
    $boms = @(
        @{ Prefix = [byte[]](0xFF, 0xFE, 0x00, 0x00); Encoding = [Text.UTF32Encoding]::new($false, $false) }
        @{ Prefix = [byte[]](0x00, 0x00, 0xFE, 0xFF); Encoding = [Text.UTF32Encoding]::new($true, $false) }
        @{ Prefix = [byte[]](0xFF, 0xFE); Encoding = [Text.UnicodeEncoding]::new($false, $false) }
        @{ Prefix = [byte[]](0xFE, 0xFF); Encoding = [Text.UnicodeEncoding]::new($true, $false) }
        @{ Prefix = [byte[]](0xEF, 0xBB, 0xBF); Encoding = [Text.UTF8Encoding]::new($false) }
    )
    foreach ($bom in $boms) {
        if (Test-Prefix $bytes $bom.Prefix) {
            return $bom.Encoding.GetString($bytes, $bom.Prefix.Length, $bytes.Length - $bom.Prefix.Length)
        }
    }
    if ([Array]::IndexOf($bytes, [byte]0) -ge 0) { return $null }
    [Text.Encoding]::UTF8.GetString($bytes)
}

# Email addresses that are neither the public identity nor at a reserved example domain.
function Get-EmailMatches([string] $text) {
    foreach ($match in $emailPattern.Matches($text)) {
        if ($match.Value -ieq $publicIdentity -or $allowedDomain.IsMatch($match.Groups['domain'].Value)) { continue }
        $match
    }
}

# SIDs whose machine, domain or tenant parts are not synthetic.
function Get-RealSidMatches([string] $text) {
    foreach ($pattern in $sidPatterns) {
        foreach ($match in $pattern.Matches($text)) {
            $parts = $match.Groups | Select-Object -Skip 1 | ForEach-Object Value
            if ($parts | Where-Object { -not $syntheticSubAuthority.IsMatch($_) }) { $match }
        }
    }
}

function Get-PrivateMatches([string] $text) {
    if ($privateRegex) { $privateRegex.Matches($text) }
}

# A file path as it may be printed: each matched email address, SID or private pattern shown as ***.
function Hide-Matches([string] $text) {
    $hidden = [bool[]]::new($text.Length)
    foreach ($match in @(Get-EmailMatches $text) + @(Get-RealSidMatches $text) + @(Get-PrivateMatches $text)) {
        for ($i = $match.Index; $i -lt $match.Index + $match.Length; $i++) { $hidden[$i] = $true }
    }
    $shown = [Text.StringBuilder]::new()
    for ($i = 0; $i -lt $text.Length; $i++) {
        if (-not $hidden[$i]) { [void]$shown.Append($text[$i]) }
        elseif ($i -eq 0 -or -not $hidden[$i - 1]) { [void]$shown.Append('***') }
    }
    $shown.ToString()
}

$previousEncoding = [Console]::OutputEncoding
try {
    [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
    $listed = git -C $root ls-files --cached --others --exclude-standard -z
    if ($LASTEXITCODE -ne 0) { throw 'git ls-files failed.' }
}
finally {
    [Console]::OutputEncoding = $previousEncoding
}
$files = @(($listed -join '') -split "`0" | Where-Object { $_ } | Sort-Object -Unique)
$allowedBinaries = @(Get-AllowedBinaries)
$privatePatterns = @(Get-PrivatePatterns)
$privateRegex = if ($privatePatterns.Count -gt 0) {
    [regex]::new(($privatePatterns | ForEach-Object { [regex]::Escape($_) }) -join '|', 'IgnoreCase')
}

$findings = [ordered]@{
    'event-log export or data file'                         = [Collections.Generic.List[string]]::new()
    'email address'                                         = [Collections.Generic.List[string]]::new()
    'SID that is not synthetic'                             = [Collections.Generic.List[string]]::new()
    'private pattern (.git/info/private-patterns)'          = [Collections.Generic.List[string]]::new()
    'binary file not in tools/hygiene-allowed-binaries.txt' = [Collections.Generic.List[string]]::new()
}

$checked = 0
foreach ($file in $files) {
    # The whole path is checked, extension included: an address may end where the extension begins.
    $shown = Hide-Matches $file
    if ($dataFile.IsMatch($file)) { $findings['event-log export or data file'].Add($shown) }
    if (@(Get-EmailMatches $file).Count -gt 0) { $findings['email address'].Add($shown) }
    if (@(Get-RealSidMatches $file).Count -gt 0) { $findings['SID that is not synthetic'].Add($shown) }
    if ($privateRegex -and $privateRegex.IsMatch($file)) { $findings['private pattern (.git/info/private-patterns)'].Add($shown) }

    $path = Join-Path $root $file
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { continue }
    $checked++
    $text = Read-Text ([IO.File]::ReadAllBytes($path))
    if ($null -eq $text) {
        if ($allowedBinaries -notcontains $file) { $findings['binary file not in tools/hygiene-allowed-binaries.txt'].Add($shown) }
        continue
    }

    $lines = $text -split '\r?\n'
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        $at = "${shown}:$($i + 1)"
        if ($line.Contains('@') -and @(Get-EmailMatches $line).Count -gt 0) { $findings['email address'].Add($at) }
        if ($line.IndexOf('S-1-', [StringComparison]::OrdinalIgnoreCase) -ge 0 -and @(Get-RealSidMatches $line).Count -gt 0) { $findings['SID that is not synthetic'].Add($at) }
        if ($privateRegex -and $privateRegex.IsMatch($line)) {
            $findings['private pattern (.git/info/private-patterns)'].Add($at)
        }
    }
}

$total = ($findings.Values | Measure-Object -Property Count -Sum).Sum
if ($total -eq 0) {
    Write-Host "hygiene: clean ($checked files checked)"
    exit 0
}

Write-Host "hygiene: $total finding(s) - file:line only, the matched text is never printed (*** in a file name)" -ForegroundColor Red
foreach ($rule in $findings.Keys) {
    if ($findings[$rule].Count -eq 0) { continue }
    Write-Host "  ${rule}:"
    $findings[$rule] | ForEach-Object { Write-Host "    $_" }
}
exit 1
