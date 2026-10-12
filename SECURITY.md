# Security

Awavio changes Windows power settings during a timed awake session. Review the behavior below before relying on it. The current release is experimental; the validation record is in [VALIDATION.md](VALIDATION.md).

## Reporting a vulnerability

Report against a release tag or commit and include a minimal, non-destructive reproduction. If this repository's Security page offers **Report a vulnerability**, use that private channel. Otherwise open an issue that asks the maintainer for a private contact, without publishing exploit details or sensitive data.

Do not include passwords, access tokens, personal logs or unrelated machine information in a public issue. There is no guaranteed security-response time.

## What the app changes

- An awake request uses Windows `SetThreadExecutionState`. The request is associated with the app's UI thread and is released when that thread exits. It does not prevent every possible Windows sleep or shutdown condition.
- With lid mode enabled, Awavio records the current power plan's plugged-in and battery lid-close actions before setting both to **Do nothing**. Normal session end and recovery attempt to restore the recorded values. A value changed away from **Do nothing** during a session is retained.
- A per-user scheduled task runs `Awavio.exe --recover` at sign-in and every five minutes. Recovery needs a usable journal, working power APIs and a chance for Windows to run the task. It is a retry mechanism, not a guarantee after power loss or storage failure.
- **Start with Windows** uses the current user's `HKCU` Run key. Installation does not require administrator rights or install a service.

Power-plan settings affect the machine beyond the app. End sessions in other tools that alter lid actions before using Awavio. In particular, do not run overlapping lid sessions in Insomnia and Awavio. Multiple users can change the same Windows power plan. A Windows policy or another program can override the app's settings.

Awavio does not detect temperature or provide thermal protection. Test a short session on your machine with the lid open first, then check both lid-close settings after the session ends. Use an awake laptop on a stable, ventilated surface; do not put it in a closed bag.

## Local data and privacy

The normal data location is `%LOCALAPPDATA%\Awavio`. It contains `config.json`, `recovery-journal.json` and `Logs\awavio.log`. The journal stores session times, a power-plan identifier and the prior lid-close settings. Logs record session times, recovery results and diagnostic exceptions; those exceptions may contain local paths.

The app does not send telemetry or upload these files. Building from source downloads SDK dependencies through the .NET toolchain. The installed app does not need a GitHub account or an access token.

Files inherit the Windows directory permissions. They are not encrypted, and Awavio is not a security boundary against another program running under the same user account. Keep the data directory private and omit personal diagnostics when sharing bug reports. `AWAVIO_HOME` is an optional test override; do not point it at a shared or untrusted directory. A recovery task must see the same data location as the interactive app.

Do not delete or edit a recovery journal while a session is active. An unreadable journal needs review or retry; choosing to forget a journal keeps the current lid setting and removes the stored recovery information.

## Installation and releases

The installer places the application under `%LOCALAPPDATA%\Programs\Awavio` and registers the current user's recovery task. Upgrades and removal attempt recovery before replacing or deleting installed files. An unsuccessful recovery should preserve the application and its recovery evidence.

Release executables are not Authenticode signed unless the release notes explicitly state otherwise. Use the archives from [PbyLoyth/Awavio](https://github.com/PbyLoyth/Awavio), compare the published SHA-256 checksums when provided, and review the source or build it yourself when appropriate. A matching checksum detects changed bytes; it does not establish that the software is defect-free. Automated tests likewise do not prove physical lid behavior, policy compatibility or thermal safety on every PC.
