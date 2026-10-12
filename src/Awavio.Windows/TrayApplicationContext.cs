using Awavio.Core;
using Microsoft.Win32;

namespace Awavio.Windows;

/// <summary>The notification-area icon and its menu. Everything runs on the UI thread,
/// which is also the thread that holds the keep-awake request.</summary>
sealed class TrayApplicationContext : ApplicationContext
{
    static readonly int[] FloorChoices = [0, 5, 10, 15, 20, 30];

    readonly ConfigStore configStore;
    readonly SessionController session;
    readonly Log log;
    readonly AppPaths paths;
    readonly NotifyIcon tray;
    readonly System.Windows.Forms.Timer ticker = new() { Interval = 1000 };
    readonly Icon idleIcon = TrayIcons.Draw(open: false, warning: false);
    readonly Icon activeIcon = TrayIcons.Draw(open: true, warning: false);
    readonly Icon warningIcon = TrayIcons.Draw(open: false, warning: true);
    readonly SynchronizationContext ui;
    AppConfig config;
    DurationForm? openDialog;
    WidgetForm? widget;

    public TrayApplicationContext(AppPaths paths)
    {
        this.paths = paths;
        log = new Log(paths.LogFile);
        configStore = new ConfigStore(paths.Config);
        config = configStore.Load();
        session = new SessionController(new WindowsPowerPlan(), new ExecutionStateKeepAwake(), new RecoveryJournal(paths.Journal), log);

        tray = new NotifyIcon { Icon = idleIcon, Visible = true, ContextMenuStrip = new ContextMenuStrip() };
        Theme.ApplyMenu(tray.ContextMenuStrip);
        tray.ContextMenuStrip.Opening += (_, e) => { BuildMenu(); e.Cancel = false; };
        tray.MouseClick += (_, e) =>
        {
            if (e.Button != MouseButtons.Left) return;
            log.Write("tray icon clicked");
            PromptDuration();
        };
        // Creating the menu installed the WinForms context; SystemEvents may fire on another thread.
        ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();

        ticker.Tick += (_, _) => OnTick();
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        SystemEvents.SessionEnding += OnSessionEnding;

        log.Write("app started");
        var recovered = session.Recover();
        if (recovered.Outcome == RestoreOutcome.Restored)
            Notify("Restored the lid setting from an earlier session that didn't end cleanly.");
        else if (!recovered.Ok)
            Notify("An earlier session couldn't be undone. Click the widget or the icon for options.", ToolTipIcon.Warning);
        if (config.ShowWidget) ShowWidget();
        ticker.Start();
        Refresh();
    }

    void ShowWidget()
    {
        if (widget is not null) return;
        Point? saved = config.WidgetX is { } x && config.WidgetY is { } y ? new Point(x, y) : null;
        widget = new WidgetForm(WidgetState, () =>
        {
            log.Write("widget clicked");
            PromptDuration();
        }, () =>
        {
            log.Write("widget held to end");
            EndSession(EndReason.User);
        }, location =>
        {
            config = config with { WidgetX = location.X, WidgetY = location.Y };
            SaveConfig();
        }, tray.ContextMenuStrip!, saved);
        widget.Show();
    }

    void HideWidget()
    {
        widget?.Close();
        widget?.Dispose();
        widget = null;
    }

    WidgetView WidgetState()
    {
        var now = DateTimeOffset.Now;
        if (session.PendingRecoveryError is not null)
            return new WidgetView(WidgetMode.NeedsAttention, "Needs attention", "An earlier session wasn't undone. Click for options.");
        if (session.IsActive)
            return new WidgetView(WidgetMode.Active, DurationInput.FormatRemaining(session.Remaining(now)),
                StatusText(now) + "\nClick to add time · press and hold to end · right-click for menu");
        return new WidgetView(WidgetMode.Idle, "Ready when you are", "Click to start a session · right-click for menu · drag to move");
    }

