namespace Awavio.Core;

public static class DurationInput
{
    public static readonly TimeSpan Maximum = TimeSpan.FromDays(30);

    /// <summary>Validates the days/hours/minutes fields of the duration dialog.</summary>
    public static bool TryParse(int days, int hours, int minutes, out TimeSpan duration, out string? error)
    {
        duration = TimeSpan.Zero;
        if (days < 0 || hours < 0 || minutes < 0)
        {
            error = "Values can't be negative.";
            return false;
        }
        if (hours > 23 || minutes > 59)
        {
            error = "Use at most 23 hours and 59 minutes.";
            return false;
        }
        var total = TimeSpan.FromDays(days) + TimeSpan.FromHours(hours) + TimeSpan.FromMinutes(minutes);
        if (total < TimeSpan.FromMinutes(1))
        {
            error = "Enter at least one minute.";
            return false;
        }
        if (total > Maximum)
        {
            error = $"Sessions can last at most {Maximum.TotalDays:0} days.";
            return false;
        }
        duration = total;
        error = null;
        return true;
    }

    /// <summary>"2d 03:04:05", or "03:04:05" under a day.</summary>
    public static string FormatRemaining(TimeSpan remaining)
    {
        if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
        var clock = $"{remaining.Hours:00}:{remaining.Minutes:00}:{remaining.Seconds:00}";
        return remaining.Days > 0 ? $"{remaining.Days}d {clock}" : clock;
    }
}
