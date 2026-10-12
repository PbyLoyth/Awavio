using System.Drawing.Drawing2D;
using Awavio.Core;

namespace Awavio.Windows;

/// <summary>Timed-session entry with presets and explicit days/hours/minutes fields.</summary>
sealed class DurationForm : Form
{
    readonly NumericUpDown days = Field(30, "Days");
    readonly NumericUpDown hours = Field(23, "Hours");
    readonly NumericUpDown minutes = Field(59, "Minutes");
    readonly Label error = new() { ForeColor = Theme.Coral, BackColor = Theme.Ink, Font = Theme.Small };
    readonly OrbitButton[] presets;
    readonly bool adding;
    readonly Icon windowIcon;
    bool initializing = true;
    bool moving;
    Point dragStart;
    Point dragLocation;
    TimeSpan selected;

    public TimeSpan Duration { get; private set; }

    public DurationForm(string title, string action, TimeSpan initial)
    {
        Text = title;
        adding = action.Equals("Add", StringComparison.OrdinalIgnoreCase);
        FormBorderStyle = FormBorderStyle.None;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = true;
        TopMost = true;
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(480, 650);
        BackColor = Theme.Ink;
        ForeColor = Theme.Text;
        Font = Theme.Body;
        DoubleBuffered = true;
        windowIcon = TrayIcons.Draw(open: true, warning: false);
        Icon = windowIcon;
        AccessibleName = adding ? "Awavio add time" : "Awavio start a timed session";

        var close = new OrbitButton { Quiet = true, CloseGlyph = true, Bounds = new Rectangle(423, 24, 30, 30), DialogResult = DialogResult.Cancel, AccessibleName = "Close duration window", TabIndex = 8 };
        Controls.Add(close);
        CancelButton = close;

        var values = new[] { 15, 30, 60, 120 };
        var labels = new[] { "15 min", "30 min", "1 hour", "2 hours" };
        presets = new OrbitButton[values.Length];
        for (var i = 0; i < values.Length; i++)
        {
            var value = values[i];
            var button = new OrbitButton { Text = labels[i], Bounds = new Rectangle(28 + i * 108, 346, 100, 40), TabIndex = i, AccessibleName = $"Select {value} minutes" };
            button.Click += (_, _) => SetDuration(TimeSpan.FromMinutes(value));
            presets[i] = button;
            Controls.Add(button);
        }

        days.Bounds = new Rectangle(28, 438, 128, 44);
        hours.Bounds = new Rectangle(176, 438, 128, 44);
        minutes.Bounds = new Rectangle(324, 438, 128, 44);
        days.TabIndex = 4;
        hours.TabIndex = 5;
        minutes.TabIndex = 6;
        foreach (var field in new[] { days, hours, minutes })
        {
            Controls.Add(field);
            field.ValueChanged += (_, _) => UpdateSelection();
        }
        error.Bounds = new Rectangle(28, 491, 424, 30);
        error.AccessibleRole = AccessibleRole.Alert;
        Controls.Add(error);

        var ok = new OrbitButton { Text = adding ? "Add time" : "Start session", Primary = true, Bounds = new Rectangle(28, 530, 424, 52), TabIndex = 7, AccessibleName = adding ? "Add the selected time to the session" : "Start the selected awake session" };
        ok.Click += (_, _) => Accept();
        AcceptButton = ok;
        Controls.Add(ok);

        SetDuration(initial);
        initializing = false;
        UpdateSelection();
        Shown += (_, _) => { Activate(); hours.Select(); hours.Select(0, hours.Text.Length); };
    }

    void SetDuration(TimeSpan duration)
    {
        var wasInitializing = initializing;
        initializing = true;
        var clamped = duration < TimeSpan.Zero ? TimeSpan.Zero : duration > DurationInput.Maximum ? DurationInput.Maximum : duration;
        days.Value = clamped.Days;
        hours.Value = clamped.Hours;
        minutes.Value = clamped.Minutes;
        initializing = wasInitializing;
        if (!initializing) UpdateSelection();
    }

    void UpdateSelection()
    {
        if (initializing) return;
        selected = TimeSpan.FromDays((int)days.Value) + TimeSpan.FromHours((int)hours.Value) + TimeSpan.FromMinutes((int)minutes.Value);
        var values = new[] { 15, 30, 60, 120 };
        for (var i = 0; i < presets.Length; i++) presets[i].Selected = selected.TotalMinutes == values[i];
        error.Text = "";
        Invalidate();
    }