    void BuildMenu()
    {
        var items = tray.ContextMenuStrip!.Items;
        // Clear does not dispose the menu items and their native handles.
        while (items.Count > 0) items[0].Dispose();
        var now = DateTimeOffset.Now;

        items.Add(new ToolStripMenuItem("AWAVIO  ·  STAY IN FLOW") { Enabled = false });
        items.Add(new ToolStripMenuItem(StatusText(now)) { Enabled = false });
        if (session.PendingRecoveryError is { } problem)
        {
            items.Add(new ToolStripMenuItem("⚠ " + problem) { Enabled = false });
            items.Add("Retry restoring the lid setting", null, (_, _) => RetryRecovery());
            items.Add("Forget it (keep the current lid setting)", null, (_, _) =>
            {
                session.DiscardRecovery();
                Refresh();
            });
        }
        items.Add(new ToolStripSeparator());

        if (session.IsActive)
        {
            items.Add("Add time…", null, (_, _) => PromptDuration());
            items.Add(new ToolStripMenuItem("End session", null, (_, _) => EndSession(EndReason.User)) { ForeColor = Theme.Coral });
        }
        else
        {
            var start = new ToolStripMenuItem("Start session…", null, (_, _) => PromptDuration())
            {
                Enabled = session.PendingRecoveryError is null,
            };
            items.Add(start);
        }
        items.Add(new ToolStripSeparator());

        items.Add(Toggle("Stay awake with the lid closed", config.KeepAwakeWithLidClosed, !session.IsActive,
            on => config = config with { KeepAwakeWithLidClosed = on }));
        items.Add(Toggle("Keep the display on", config.KeepDisplayOn, true, on =>
        {
            config = config with { KeepDisplayOn = on };
            session.Rehold(on);
        }));

        var floor = new ToolStripMenuItem("End on battery below");
        foreach (var choice in FloorChoices)
        {
            var value = choice;
            floor.DropDownItems.Add(new ToolStripMenuItem(value == 0 ? "Off" : $"{value}%", null, (_, _) =>
            {
                config = config with { BatteryEndFloorPercent = value };
                SaveConfig();
            }) { Checked = config.BatteryEndFloorPercent == value });
        }
        items.Add(floor);

        items.Add(Toggle("Show widget above the taskbar", config.ShowWidget, true, on =>
        {
            config = config with { ShowWidget = on };
            if (on) ShowWidget(); else HideWidget();
        }));
        if (widget is not null)
            items.Add("Move widget back above the clock", null, (_, _) => widget?.ResetPosition());

        items.Add(new ToolStripMenuItem("Start with Windows", null, (_, _) =>
        {
            try
            {
                StartupRegistration.Set(!StartupRegistration.IsEnabled);
            }
            catch (Exception e)
            {
                ShowError("Couldn't change the startup setting: " + e.Message);
            }
        }) { Checked = StartupRegistration.IsEnabled });
        items.Add("Open log folder", null, (_, _) =>
        {
            var dir = Path.GetDirectoryName(paths.LogFile)!;
            Directory.CreateDirectory(dir);
            System.Diagnostics.Process.Start("explorer.exe", $"\"{dir}\"");
        });
        items.Add(new ToolStripSeparator());
        items.Add("Quit Awavio", null, (_, _) => Quit());
    }

    ToolStripMenuItem Toggle(string text, bool on, bool enabled, Action<bool> set) =>
        new(text, null, (_, _) => { set(!on); SaveConfig(); }) { Checked = on, Enabled = enabled };

    string StatusText(DateTimeOffset now) => session.IsActive
        ? $"Awake — {DurationInput.FormatRemaining(session.Remaining(now))} left (until {session.EndsAt!.Value.LocalDateTime:t})"
        : "No session running";

    void PromptDuration()
    {
        if (openDialog is not null)
        {
            openDialog.Activate();
            return;
        }
        if (!session.IsActive && session.PendingRecoveryError is not null && !ResolvePendingRecovery())
            return;
        var adding = session.IsActive;
        using var dialog = new DurationForm(
            adding ? "Awavio — add time" : "Awavio — stay awake for",
            adding ? "Add" : "Start",
            adding ? TimeSpan.FromHours(1) : TimeSpan.FromMinutes(config.LastDurationMinutes));
        openDialog = dialog;
        try
        {
            if (dialog.ShowDialog() != DialogResult.OK) return;
            if (adding && session.IsActive)
            {
                session.Extend(dialog.Duration);
            }
            else if (!session.IsActive)
            {
                session.Start(dialog.Duration, config, DateTimeOffset.Now);
                config = config with { LastDurationMinutes = (int)dialog.Duration.TotalMinutes };
                SaveConfig();
            }
        }
        catch (Exception e) when (e is SessionStartException or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            log.Write($"start/extend failed: {e}");
            var detail = e.InnerException is { } inner ? $" ({inner.Message})" : "";
            ShowError(e.Message + detail);
        }
        finally
        {
            openDialog = null;
            Refresh();
        }
    }

