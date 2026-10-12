<#
.SYNOPSIS
  Removes Awavio for Windows. Undoes an open session first.
.PARAMETER Purge
  Also delete settings and logs in %LOCALAPPDATA%\Awavio.
#>
[CmdletBinding()]
param(
    [switch]$Purge
)
$ErrorActionPreference = 'Stop'

$installDir = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Programs\Awavio'))
$expectedInstallRoot = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Programs')) + '\'
if (-not $installDir.StartsWith($expectedInstallRoot,[StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid install directory.' }
$exe = Join-Path $installDir 'Awavio.exe'
$dataDir = if ([string]::IsNullOrWhiteSpace($env:AWAVIO_HOME)) {
    Join-Path $env:LOCALAPPDATA 'Awavio'
} else { [IO.Path]::GetFullPath($env:AWAVIO_HOME) }
$journal = Join-Path $dataDir 'recovery-journal.json'
$taskName = 'Awavio recovery'

Get-Process -Name 'Awavio' -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -eq $exe } |
    ForEach-Object { $_ | Stop-Process -Force; $_ | Wait-Process -Timeout 10 -ErrorAction SilentlyContinue }

if (Test-Path $exe) {
    $recovery = Start-Process -FilePath $exe -ArgumentList '--recover' -Wait -PassThru -WindowStyle Hidden
    if ($recovery.ExitCode -ne 0) { throw 'Session recovery failed, so nothing was removed.' }
}
if (Test-Path $journal) {
    throw "A session's lid change couldn't be undone, so nothing was removed. Open Awavio, use 'Retry restoring the lid setting' or 'Forget it', then run this again. Log: $dataDir\Logs\awavio.log"
}

Unregister-ScheduledTask -TaskName $taskName -Confirm:$false -ErrorAction SilentlyContinue
Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'Awavio' -ErrorAction SilentlyContinue
foreach ($shortcutPath in @((Join-Path ([Environment]::GetFolderPath('Programs')) 'Awavio.lnk'), (Join-Path ([Environment]::GetFolderPath('Desktop')) 'Awavio.lnk'))) {
    if (Test-Path -LiteralPath $shortcutPath) {
        $shortcutShell = New-Object -ComObject WScript.Shell
        $shortcut = $shortcutShell.CreateShortcut($shortcutPath)
        if ($shortcut.TargetPath -eq $exe) { Remove-Item -LiteralPath $shortcutPath -Force }
    }
}
if (Test-Path -LiteralPath $installDir) { Remove-Item -Recurse -Force -LiteralPath $installDir }
if ($Purge -and (Test-Path -LiteralPath $dataDir)) {
    $defaultDataDir = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Awavio'))
    if ([IO.Path]::GetFullPath($dataDir) -ne $defaultDataDir) {
        Write-Warning 'Custom AWAVIO_HOME was preserved. Only the default app data directory can be purged.'
    } else { Remove-Item -Recurse -Force -LiteralPath $defaultDataDir }
}

Write-Host 'Awavio was removed.'
