[CmdletBinding()]
param([string]$PublishedDirectory)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (-not $PublishedDirectory) { $PublishedDirectory = Join-Path $root 'artifacts\publish\win-x64' }
$fixture = [IO.Path]::GetFullPath((Join-Path $root ('artifacts\install-' + [guid]::NewGuid().ToString('N'))))
$allowedRoot = [IO.Path]::GetFullPath((Join-Path $root 'artifacts')) + '\'
if (-not $fixture.StartsWith($allowedRoot,[StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid fixture path.' }
$oldLocalAppData = $env:LOCALAPPDATA
$oldDataHome = $env:AWAVIO_HOME
$installState = @{ Registered = $false; ShortcutCount = 0 }
function New-ScheduledTaskAction { param($Execute,$Argument) @{Execute=$Execute;Argument=$Argument} }
function New-ScheduledTaskTrigger { param([switch]$AtLogOn,$User,[switch]$Once,$At,$RepetitionInterval) @{} }
function New-ScheduledTaskSettingsSet { param([switch]$AllowStartIfOnBatteries,[switch]$DontStopIfGoingOnBatteries,[switch]$StartWhenAvailable,$ExecutionTimeLimit,$MultipleInstances) @{} }
function New-ScheduledTaskPrincipal { param($UserId,$LogonType,$RunLevel) @{RunLevel=$RunLevel} }
function Register-ScheduledTask {
    param($TaskName,$Action,$Trigger,$Settings,$Principal,[switch]$Force)
    if ($Principal.RunLevel -ne 'Limited' -or $Action.Argument -ne '--recover') { throw 'Unexpected task privileges or command.' }
    $installState.Registered = $true
}
function New-Object {
    param([string]$ComObject)
    if ($ComObject -ne 'WScript.Shell') { throw 'Unexpected COM request.' }
    $shell = [pscustomobject]@{}
    $shell | Add-Member -MemberType ScriptMethod -Name CreateShortcut -Value {
        param($Path)
        $shortcut = [pscustomobject]@{TargetPath='';WorkingDirectory='';IconLocation=''}
        $shortcut | Add-Member -MemberType ScriptMethod -Name Save -Value { $installState.ShortcutCount++ }
        return $shortcut
    }
    return $shell
}
try {
    New-Item -ItemType Directory -Path $fixture | Out-Null
    $env:LOCALAPPDATA = $fixture
    $env:AWAVIO_HOME = Join-Path $fixture 'Awavio'
    & (Join-Path $root 'scripts\install.ps1') -PublishedDirectory $PublishedDirectory -DotnetPath 'unused-sdk'
    $installed = Join-Path $fixture 'Programs\Awavio\Awavio.exe'
    if (-not (Test-Path -LiteralPath $installed) -or -not $installState.Registered -or $installState.ShortcutCount -ne 1) { throw 'Binary installation failed.' }
    foreach ($name in @('LICENSE','ATTRIBUTION.md','uninstall.ps1')) {
        if (-not (Test-Path -LiteralPath (Join-Path $fixture "Programs\Awavio\$name"))) { throw "Missing installed file: $name" }
    }
    $before = (Get-FileHash -LiteralPath $installed).Hash
    New-Item -ItemType Directory -Path $env:AWAVIO_HOME | Out-Null
    $journal = Join-Path $env:AWAVIO_HOME 'recovery-journal.json'
    [IO.File]::WriteAllText($journal,'{}')
    $blocked = $false
    try { & (Join-Path $root 'scripts\install.ps1') -PublishedDirectory $PublishedDirectory -DotnetPath 'unused-sdk' } catch { $blocked = $true }
    if (-not $blocked -or -not (Test-Path -LiteralPath $journal) -or (Get-FileHash -LiteralPath $installed).Hash -ne $before) { throw 'Unresolved recovery did not block upgrade.' }
    Write-Output 'PASS: binary installation needs no SDK; licenses, task and shortcut are present; unresolved recovery blocks upgrade.'
} finally {
    $env:LOCALAPPDATA = $oldLocalAppData
    $env:AWAVIO_HOME = $oldDataHome
}
