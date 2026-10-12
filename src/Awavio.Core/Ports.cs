namespace Awavio.Core;

/// <summary>Lid-close action indexes as Windows stores them (LIDACTION).</summary>
public static class LidAction
{
    public const uint DoNothing = 0;
    public const uint Sleep = 1;
    public const uint Hibernate = 2;
    public const uint ShutDown = 3;
}

/// <summary>The lid action for plugged-in (AC) and battery (DC) power.</summary>
public readonly record struct LidActions(uint Ac, uint Dc);

/// <summary>Reads and writes the lid action of a power plan.</summary>
public interface IPowerPlan
{
    Guid ActiveScheme();
    LidActions ReadLid(Guid scheme);
    /// <summary>Writes both values and re-applies the active plan so they take effect.</summary>
    void WriteLid(Guid scheme, LidActions actions);
}

/// <summary>Keeps the system (and optionally the display) from idling to sleep.</summary>
public interface IKeepAwake
{
    void Hold(bool keepDisplayOn);
    void Release();
}

/// <summary>A battery reading. Percent is null when Windows doesn't know it.</summary>
public readonly record struct PowerSnapshot(bool OnBattery, int? Percent)
{
    public static readonly PowerSnapshot PluggedIn = new(false, null);
}
