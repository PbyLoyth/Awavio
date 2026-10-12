<#
.SYNOPSIS
  Opt-in tests on Windows. Use -IsolatedPlan to change only an inactive copy.
#>
[CmdletBinding()]
param([Parameter(Mandatory)][switch]$RealPower, [switch]$IsolatedPlan, [string]$DotnetPath = 'dotnet')
$ErrorActionPreference = 'Stop'
if (-not $RealPower) { throw 'Real power-plan testing must be explicitly enabled.' }
$root = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $root 'artifacts'
$smokeDir = Join-Path $artifacts 'smoke'
$publishDir = Join-Path $artifacts 'publish\win-x64'
& $DotnetPath publish (Join-Path $root 'tests\Awavio.Windows.Smoke') -c Release -r win-x64 --self-contained true -o $smokeDir
if ($LASTEXITCODE -ne 0) { throw 'Smoke test build failed.' }
& $DotnetPath publish (Join-Path $root 'src\Awavio.Windows') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $publishDir
if ($LASTEXITCODE -ne 0) { throw 'App build failed.' }
$smoke = Join-Path $smokeDir 'Awavio.Windows.Smoke.exe'
$exe = Join-Path $publishDir 'Awavio.exe'
$oldHome = $env:AWAVIO_HOME
$worker = $null
try {
    $env:AWAVIO_HOME = Join-Path $artifacts ('native-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $env:AWAVIO_HOME | Out-Null
    $normalArgs = @('--real-power')
    if ($IsolatedPlan) { $normalArgs += '--isolated-plan' }
    $normal = Start-Process -FilePath $smoke -ArgumentList $normalArgs -WindowStyle Hidden -PassThru `
        -RedirectStandardOutput (Join-Path $env:AWAVIO_HOME 'timer.stdout') `
        -RedirectStandardError (Join-Path $env:AWAVIO_HOME 'timer.stderr')
    if (-not $normal.WaitForExit(240000)) { $normal.Kill(); $normal.WaitForExit(); throw 'Native timer test timed out.' }
    if ($normal.ExitCode -ne 0) { throw (Get-Content (Join-Path $env:AWAVIO_HOME 'timer.stderr') -Raw) }
    Get-Content (Join-Path $env:AWAVIO_HOME 'timer.stdout')

    $crashArgs = $normalArgs + '--hold'
    $worker = Start-Process -FilePath $smoke -ArgumentList $crashArgs -WindowStyle Hidden -PassThru `
        -RedirectStandardOutput (Join-Path $env:AWAVIO_HOME 'crash.stdout') `
        -RedirectStandardError (Join-Path $env:AWAVIO_HOME 'crash.stderr')
    $ready = Join-Path $env:AWAVIO_HOME 'ready'
    $deadline = (Get-Date).AddSeconds(90)
    while (-not (Test-Path -LiteralPath $ready)) {
        if ($worker.HasExited -or (Get-Date) -gt $deadline) { throw 'Crash test did not start.' }
        Start-Sleep -Milliseconds 200
    }
    $journal = Join-Path $env:AWAVIO_HOME 'recovery-journal.json'
    $busy = Start-Process -FilePath $exe -ArgumentList '--recover' -WindowStyle Hidden -Wait -PassThru
    if ($busy.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $journal)) { throw 'Recovery interfered with a live owner.' }
    $worker.Kill()
    $worker.WaitForExit()
    $worker = $null
    $recover = Start-Process -FilePath $exe -ArgumentList '--recover' -WindowStyle Hidden -Wait -PassThru
    if ($recover.ExitCode -ne 0 -or (Test-Path -LiteralPath $journal)) { throw 'Crash recovery failed.' }
    Write-Output 'PASS: live-owner recovery is a no-op; published app recovers after process termination.'
    Write-Output ('Evidence directory: ' + $env:AWAVIO_HOME)
} finally {
    if ($worker -and -not $worker.HasExited) { $worker.Kill(); $worker.WaitForExit() }
    if (Test-Path -LiteralPath $exe) {
        $cleanup = Start-Process -FilePath $exe -ArgumentList '--recover' -WindowStyle Hidden -Wait -PassThru
        if ($cleanup.ExitCode -ne 0) { Write-Warning 'Recovery needs attention; see the evidence directory.' }
    }
    if ($IsolatedPlan -and (Test-Path -LiteralPath (Join-Path $env:AWAVIO_HOME 'before.json'))) {
        $snapshot = Get-Content -LiteralPath (Join-Path $env:AWAVIO_HOME 'before.json') -Raw | ConvertFrom-Json
        $activePlanText = & powercfg.exe /getactivescheme
        if ($activePlanText -notmatch [regex]::Escape([string]$snapshot.Scheme)) {
            & powercfg.exe /delete ([string]$snapshot.Scheme)
            # A completed timer run already deleted its isolated plan.
        }
    }
    $env:AWAVIO_HOME = $oldHome
}
