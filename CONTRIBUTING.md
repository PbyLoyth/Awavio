# Contributing to Awavio

Thanks for helping improve Awavio. Focused fixes, reproducible bug reports and tested Windows improvements are welcome.

## Report a problem

[Open an issue](https://github.com/PbyLoyth/Awavio/issues/new) with:

- Awavio version and Windows version.
- Whether you used an installed or portable copy.
- Whether the PC was plugged in and which settings were enabled.
- Steps to reproduce, expected behavior and actual behavior.
- Relevant lines from **Open log folder**, with personal information removed.

For lid or recovery issues, include the lid values before, during and after the session if you can reproduce them.

## Set up development

Use Windows 10 or 11 x64 and the **.NET 8 SDK or later** for the app. Core tests can also run on other platforms.

```powershell
git clone https://github.com/PbyLoyth/Awavio.git
cd Awavio
dotnet test .\tests\Awavio.Core.Tests
dotnet build .\Awavio.Windows.sln -c Release
dotnet run --project .\src\Awavio.Windows
```

If your PATH exposes only a .NET runtime, the source installer also checks `%USERPROFILE%\.dotnet\dotnet.exe` for an SDK. You can select an SDK explicitly with `-DotnetPath`.

## Project structure

| Path | Responsibility |
| --- | --- |
| `src/Awavio.Core` | Session lifecycle, validation, configuration, journal and recovery rules |
| `src/Awavio.Windows` | WinForms UI, tray, startup registration and Windows power APIs |
| `tests/Awavio.Core.Tests` | Platform-independent behavior tests |
| `tests/Awavio.Windows.Smoke` | Native Windows validation harness |
| `scripts` | Build, installation, removal and native-validation scripts |
| `docs` | User guide, Turkish README, design notes and images |
| `.github/windows-ci.template.yml` | Windows CI template |

Keep platform-independent session logic in the core project. Windows-specific calls and UI belong in the Windows project.

## Verify a change

Run the core tests and Release build for code changes:

```powershell
dotnet test .\tests\Awavio.Core.Tests -c Release
dotnet build .\Awavio.Windows.sln -c Release
```

For changes to power handling or recovery, run native checks on Windows as well. Quit other Awavio instances first:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\test-native.ps1 -RealPower -IsolatedPlan
```

The recommended isolated mode duplicates the current power plan, changes and restores the copy's lid actions, and removes the temporary plan afterward. It does not activate the copy or change your active plan. The test also checks a real timed awake request and recovery after process termination. Read [VALIDATION.md](VALIDATION.md) for its scope. It does not establish physical-lid, battery-discharge, sign-out or shutdown behavior.

Omitting `-IsolatedPlan` exercises the actual active plan instead. End active keep-awake sessions before deliberately using that mode.

For installer/removal changes, publish the executable before running the isolated removal fixture:

```powershell
dotnet publish .\src\Awavio.Windows -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o .\artifacts\publish\win-x64
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\test-uninstall.ps1
```

The fixture mocks task and startup cleanup to avoid removing the real installation. Check its actual result before claiming removal coverage.

For UI changes, check the duration window, widget states, tray menu, keyboard controls and scaling on Windows. Update the README images when the visible behavior changes.

The native duration-handler checks run without changing power settings:

```powershell
dotnet run --project tests/Awavio.Windows.Smoke -c Release -- --ui-only
```

## GitHub automation

The Windows workflow is provided as `.github/windows-ci.template.yml`. It is
inactive in this release because the publishing connection lacks GitHub's
`workflow` permission. To enable it using authorized workflow access, copy it
to `.github/workflows/windows.yml` and commit that file. Until then, use the
local checks above; no GitHub Actions success is claimed.

## Submit a pull request

Keep the change focused. Explain the user-visible problem, the resulting behavior, and how you verified it. Mention untested hardware or power scenarios explicitly.

Recovery changes should preserve these rules:

- Write the journal before changing a lid action.
- Restore only the values Awavio still owns.
- Leave a live session's recovery journal alone.
- Retain recovery data when restoration fails.
- Stop removal if the previous session cannot be recovered.

The project is distributed under the [MIT license](LICENSE). Preserve the license and attribution notices when reusing or redistributing code.
