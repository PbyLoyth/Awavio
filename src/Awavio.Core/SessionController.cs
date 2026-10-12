namespace Awavio.Core;

public enum EndReason { User, Timer, Battery, Shutdown, Quit }

public sealed class SessionStartException(string message, Exception inner) : Exception(message, inner);

/// <summary>Outcome of putting the lid action back. Failed keeps the journal so
/// a later retry, the next launch or the scheduled backstop can try again.</summary>
public enum RestoreOutcome { NothingToRestore, Restored, LeftUserChange, Failed }

public sealed record RestoreResult(RestoreOutcome Outcome, string? Error = null)
{
    public bool Ok => Outcome != RestoreOutcome.Failed;
}

/// <summary>
/// Runs one timed awake session: journal first, then change the lid action and
/// hold the system awake; on end, release and put the lid action back.
/// Not thread-safe: call it from one thread (the UI thread on Windows).
/// </summary>
public sealed class SessionController(
    IPowerPlan powerPlan,
    IKeepAwake keepAwake,
    RecoveryJournal journal,
    Log log)
{
    JournalEntry? active;

    public bool IsActive => active is not null;
    public DateTimeOffset? EndsAt => active?.EndsAt;

    /// <summary>Set when the last restore failed and the journal is still on disk.</summary>
    public string? PendingRecoveryError { get; private set; }

    public TimeSpan Remaining(DateTimeOffset now) =>
        active is null ? TimeSpan.Zero : (active.EndsAt - now) < TimeSpan.Zero ? TimeSpan.Zero : active.EndsAt - now;

    public void Start(TimeSpan duration, AppConfig config, DateTimeOffset now)
    {
        if (active is not null) throw new InvalidOperationException("A session is already running.");
        if (journal.Exists)
            throw new InvalidOperationException("An earlier session hasn't been undone yet. Restore it first.");

        var entry = new JournalEntry(now, now + duration, null, null);
        if (config.KeepAwakeWithLidClosed)
        {
            Guid scheme;
            LidActions original;
            try
            {
                scheme = powerPlan.ActiveScheme();
                original = powerPlan.ReadLid(scheme);
            }
            catch (Exception e)
            {
                throw new SessionStartException("Couldn't read the power plan's lid setting.", e);
            }
            entry = entry with { Scheme = scheme, OriginalLid = original };
            journal.Save(entry);
            try
            {
                powerPlan.WriteLid(scheme, new LidActions(LidAction.DoNothing, LidAction.DoNothing));
                if (powerPlan.ReadLid(scheme) != new LidActions(LidAction.DoNothing, LidAction.DoNothing))
                    throw new IOException("The lid setting did not take effect.");
                log.Write($"lid action set to do nothing on {scheme} (was AC={original.Ac} DC={original.Dc})");
            }
            catch (Exception e)
            {
                log.Write($"setting the lid action failed: {e.Message}");
                // A partial write (AC done, DC failed) must not outlive the failed start.
                Finish(entry);
                throw new SessionStartException("Couldn't change the lid setting.", e);
            }
        }
        else
        {
            journal.Save(entry);
        }

        try
        {
            keepAwake.Hold(config.KeepDisplayOn);
        }
        catch (Exception e)
        {
            // A failed awake request must undo the already-written lid setting.
            keepAwake.Release();
            var undo = Finish(entry);
            throw new SessionStartException(undo.Ok
                ? "Couldn't keep the computer awake. The lid setting was restored."
                : "Couldn't keep the computer awake and the lid setting needs recovery.", e);
        }
        active = entry;
        log.Write($"session started, ends {entry.EndsAt:yyyy-MM-dd HH:mm:ss zzz}");
    }

    public void Extend(TimeSpan extra)
    {
        if (active is null) throw new InvalidOperationException("No session is running.");
        var updated = active with { EndsAt = active.EndsAt + extra };
        if (updated.EndsAt - updated.StartedAt > DurationInput.Maximum + TimeSpan.FromDays(1))
            throw new InvalidOperationException("That would make the session too long.");
        journal.Save(updated);
        active = updated;
        log.Write($"session extended to {updated.EndsAt:yyyy-MM-dd HH:mm:ss zzz}");
    }

    /// <summary>Re-applies the keep-awake request, e.g. after the display setting changed.</summary>
    public void Rehold(bool keepDisplayOn)
    {
        if (active is not null) keepAwake.Hold(keepDisplayOn);
    }

    /// <summary>Called on a timer. Returns the reason if the session just ended.</summary>
    public EndReason? Tick(DateTimeOffset now, PowerSnapshot power, AppConfig config)
    {
        if (active is null) return null;
        EndReason? reason = null;
        if (now >= active.EndsAt) reason = EndReason.Timer;
        else if (BatteryRule.ShouldEnd(power, config.BatteryEndFloorPercent)) reason = EndReason.Battery;
        if (reason is { } r) End(r);
        return reason;
    }

    public RestoreResult End(EndReason reason)
    {
        if (active is null) return new RestoreResult(RestoreOutcome.NothingToRestore);
        var entry = active;
        active = null;
        keepAwake.Release();
        log.Write($"session ended ({reason})");
        return Finish(entry);
    }

    /// <summary>Undoes a session left behind by a crash, kill or power loss.
    /// Only call this when no other Awavio process owns the session.</summary>
    public RestoreResult Recover()
    {
        if (active is not null) throw new InvalidOperationException("A session is running in this process.");
        JournalEntry? entry;
        try
        {
            entry = journal.Load();
        }
        catch (JournalCorruptException e)
        {
            PendingRecoveryError = e.Message;
            log.Write(e.Message);
            return new RestoreResult(RestoreOutcome.Failed, e.Message);
        }
        if (entry is null)
        {
            PendingRecoveryError = null;
            return new RestoreResult(RestoreOutcome.NothingToRestore);
        }
        log.Write("recovering a session left from an earlier run");
        return Finish(entry);
    }

    /// <summary>Forgets a journal the user has chosen to give up on. The lid setting stays as it is.</summary>
    public void DiscardRecovery()
    {
        if (active is not null) throw new InvalidOperationException("A session is running.");
        journal.Delete();
        PendingRecoveryError = null;
        log.Write("recovery journal discarded by the user");
    }

    RestoreResult Finish(JournalEntry entry)
    {
        var result = RestoreFrom(entry);
        if (result.Ok)
        {
            try
            {
                journal.Delete();
                PendingRecoveryError = null;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                result = new RestoreResult(RestoreOutcome.Failed, $"Settings were restored, but the recovery journal could not be removed: {e.Message}");
                PendingRecoveryError = result.Error;
                log.Write(result.Error!);
            }
        }
        else
        {
            PendingRecoveryError = result.Error;
        }
        return result;
    }

    RestoreResult RestoreFrom(JournalEntry entry)
    {
        if (entry.Scheme is not { } scheme || entry.OriginalLid is not { } original)
            return new RestoreResult(RestoreOutcome.NothingToRestore);
        try
        {
            // Only put back a value that still holds what we wrote: if the user
            // picked something else mid-session, their choice wins.
            var current = powerPlan.ReadLid(scheme);
            var target = new LidActions(
                current.Ac == LidAction.DoNothing ? original.Ac : current.Ac,
                current.Dc == LidAction.DoNothing ? original.Dc : current.Dc);
            var userChanged = target.Ac != original.Ac || target.Dc != original.Dc;
            if (target != current) powerPlan.WriteLid(scheme, target);
            var readBack = powerPlan.ReadLid(scheme);
            if (readBack != target)
                return Fail($"The lid setting reads AC={readBack.Ac} DC={readBack.Dc} after restore, expected AC={target.Ac} DC={target.Dc}.");
            log.Write($"lid action restored on {scheme} to AC={target.Ac} DC={target.Dc}");
            return new RestoreResult(userChanged ? RestoreOutcome.LeftUserChange : RestoreOutcome.Restored);
        }
        catch (Exception e)
        {
            return Fail($"Couldn't restore the lid setting: {e.Message}");
        }

        RestoreResult Fail(string message)
        {
            log.Write(message);
            return new RestoreResult(RestoreOutcome.Failed, message);
        }
    }
}

public static class BatteryRule
{
    /// <summary>End on battery below the floor. A floor of 0 or an unknown charge never ends.</summary>
    public static bool ShouldEnd(PowerSnapshot power, int floorPercent) =>
        floorPercent > 0 && power.OnBattery && power.Percent is { } p && p < floorPercent;
}
