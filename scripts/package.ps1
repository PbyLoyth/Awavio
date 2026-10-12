[CmdletBinding()]
param([string]$DotnetPath = 'dotnet', [string]$Version = '0.1.0')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$published = Join-Path $root 'artifacts\publish\win-x64'
& $DotnetPath publish (Join-Path $root 'src\Awavio.Windows') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $published
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
$package = Join-Path $root ('artifacts\package-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $package | Out-Null
Copy-Item -LiteralPath (Join-Path $published 'Awavio.exe') -Destination $package
Copy-Item -LiteralPath (Join-Path $root 'LICENSE'),(Join-Path $root 'ATTRIBUTION.md') -Destination $package
Copy-Item -LiteralPath (Join-Path $root 'SECURITY.md'),(Join-Path $root 'VALIDATION.md') -Destination $package
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'install.ps1'),(Join-Path $PSScriptRoot 'uninstall.ps1') -Destination $package
[IO.File]::WriteAllText((Join-Path $package 'Install.cmd'), "@echo off`r`npowershell -NoProfile -ExecutionPolicy Bypass -File `"%~dp0install.ps1`" -DesktopShortcut`r`nif errorlevel 1 (pause & exit /b 1)`r`nstart `"`" `"%LOCALAPPDATA%\Programs\Awavio\Awavio.exe`"`r`n")
[IO.File]::WriteAllText((Join-Path $package 'Uninstall.cmd'), "@echo off`r`npowershell -NoProfile -ExecutionPolicy Bypass -File `"%~dp0uninstall.ps1`"`r`npause`r`n")
[IO.File]::WriteAllText((Join-Path $package 'QUICKSTART.txt'), "Awavio $Version - Windows x64`r`n`r`n1. Extract the ZIP to a folder.`r`n2. Double-click Install.cmd. No administrator rights or .NET SDK required.`r`n3. Click the eye widget to start a timed awake session.`r`n4. Right-click for settings. Hold the active widget for 1.2 seconds to end.`r`n`r`nPortable mode: run Awavio.exe directly. Automatic scheduled recovery requires installation.`r`nUnsigned app: Windows may show a publisher/SmartScreen prompt.`r`nLearn more: https://github.com/PbyLoyth/Awavio`r`n")
$zip = Join-Path $root "artifacts\Awavio-$Version-win-x64.zip"
Compress-Archive -Path (Join-Path $package '*') -DestinationPath $zip -Force
$hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText((Join-Path $root 'artifacts\SHA256SUMS.txt'), "$hash  Awavio-$Version-win-x64.zip`n")
Write-Output $zip
Write-Output "SHA256: $hash"