    void Accept()
    {
        ValidateChildren();
        if (DurationInput.TryParse((int)days.Value, (int)hours.Value, (int)minutes.Value, out var duration, out var message))
        {
            Duration = duration;
            DialogResult = DialogResult.OK;
        }
        else error.Text = message;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        g.ScaleTransform(DeviceDpi / 96f, DeviceDpi / 96f);
        using (var orbit = new Pen(Color.FromArgb(24, Theme.Violet), 1.3f))
        {
            g.DrawEllipse(orbit, 270, -124, 330, 270);
            g.DrawEllipse(orbit, 294, -104, 282, 230);
        }
        TrayIcons.DrawMark(g, new RectangleF(28, 24, 42, 42), true, false);
        Draw(g, "awavio", 81, 24, 23, Theme.Text, true);
        Draw(g, "STAY AWAKE. STAY IN FLOW.", 83, 59, 7.5f, Theme.Muted, true);
        Draw(g, adding ? "A little more flow." : "Stay in your flow.", 28, 100, 23, Theme.Text, true);
        Draw(g, adding ? "Extend your session. Keep your momentum." : "Give your PC a little more awake time.", 30, 143, 10, Theme.Muted);

        var card = new RectangleF(28, 180, 424, 126);
        using (var path = Theme.Rounded(card, 20))
        using (var fill = new LinearGradientBrush(card, Theme.Surface, Color.FromArgb(32, 30, 59), 20))
        using (var stroke = new Pen(Color.FromArgb(78, 72, 112), 1))
        {
            g.FillPath(fill, path);
            g.DrawPath(stroke, path);
        }
        Draw(g, adding ? "EXTRA AWAKE TIME" : "YOUR AWAKE WINDOW", 49, 199, 8, Theme.Aqua, true);
        var duration = DurationLabel(selected);
        Draw(g, duration, 45, 221, duration.Length > 12 ? 24 : 32, Theme.Text, true);
        var end = DateTime.Now.Add(selected);
        var endText = selected == TimeSpan.Zero ? "Choose at least one minute" : adding ? "Added to your current session" : selected.TotalDays >= 1 ? $"Until {end:ddd, MMM d · HH:mm}" : $"Until {end:HH:mm} · then back to normal";
        Draw(g, endText, 49, 277, 9, Theme.Muted);
        TrayIcons.DrawMark(g, new RectangleF(351, 211, 70, 70), true, false);
        Draw(g, "QUICK PICK", 30, 325, 8, Theme.Muted, true);
        Draw(g, "Or make it your own", 30, 402, 10, Theme.Text, true);
        Draw(g, "DAYS", 30, 423, 7.5f, Theme.Muted, true);
        Draw(g, "HOURS", 178, 423, 7.5f, Theme.Muted, true);
        Draw(g, "MINUTES", 326, 423, 7.5f, Theme.Muted, true);
        using (var pen = new Pen(Theme.Aqua, 1.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            g.DrawLines(pen, [new PointF(31, 610), new PointF(35, 614), new PointF(42, 605)]);
        Draw(g, "A little more time. No permanent changes.", 51, 600, 9, Theme.Text);
        Draw(g, "Your PC can sleep again when the timer ends.", 51, 618, 8, Theme.Muted);
        using var border = Theme.Rounded(new RectangleF(.5f, .5f, 479, 649), 24);
        using var edge = new Pen(Theme.Border, 1);
        g.DrawPath(edge, border);
    }

    static string DurationLabel(TimeSpan duration)
    {
        if (duration.Days > 0) return duration.Hours == 0 && duration.Minutes == 0 ? $"{duration.Days} days" : $"{duration.Days}d {duration.Hours}h {duration.Minutes}m";
        if (duration.Hours > 0) return duration.Minutes > 0 ? $"{duration.Hours} hr {duration.Minutes} min" : $"{duration.Hours} hour" + (duration.Hours == 1 ? "" : "s");
        return $"{duration.Minutes} min";
    }

    static void Draw(Graphics g, string text, float x, float y, float size, Color color, bool strong = false)
    {
        using var font = new Font(strong ? "Segoe UI Semibold" : "Segoe UI", size);
        using var fill = new SolidBrush(color);
        g.DrawString(text, font, fill, x, y);
    }

    static NumericUpDown Field(int max, string name) => new()
    {
        Minimum = 0, Maximum = max, TextAlign = HorizontalAlignment.Center,
        BackColor = Theme.Surface, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle,
        Font = new Font("Segoe UI Semibold", 18), AccessibleName = name,
    };

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (ClientSize.Width <= 0 || ClientSize.Height <= 0) return;
        using var path = Theme.Rounded(new RectangleF(0, 0, ClientSize.Width, ClientSize.Height), 24 * DeviceDpi / 96f);
        var previous = Region;
        Region = new Region(path);
        previous?.Dispose();
    }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left || e.Y > LogicalToDeviceUnits(90)) return;
        moving = true;
        dragStart = Cursor.Position;
        dragLocation = Location;
        Capture = true;
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (moving) Location = new Point(dragLocation.X + Cursor.Position.X - dragStart.X, dragLocation.Y + Cursor.Position.Y - dragStart.Y);
    }
    protected override void OnMouseUp(MouseEventArgs e) { moving = false; Capture = false; base.OnMouseUp(e); }
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            days.Font.Dispose();
            hours.Font.Dispose();
            minutes.Font.Dispose();
            windowIcon.Dispose();
        }
        base.Dispose(disposing);
    }
}