    void OnTick()
    {
        var status = SystemInformation.PowerStatus;
        var power = new PowerSnapshot(
            status.PowerLineStatus == PowerLineStatus.Offline,
            status.BatteryLifePercent is >= 0 and <= 1 && status.BatteryChargeStatus != BatteryChargeStatus.NoSystemBattery
                ? (int)Math.Round(status.BatteryLifePercent * 100)
                : null);
        if (session.Tick(DateTimeOffset.Now, power, config) is { } reason)
            AfterEnd(reason, session.PendingRecoveryError);
        Refresh();
    }

    void EndSession(EndReason reason)
    {
        session.End(reason);
        AfterEnd(reason, session.PendingRecoveryError);
        Refresh();
    }

    void AfterEnd(EndReason reason, string? error)
    {
        if (error is not null)
        {
            Notify("Session ended, but the lid setting couldn't be restored. Right-click the icon.", ToolTipIcon.Warning);
            return;
        }
        var message = reason switch
        {
            EndReason.Timer => "Session over. Your PC can sleep again.",
            EndReason.Battery => $"Battery below {config.BatteryEndFloorPercent}%. Session ended.",
            _ => null,
        };
        if (message is not null) Notify(message);
    }

    void RetryRecovery()
    {
        var result = session.Recover();
        if (result.Ok) Notify("Lid setting restored.");
        else ShowError("Still couldn't restore it: " + result.Error);
        Refresh();
    }

    /// <summary>Asks what to do about an earlier session that wasn't undone.
    /// Returns true once nothing blocks a new session.</summary>
    bool ResolvePendingRecovery()
    {
        log.Write("start blocked by pending recovery; asking the user");
        var answer = MessageBox.Show(
            "An earlier session's lid setting wasn't put back:\n\n" + session.PendingRecoveryError +
            "\n\nYes: try to restore it now.\nNo: forget it and keep the current lid setting.\nCancel: do nothing.",
            "Awavio", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button1, MessageBoxOptions.DefaultDesktopOnly);
        if (answer == DialogResult.Yes) RetryRecovery();
        else if (answer == DialogResult.No) session.DiscardRecovery();
        Refresh();
        return session.PendingRecoveryError is null;
    }

    void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        // After resume, catch up at once in case the end time passed while asleep.
        if (e.Mode == PowerModes.Resume) ui.Post(_ => OnTick(), null);
    }

    void OnSessionEnding(object? sender, SessionEndingEventArgs e)
    {
        // Sign-out or shutdown: put the lid setting back while we still can.
        // Send, not Post: this has to finish before Windows moves on.
        ui.Send(_ => { if (session.IsActive) session.End(EndReason.Shutdown); }, null);
    }

    void Refresh()
    {
        var now = DateTimeOffset.Now;
        tray.Icon = session.PendingRecoveryError is not null ? warningIcon : session.IsActive ? activeIcon : idleIcon;
        var text = "Awavio — " + StatusText(now).Replace("Awake — ", "");
        tray.Text = text.Length > 127 ? text[..127] : text;
        widget?.UpdateView(reassertTopMost: true);
    }

    void SaveConfig()
    {
        try
        {
            configStore.Save(config);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            log.Write($"saving config failed: {e.Message}");
        }
    }

    void Notify(string message, ToolTipIcon icon = ToolTipIcon.Info)
    {
        log.Write("notice: " + message);
        tray.ShowBalloonTip(5000, "Awavio", message, icon);
    }

    /// <summary>For problems the user caused by clicking: a dialog, because Windows
    /// hides balloon notifications under Do Not Disturb.</summary>
    void ShowError(string message)
    {
        log.Write("error shown: " + message);
        MessageBox.Show(message, "Awavio", MessageBoxButtons.OK, MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button1, MessageBoxOptions.DefaultDesktopOnly);
    }

    void Quit()
    {
        if (session.IsActive) session.End(EndReason.Quit);
        if (session.PendingRecoveryError is not null)
        {
            var answer = MessageBox.Show(
                "The lid setting couldn't be restored. If you quit now, the recovery task will retry later.\n\nQuit anyway?",
                "Awavio", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (answer != DialogResult.Yes)
            {
                Refresh();
                return;
            }
        }
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            SystemEvents.SessionEnding -= OnSessionEnding;
            if (session.IsActive) session.End(EndReason.Quit);
            ticker.Dispose();
            HideWidget();
            tray.Visible = false;
            tray.Dispose();
            idleIcon.Dispose();
            activeIcon.Dispose();
            warningIcon.Dispose();
            log.Write("app exited");
        }
        base.Dispose(disposing);
    }
}
