<#
.SYNOPSIS
  Install Awavio for the current user from a release ZIP or from source.
#>
[CmdletBinding()]
param([switch]$StartWithWindows, [switch]$DesktopShortcut, [string]$PublishedDirectory, [string]$DotnetPath)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$installDir = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Programs\Awavio'))
$exe = Join-Path $installDir 'Awavio.exe'
$taskName = 'Awavio recovery'
if (-not $PublishedDirectory -and (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'Awavio.exe'))) { $PublishedDirectory = $PSScriptRoot }
if (-not $PublishedDirectory) {
    if (-not $DotnetPath) {
        foreach ($candidate in @((Get-Command dotnet -ErrorAction SilentlyContinue).Source, (Join-Path $env:USERPROFILE '.dotnet\dotnet.exe'))) {
            if ($candidate -and (Test-Path -LiteralPath $candidate)) {
                $sdks = & $candidate --list-sdks
                if ($LASTEXITCODE -eq 0 -and $sdks) { $DotnetPath = $candidate; break }
            }
        }
    }
    if (-not $DotnetPath) { throw 'Building from source requires .NET SDK 8+. Release ZIPs need no SDK.' }
    $rid = if ($env:PROCESSOR_ARCHITECTURE -eq 'ARM64') { 'win-arm64' } else { 'win-x64' }
    $PublishedDirectory = Join-Path $root "artifacts\publish\$rid"
    & $DotnetPath publish (Join-Path $root 'src\Awavio.Windows') -c Release -r $rid --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $PublishedDirectory
    if ($LASTEXITCODE -ne 0) { throw 'Awavio could not be built. The existing installation was preserved.' }
}
$sourceExe = [IO.Path]::GetFullPath((Join-Path $PublishedDirectory 'Awavio.exe'))
if (-not (Test-Path -LiteralPath $sourceExe)) { throw "Awavio.exe was not found in $PublishedDirectory." }
$running = @(Get-Process -Name Awavio -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exe })
if ($running) { $running | Stop-Process -Force; $running | Wait-Process -Timeout 15 -ErrorAction SilentlyContinue }
# Recover even when the old process is already gone. A nonzero exit or remaining
# journal stops the upgrade before any installation file is replaced.
$dataDir = if ([string]::IsNullOrWhiteSpace($env:AWAVIO_HOME)) { Join-Path $env:LOCALAPPDATA 'Awavio' } else { [IO.Path]::GetFullPath($env:AWAVIO_HOME) }
$journal = Join-Path $dataDir 'recovery-journal.json'
if (Test-Path -LiteralPath $exe) {
    $recovery = Start-Process -FilePath $exe -ArgumentList '--recover' -Wait -PassThru -WindowStyle Hidden
    if ($recovery.ExitCode -ne 0 -or (Test-Path -LiteralPath $journal)) { throw 'The earlier session needs recovery. No installation files were replaced.' }
} elseif (Test-Path -LiteralPath $journal) {
    $recovery = Start-Process -FilePath $sourceExe -ArgumentList '--recover' -Wait -PassThru -WindowStyle Hidden
    if ($recovery.ExitCode -ne 0 -or (Test-Path -LiteralPath $journal)) { throw 'An earlier session needs recovery. Installation was stopped.' }
}
New-Item -ItemType Directory -Path $installDir -Force | Out-Null
if ($sourceExe -ne $exe) { Copy-Item -LiteralPath $sourceExe -Destination $exe -Force }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'uninstall.ps1') -Destination (Join-Path $installDir 'uninstall.ps1') -Force
foreach ($licenseName in @('LICENSE','ATTRIBUTION.md')) {
    $licensePath = Join-Path $PublishedDirectory $licenseName
    if (-not (Test-Path -LiteralPath $licensePath)) { $licensePath = Join-Path $root $licenseName }
    if (Test-Path -LiteralPath $licensePath) { Copy-Item -LiteralPath $licensePath -Destination (Join-Path $installDir $licenseName) -Force }
}
$action = New-ScheduledTaskAction -Execute $exe -Argument '--recover'
$atLogOn = New-ScheduledTaskTrigger -AtLogOn -User "$env:USERDOMAIN\$env:USERNAME"
$repeat = New-ScheduledTaskTrigger -Once -At (Get-Date) -RepetitionInterval (New-TimeSpan -Minutes 5)
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable -ExecutionTimeLimit (New-TimeSpan -Minutes 2) -MultipleInstances IgnoreNew
$principal = New-ScheduledTaskPrincipal -UserId "$env:USERDOMAIN\$env:USERNAME" -LogonType Interactive -RunLevel Limited
Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $atLogOn,$repeat -Settings $settings -Principal $principal -Force | Out-Null
if ($StartWithWindows) { Set-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name Awavio -Value "`"$exe`"" }
$shortcutShell = New-Object -ComObject WScript.Shell
$shortcutTargets = @(Join-Path ([Environment]::GetFolderPath('Programs')) 'Awavio.lnk')
if ($DesktopShortcut) { $shortcutTargets += Join-Path ([Environment]::GetFolderPath('Desktop')) 'Awavio.lnk' }
foreach ($shortcutPath in $shortcutTargets) {
    $shortcut = $shortcutShell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = $exe
    $shortcut.WorkingDirectory = $installDir
    $shortcut.IconLocation = "$exe,0"
    $shortcut.Save()
}
Write-Host "Awavio installed: $exe"
Write-Host 'Recovery task and Start menu shortcut are ready.'
