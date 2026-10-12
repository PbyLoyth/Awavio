# Awavio 0.1.0 validation — 12 October 2026

Local checks ran on Windows x64, OS build 26300, in a non-elevated process.
The application targets .NET 8; release archives include the runtime.

## Results

- **41 core tests passed, 0 failed.** They cover session timing, battery rules,
  duration input, user power-setting changes, failed writes, failed awake
  requests, rollback and recovery. Invalid, incomplete and locked recovery
  journals are retained for attention instead of silently discarded.
- **Windows x64 self-contained publish succeeded**, with no build warnings or
  errors.
- **Two-minute native timer passed.** A separate, inactive copy of the power
  plan started with AC=1 / DC=1 (Sleep), changed to AC=0 / DC=0 (Do nothing),
  and returned to AC=1 / DC=1 on expiry. The user's active plan was not changed
  by this Awavio test.
- **The native awake request was checked:** `SetThreadExecutionState` reported
  the thread's system-required flag during the session and its release after
  expiry.
- **Process interruption recovery passed:** the published `Awavio.exe --recover`
  left a live owner's journal intact. After the owner was forcibly terminated,
  recovery restored the test plan and removed the journal.
- **Real WinForms controls were exercised:** duration presets select
  15/30/60/120 minutes, zero is rejected with feedback, and Start and Add return
  the selected duration. The tray menu was rebuilt 100 times and disposed.
- **Binary installation fixture passed without an SDK:** the executable,
  copyright notices and uninstaller were copied; shortcut and limited-privilege
  recovery task creation were requested. An unresolved journal blocked an
  update and preserved the installed executable.
- **Uninstall fixture passed:** failed recovery preserved the executable and
  journal without requesting task/startup removal; successful recovery removed
  the isolated installation and requested cleanup.
- **PowerShell parse checks passed.**
- **Product previews were rendered from the current native Windows controls**
  and visually checked at 100% scaling. They use example state, without
  starting an awake session or capturing unrelated desktop content.

Installer fixture tests mock shortcut creation, Task Scheduler registration
and registry/task deletion. They do not establish successful registration on
every Windows installation. A GitHub Windows workflow template covers core
tests, native duration controls, packaging and both installer guards. It is
inactive because the publishing connection lacks GitHub's workflow permission;
these results are from local Windows tests, not a GitHub Actions run.

## Repeat the checks

```powershell
dotnet test tests/Awavio.Core.Tests -c Release
dotnet run --project tests/Awavio.Windows.Smoke -c Release -- --ui-only
./scripts/package.ps1
./tests/test-install.ps1
./tests/test-uninstall.ps1
./scripts/test-native.ps1 -RealPower -IsolatedPlan
```

The last command creates and changes an inactive test power plan, makes a real
awake request for two minutes, and tests recovery after terminating its own
worker. Run it when no Awavio process owns a session. Omitting `-IsolatedPlan`
instead changes the active power plan and requires a separate hardware test.

## Boundaries

The physical lid was not closed. Actual battery discharge, sign-out/shutdown,
ARM64 execution, other display scaling factors and organizational power
policies were not tested. Inactive-plan API checks do not prove physical
closed-lid behavior on a particular laptop. Windows 10 is a supported target,
but was not the local test environment. The release is unsigned.
