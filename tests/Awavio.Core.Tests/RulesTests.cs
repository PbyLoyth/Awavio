using Awavio.Core;
using Xunit;

namespace Awavio.Core.Tests;

public sealed class RulesTests
{
    [Theory]
    [InlineData(true, 9, 10, true)]
    [InlineData(true, 10, 10, false)]
    [InlineData(false, 1, 10, false)]
    [InlineData(true, 1, 0, false)]
    [InlineData(true, null, 10, false)]
    public void BatteryFloor(bool onBattery, int? percent, int floor, bool expected) =>
        Assert.Equal(expected, BatteryRule.ShouldEnd(new PowerSnapshot(onBattery, percent), floor));

    [Theory]
    [InlineData(0, 0, 0, false)]
    [InlineData(0, 0, 1, true)]
    [InlineData(0, 24, 0, false)]
    [InlineData(0, 0, 60, false)]
    [InlineData(-1, 0, 5, false)]
    [InlineData(30, 0, 0, true)]
    [InlineData(30, 0, 1, false)]
    public void DurationValidation(int d, int h, int m, bool ok)
    {
        Assert.Equal(ok, DurationInput.TryParse(d, h, m, out var duration, out var error));
        Assert.Equal(ok, error is null);
        if (ok) Assert.Equal(new TimeSpan(d, h, m, 0), duration);
    }

    [Fact]
    public void FormatsRemaining()
    {
        Assert.Equal("00:05:09", DurationInput.FormatRemaining(new TimeSpan(0, 0, 5, 9)));
        Assert.Equal("2d 03:04:05", DurationInput.FormatRemaining(new TimeSpan(2, 3, 4, 5)));
        Assert.Equal("00:00:00", DurationInput.FormatRemaining(TimeSpan.FromSeconds(-3)));
    }

    [Fact]
    public void ConfigRoundTripsAndClamps()
    {
        using var home = new TempHome();
        var store = new ConfigStore(home.Paths.Config);
        Assert.Equal(new AppConfig(), store.Load());

        store.Save(new AppConfig { BatteryEndFloorPercent = 250, KeepDisplayOn = true });
        var loaded = store.Load();
        Assert.Equal(99, loaded.BatteryEndFloorPercent);
        Assert.True(loaded.KeepDisplayOn);
    }

    [Fact]
    public void BrokenConfigFallsBackToDefaults()
    {
        using var home = new TempHome();
        Directory.CreateDirectory(home.Paths.Home);
        File.WriteAllText(home.Paths.Config, "[");
        Assert.Equal(new AppConfig(), new ConfigStore(home.Paths.Config).Load());
    }
}
