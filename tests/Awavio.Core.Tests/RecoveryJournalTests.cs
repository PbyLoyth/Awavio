using Awavio.Core;
using Xunit;

namespace Awavio.Core.Tests;

public sealed class RecoveryJournalTests
{
    [Fact]
    public void LockedJournalIsReportedAndPreserved()
    {
        if (!OperatingSystem.IsWindows()) return; // Windows sharing semantics.
        using var home = new TempHome();
        var journal = new RecoveryJournal(home.Paths.Journal);
        var plan = new FakePowerPlan(new LidActions(1, 1));
        var controller = new SessionController(plan, new FakeKeepAwake(), journal, new Log(home.Paths.LogFile));
        controller.Start(TimeSpan.FromMinutes(2), new AppConfig(), DateTimeOffset.Now);
        using (var locked = new FileStream(journal.Path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.Equal(RestoreOutcome.Failed, controller.End(EndReason.User).Outcome);
            Assert.NotNull(controller.PendingRecoveryError);
            Assert.True(journal.Exists);
            Assert.Equal(new LidActions(1, 1), plan.Lids[plan.Scheme]);
        }
        Assert.True(controller.Recover().Ok);
        Assert.False(journal.Exists);
    }

    [Fact]
    public void UnreadableJournalProducesControlledFailure()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var home = new TempHome();
        var journal = new RecoveryJournal(home.Paths.Journal);
        var plan = new FakePowerPlan(new LidActions(1, 1));
        var controller = new SessionController(plan, new FakeKeepAwake(), journal, new Log(home.Paths.LogFile));
        controller.Start(TimeSpan.FromMinutes(2), new AppConfig(), DateTimeOffset.Now);
        using (var locked = new FileStream(journal.Path, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var next = new SessionController(plan, new FakeKeepAwake(), journal, new Log(home.Paths.LogFile));
            Assert.Equal(RestoreOutcome.Failed, next.Recover().Outcome);
            Assert.NotNull(next.PendingRecoveryError);
            Assert.True(journal.Exists);
        }
        controller.End(EndReason.User);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("{\"StartedAt\":\"2026-10-12T09:00:00Z\",\"EndsAt\":\"2026-10-12T10:00:00Z\",\"Scheme\":\"381b4222-f694-41f0-9685-ff5bb260df2e\",\"OriginalLid\":null}")]
    [InlineData("{\"StartedAt\":\"2026-10-12T09:00:00Z\",\"EndsAt\":\"2026-10-12T08:00:00Z\",\"Scheme\":null,\"OriginalLid\":null}")]
    [InlineData("{\"StartedAt\":\"2026-10-12T09:00:00Z\",\"EndsAt\":\"2026-10-12T10:00:00Z\",\"Scheme\":\"381b4222-f694-41f0-9685-ff5bb260df2e\",\"OriginalLid\":{\"Ac\":9,\"Dc\":1}}")]
    [InlineData("{\"StartedAt\":\"2026-10-12T09:00:00Z\",\"EndsAt\":\"2026-10-12T10:00:00Z\",\"Scheme\":\"381b4222-f694-41f0-9685-ff5bb260df2e\",\"OriginalLid\":{\"Ac\":1}}")]
    public void IncompleteOrInvalidRecoveryStateIsPreserved(string contents)
    {
        using var home = new TempHome();
        Directory.CreateDirectory(home.Paths.Home);
        File.WriteAllText(home.Paths.Journal, contents);
        var journal = new RecoveryJournal(home.Paths.Journal);
        var plan = new FakePowerPlan(new LidActions(0, 0));
        var controller = new SessionController(plan, new FakeKeepAwake(), journal, new Log(home.Paths.LogFile));
        Assert.Equal(RestoreOutcome.Failed, controller.Recover().Outcome);
        Assert.True(journal.Exists);
        Assert.Equal(0, plan.Writes);
    }
}
