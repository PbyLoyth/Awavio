using System.Reflection;
using Awavio.Windows;

namespace Awavio.Windows.Smoke;

/// <summary>Exercises real native duration controls and their wired handlers, without power changes.</summary>
internal static class UiChecks
{
    public static void Run()
    {
        using (var form = Create("Start", TimeSpan.FromHours(1)))
        {
            var fields = Fields(form);
            foreach (var (label, duration) in new[] { ("15 min", 15), ("30 min", 30), ("1 hour", 60), ("2 hours", 120) })
            {
                Click(form.Controls.OfType<Button>().Single(button => button.Text == label));
                Require(fields.Days.Value == 0 && fields.Hours.Value == duration / 60 && fields.Minutes.Value == duration % 60,
                    $"preset {label} updates all three native fields");
                Require(form.DialogResult == DialogResult.None, "a preset selects time without starting a session");
            }
            fields.Days.Value = fields.Hours.Value = fields.Minutes.Value = 0;
            Click((Button)form.AcceptButton!);
            Require(form.DialogResult == DialogResult.None && form.Duration == TimeSpan.Zero, "zero duration is rejected");
            Require(form.Controls.OfType<Label>().Any(label => label.AccessibleRole == AccessibleRole.Alert && !string.IsNullOrWhiteSpace(label.Text)),
                "invalid duration gives visible feedback");
            fields.Days.Value = 1;
            fields.Hours.Value = 2;
            fields.Minutes.Value = 3;
            Click((Button)form.AcceptButton!);
            Require(form.DialogResult == DialogResult.OK && form.Duration == new TimeSpan(1, 2, 3, 0),
                "Start returns the complete positive duration");
        }
        using (var form = Create("Add", TimeSpan.FromMinutes(45)))
        {
            Require(((Button)form.AcceptButton!).Text == "Add time", "Add form exposes the add action");
            var fields = Fields(form);
            Require(fields.Days.Value == 0 && fields.Hours.Value == 0 && fields.Minutes.Value == 45, "Add initial duration is loaded");
            Click(form.Controls.OfType<Button>().Single(button => button.Text == "2 hours"));
            Click((Button)form.AcceptButton!);
            Require(form.DialogResult == DialogResult.OK && form.Duration == TimeSpan.FromHours(2), "Add returns selected extra time");
        }
        Console.WriteLine("PASS: native duration presets, zero rejection, Start acceptance and Add acceptance");
    }

    static DurationForm Create(string action, TimeSpan initial)
    {
        var form = new DurationForm("Awavio UI verification", action, initial);
        _ = form.Handle;
        foreach (Control control in form.Controls) _ = control.Handle;
        form.PerformLayout();
        return form;
    }

    static (NumericUpDown Days, NumericUpDown Hours, NumericUpDown Minutes) Fields(DurationForm form)
    {
        var fields = form.Controls.OfType<NumericUpDown>().ToArray();
        return (fields.Single(field => field.AccessibleName == "Days"), fields.Single(field => field.AccessibleName == "Hours"),
            fields.Single(field => field.AccessibleName == "Minutes"));
    }

    static void Click(Button button)
    {
        // PerformClick() skips hidden forms; invoking the real OnClick runs their actual handlers.
        typeof(Button).GetMethod("OnClick", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(button, [EventArgs.Empty]);
    }

    static void Require(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException("FAILED UI: " + message);
    }
}
