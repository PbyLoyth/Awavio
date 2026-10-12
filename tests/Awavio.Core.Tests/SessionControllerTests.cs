using Awavio.Core;
using Xunit;

namespace Awavio.Core.Tests;

public sealed class SessionControllerTests : IDisposable
{
    static readonly DateTimeOffset T0 = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);
    static readonly LidActions SleepBoth = new(LidAction.Sleep, LidAction.Sleep);
    static readonly LidActions Nothing = new(LidAction.DoNothing, LidAction.DoNothing);

    readonly TempHome home = new();
    readonly FakePowerPlan plan = new(SleepBoth);
    readonly FakeKeepAwake awake = new();
    readonly RecoveryJournal journal;
    readonly AppConfig config = new();

    public SessionControllerTests() => journal = new RecoveryJournal(home.Paths.Journal);
    public void Dispose() => home.Dispose();

    SessionController NewController() => new(plan, awake, journal, new Log(home.Paths.LogFile));

    [Fact]
    public void FailedAwakeRequestRestoresLidAndClearsJournal()
    {
        awake.FailHoldWith = new IOException("awake denied");
        var c = NewController();
        Assert.Throws<SessionStartException>(() => c.Start(TimeSpan.FromMinutes(2), config, T0));
        Assert.Equal(SleepBoth, plan.Lids[plan.Scheme]);
        Assert.False(c.IsActive);
        Assert.False(awake.Held);
        Assert.False(journal.Exists);
    }

    [Fact]
    public void FailedAwakeRequestKeepsJournalWhenRollbackFails()
    {
        var c = NewController();
        // The first write succeeds, but the rollback fails after Hold throws.
        awake.FailHoldWith = new IOException("awake denied");
        var failingAwake = new CallbackKeepAwake(() =>
        {
            plan.FailWritesWith = new IOException("rollback denied");
            throw new IOException("awake denied");
        });
        c = new SessionController(plan, failingAwake, journal, new Log(home.Paths.LogFile));
        Assert.Throws<SessionStartException>(() => c.Start(TimeSpan.FromMinutes(2), config, T0));
        Assert.True(journal.Exists);
        Assert.NotNull(c.PendingRecoveryError);
        Assert.False(c.IsActive);
        plan.FailWritesWith = null;
        Assert.True(c.Recover().Ok);
        Assert.Equal(SleepBoth, plan.Lids[plan.Scheme]);
    }

    sealed class CallbackKeepAwake(Action hold) : IKeepAwake
    {
        public void Hold(bool keepDisplayOn) => hold();
        public void Release() { }
    }

    [Fact]
    public void StartRejectsPowerPlanWriteThatDidNotTakeEffect()
    {
        plan.IgnoreWrites = true;
        var c = NewController();
        Assert.Throws<SessionStartException>(() => c.Start(TimeSpan.FromMinutes(2), config, T0));
        Assert.False(c.IsActive);
        Assert.False(awake.Held);
        Assert.False(journal.Exists);
        Assert.Equal(SleepBoth, plan.Lids[plan.Scheme]);
    }

    [Fact]
    public void StartJournalsThenSetsLidToDoNothingAndHoldsAwake()
    {
        var c = NewController();
        c.Start(TimeSpan.FromHours(2), config, T0);

        Assert.True(c.IsActive);
        Assert.Equal(T0.AddHours(2), c.EndsAt);
        Assert.Equal(Nothing, plan.Lids[plan.Scheme]);
        Assert.True(awake.Held);
        var entry = journal.Load()!;
        Assert.Equal(SleepBoth, entry.OriginalLid);
        Assert.Equal(plan.Scheme, entry.Scheme);
    }

    [Fact]
    public void EndRestoresLidReleasesAndDeletesJournal()
    {
        var c = NewController();
        c.Start(TimeSpan.FromHours(1), config, T0);
        var result = c.End(EndReason.User);

        Assert.Equal(RestoreOutcome.Restored, result.Outcome);
        Assert.Equal(SleepBoth, plan.Lids[plan.Scheme]);
        Assert.False(awake.Held);
        Assert.False(journal.Exists);
        Assert.False(c.IsActive);
    }

    [Fact]
    public void LidOptionOffLeavesPowerPlanAlone()
    {
        var c = NewController();
        c.Start(TimeSpan.FromHours(1), config with { KeepAwakeWithLidClosed = false }, T0);
        Assert.Equal(0, plan.Writes);
        Assert.True(awake.Held);
        Assert.Equal(RestoreOutcome.NothingToRestore, c.End(EndReason.User).Outcome);
        Assert.Equal(0, plan.Writes);
        Assert.False(journal.Exists);
    }

    [Fact]
    public void KeepDisplayOnIsPassedThrough()
    {
        var c = NewController();
        c.Start(TimeSpan.FromHours(1), config with { KeepDisplayOn = true }, T0);
        Assert.True(awake.Display);
        c.Rehold(false);
        Assert.False(awake.Display);
    }

    [Fact]
    public void UserChangeDuringSessionIsKept()
    {
        var c = NewController();
        c.Start(TimeSpan.FromHours(1), config, T0);
        plan.Lids[plan.Scheme] = new LidActions(LidAction.Hibernate, LidAction.DoNothing);

        var result = c.End(EndReason.User);

        Assert.Equal(RestoreOutcome.LeftUserChange, result.Outcome);
        Assert.Equal(new LidActions(LidAction.Hibernate, LidAction.Sleep), plan.Lids[plan.Scheme]);
    }

    [Fact]
    public void FailedRestoreKeepsJournalAndRecoverRetries()
    {
        var c = NewController();
        c.Start(TimeSpan.FromHours(1), config, T0);
        plan.FailWritesWith = new UnauthorizedAccessException("denied");

        var result = c.End(EndReason.User);
        Assert.Equal(RestoreOutcome.Failed, result.Outcome);
        Assert.True(journal.Exists);
        Assert.NotNull(c.PendingRecoveryError);
        Assert.False(awake.Held);

        plan.FailWritesWith = null;
        Assert.Equal(RestoreOutcome.Restored, c.Recover().Outcome);
        Assert.False(journal.Exists);
        Assert.Null(c.PendingRecoveryError);
        Assert.Equal(SleepBoth, plan.Lids[plan.Scheme]);
    }

    [Fact]
    public void CannotStartWhileAnEarlierJournalIsPending()
    {
        var c = NewController();
        c.Start(TimeSpan.FromHours(1), config, T0);
        plan.FailWritesWith = new IOException("x");
        c.End(EndReason.User);

        Assert.Throws<InvalidOperationException>(() => c.Start(TimeSpan.FromHours(1), config, T0));
    }

    [Fact]
    public void PartialWriteOnStartIsUndone()
    {
        var c = NewController();
        plan.PartialWrite = true;

        Assert.Throws<SessionStartException>(() => c.Start(TimeSpan.FromHours(1), config, T0));
        Assert.False(c.IsActive);
        Assert.False(awake.Held);
        Assert.Equal(SleepBoth, plan.Lids[plan.Scheme]);
        Assert.False(journal.Exists);
    }

    [Fact]
    public void NewProcessRecoversAfterCrash()
    {
        NewController().Start(TimeSpan.FromHours(1), config, T0);
        // The first controller is gone without End(), as after a crash.
        var next = NewController();
        Assert.Equal(RestoreOutcome.Restored, next.Recover().Outcome);
        Assert.Equal(SleepBoth, plan.Lids[plan.Scheme]);
        Assert.False(journal.Exists);
    }

    [Fact]
    public void RecoverWithoutJournalDoesNothing()
    {
        Assert.Equal(RestoreOutcome.NothingToRestore, NewController().Recover().Outcome);
        Assert.Equal(0, plan.Writes);
    }

    [Fact]
    public void CorruptJournalIsReportedNotGuessed()
    {
        Directory.CreateDirectory(home.Paths.Home);
        File.WriteAllText(home.Paths.Journal, "{ not json");
        var c = NewController();

        var result = c.Recover();
        Assert.Equal(RestoreOutcome.Failed, result.Outcome);
        Assert.Equal(0, plan.Writes);
        Assert.True(journal.Exists);

        c.DiscardRecovery();
        Assert.False(journal.Exists);
        Assert.Null(c.PendingRecoveryError);
    }

    [Fact]
    public void TickEndsAtEndTime()
    {
        var c = NewController();
        c.Start(TimeSpan.FromMinutes(30), config, T0);
        Assert.Null(c.Tick(T0.AddMinutes(29), PowerSnapshot.PluggedIn, config));
        Assert.Equal(TimeSpan.FromMinutes(1), c.Remaining(T0.AddMinutes(29)));
        Assert.Equal(EndReason.Timer, c.Tick(T0.AddMinutes(30), PowerSnapshot.PluggedIn, config));
        Assert.False(c.IsActive);
        Assert.Equal(SleepBoth, plan.Lids[plan.Scheme]);
    }

    [Fact]
    public void TickEndsOnLowBattery()
    {
        var c = NewController();
        c.Start(TimeSpan.FromHours(5), config, T0);
        Assert.Null(c.Tick(T0.AddMinutes(1), new PowerSnapshot(true, 10), config));
        Assert.Equal(EndReason.Battery, c.Tick(T0.AddMinutes(2), new PowerSnapshot(true, 9), config));
    }

    [Fact]
    public void ExtendMovesEndTimeAndJournal()
    {
        var c = NewController();
        c.Start(TimeSpan.FromHours(1), config, T0);
        c.Extend(TimeSpan.FromMinutes(30));
        Assert.Equal(T0.AddMinutes(90), c.EndsAt);
        Assert.Equal(T0.AddMinutes(90), journal.Load()!.EndsAt);
    }

    [Fact]
    public void ExtendRefusesRunawayLength()
    {
        var c = NewController();
        c.Start(DurationInput.Maximum, config, T0);
        Assert.Throws<InvalidOperationException>(() => c.Extend(TimeSpan.FromDays(2)));
        Assert.Equal(T0 + DurationInput.Maximum, journal.Load()!.EndsAt);
    }
}
