using System.Text.Json;

namespace Awavio.Core;

/// <summary>User settings, stored as JSON next to the recovery journal.</summary>
public sealed record AppConfig
{
    /// <summary>On battery, end the session below this charge. 0 turns the rule off.</summary>
    public int BatteryEndFloorPercent { get; init; } = 10;

    /// <summary>Set the active power plan's lid action to "Do nothing" for the session.</summary>
    public bool KeepAwakeWithLidClosed { get; init; } = true;

    /// <summary>Also keep the display from turning off while the lid is open.</summary>
    public bool KeepDisplayOn { get; init; }

    /// <summary>Prefills the duration dialog.</summary>
    public int LastDurationMinutes { get; init; } = 60;

    /// <summary>Show the countdown widget above the taskbar.</summary>
    public bool ShowWidget { get; init; } = true;

    /// <summary>Where the user dragged the widget, in screen pixels. Null means the default spot.</summary>
    public int? WidgetX { get; init; }
    public int? WidgetY { get; init; }

    public AppConfig Normalized() => this with
    {
        BatteryEndFloorPercent = Math.Clamp(BatteryEndFloorPercent, 0, 99),
        LastDurationMinutes = Math.Clamp(LastDurationMinutes, 1, (int)DurationInput.Maximum.TotalMinutes),
    };
}

public sealed class ConfigStore(string path)
{
    static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public string Path { get; } = path;

    /// <summary>Missing or unreadable config falls back to defaults; it never blocks startup.</summary>
    public AppConfig Load()
    {
        try
        {
            if (!File.Exists(Path)) return new AppConfig();
            return (JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(Path)) ?? new AppConfig()).Normalized();
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            return new AppConfig();
        }
    }

    public void Save(AppConfig config) =>
        AtomicFile.WriteAllText(Path, JsonSerializer.Serialize(config.Normalized(), Options));
}
