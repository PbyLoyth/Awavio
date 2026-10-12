using Awavio.Core;

namespace Awavio.Core.Tests;

sealed class FakePowerPlan : IPowerPlan
{
    public Guid Scheme = Guid.Parse("381b4222-f694-41f0-9685-ff5bb260df2e");
    public Dictionary<Guid, LidActions> Lids = new();
    public int Writes;
    public Exception? FailWritesWith;
    /// <summary>Simulates a write that only lands the AC half before failing.</summary>
    public bool PartialWrite;
    public bool IgnoreWrites;

    public FakePowerPlan(LidActions initial) => Lids[Scheme] = initial;

    public Guid ActiveScheme() => Scheme;
    public LidActions ReadLid(Guid scheme) => Lids[scheme];

    public void WriteLid(Guid scheme, LidActions actions)
    {
        Writes++;
        if (IgnoreWrites) return;
        if (PartialWrite)
        {
            PartialWrite = false;
            Lids[scheme] = Lids[scheme] with { Ac = actions.Ac };
            throw new IOException("DC write failed");
        }
        if (FailWritesWith is not null) throw FailWritesWith;
        Lids[scheme] = actions;
    }
}

sealed class FakeKeepAwake : IKeepAwake
{
    public bool Held;
    public bool Display;
    public Exception? FailHoldWith;
    public void Hold(bool keepDisplayOn)
    {
        if (FailHoldWith is not null) throw FailHoldWith;
        Held = true;
        Display = keepDisplayOn;
    }
    public void Release() { Held = false; Display = false; }
}

sealed class TempHome : IDisposable
{
    public readonly AppPaths Paths = new(Path.Combine(Path.GetTempPath(), "awavio-tests-" + Guid.NewGuid().ToString("N")));
    public void Dispose()
    {
        if (Directory.Exists(Paths.Home)) Directory.Delete(Paths.Home, recursive: true);
    }
}
