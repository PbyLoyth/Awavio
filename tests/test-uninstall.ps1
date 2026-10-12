# Exercise the uninstaller using an isolated copy of the app. The real installed
# executable, scheduled task, startup entry and user data are never removed.
[CmdletBinding()]
param([string]$AppPath)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$fixture = [IO.Path]::GetFullPath((Join-Path $root ('artifacts\uninstall-' + [guid]::NewGuid().ToString('N'))))
$allowedRoot = [IO.Path]::GetFullPath((Join-Path $root 'artifacts')) + '\'
if (-not $fixture.StartsWith($allowedRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Fixture outside artifacts.' }
$install = Join-Path $fixture 'Programs\Awavio'
$fixtureData = Join-Path $fixture 'Awavio'
$exe = Join-Path $install 'Awavio.exe'
$journal = Join-Path $fixtureData 'recovery-journal.json'
$oldLocalAppData = $env:LOCALAPPDATA
$oldHome = $env:AWAVIO_HOME
$fixtureSettings = @{ TaskRemoved = $false; StartupRemoved = $false }

# These two external settings are represented by mocks; deletion on disk is real
# and restricted to the freshly-created fixture by LOCALAPPDATA/AWAVIO_HOME.
function Unregister-ScheduledTask {
    [CmdletBinding()]
    param([string]$TaskName, [switch]$Confirm)
    $fixtureSettings.TaskRemoved = $true
}
function Remove-ItemProperty {
    [CmdletBinding()]
    param([string]$Path, [string]$Name)
    $fixtureSettings.StartupRemoved = $true
}

try {
    New-Item -ItemType Directory -Path $install, $fixtureData | Out-Null
    if (-not $AppPath) { $AppPath = Join-Path $root 'artifacts\publish\win-x64\Awavio.exe' }
    Copy-Item -LiteralPath $AppPath -Destination $exe
    $env:LOCALAPPDATA = $fixture
    $env:AWAVIO_HOME = $fixtureData
    [IO.File]::WriteAllText($journal, '{invalid json')
    $blocked = $false
    try { & (Join-Path $root 'scripts\uninstall.ps1') } catch { $blocked = $true }
    if (-not $blocked -or -not (Test-Path -LiteralPath $exe) -or
        -not (Test-Path -LiteralPath $journal) -or $fixtureSettings.TaskRemoved -or $fixtureSettings.StartupRemoved) {
        throw 'Failed recovery did not preserve the complete fixture.'
    }
    Write-Output 'PASS: failed recovery preserves app, journal, task and startup entry.'
    Remove-Item -LiteralPath $journal
    & (Join-Path $root 'scripts\uninstall.ps1')
    if ((Test-Path -LiteralPath $install) -or -not $fixtureSettings.TaskRemoved -or -not $fixtureSettings.StartupRemoved) {
        throw 'Normal uninstall did not remove the fixture and request setting cleanup.'
    }
    Write-Output 'PASS: normal uninstall removes isolated app and requests task/startup cleanup.'
} finally {
    $env:LOCALAPPDATA = $oldLocalAppData
    $env:AWAVIO_HOME = $oldHome
}
