using System.Drawing.Drawing2D;

namespace Awavio.Windows;

public enum WidgetMode { Idle, Active, NeedsAttention }

/// <summary>What the widget shows. Text is the countdown or a short label.</summary>
public readonly record struct WidgetView(WidgetMode Mode, string Text, string Tooltip);

/// <summary>
/// A small always-on-top pill that sits just above the taskbar, next to the clock:
/// the eye and the countdown. Click to start or add time, press and hold to end,
/// right-click for the menu, drag to move. It never takes focus or a taskbar button.
/// </summary>
sealed class WidgetForm : Form
{
    static readonly TimeSpan HoldToEnd = TimeSpan.FromSeconds(1.2);
    static readonly TimeSpan TapLimit = TimeSpan.FromMilliseconds(350);

    readonly Func<WidgetView> view;
    readonly Action onClick;
    readonly Action onHoldEnd;
    readonly Action<Point> onMoved;
    readonly ContextMenuStrip menu;
    readonly System.Windows.Forms.Timer holdTimer = new() { Interval = 30 };
    readonly ToolTip tip = new() { InitialDelay = 400 };

    Point pressScreen;
    Point pressLocation;
    DateTime pressedAt;
    bool pressed;
    bool dragging;
    bool holdFired;
    float holdProgress;
    bool hover;
    string lastTip = "";

    public WidgetForm(Func<WidgetView> view, Action onClick, Action onHoldEnd, Action<Point> onMoved, ContextMenuStrip menu, Point? savedLocation)
    {
        this.view = view;
        this.onClick = onClick;
        this.onHoldEnd = onHoldEnd;
        this.onMoved = onMoved;
        this.menu = menu;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        DoubleBuffered = true;
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Theme.Ink;
        Text = "Awavio";
        AccessibleName = "Awavio floating session control";
        AccessibleRole = AccessibleRole.PushButton;
        Cursor = Cursors.Hand;
        Size = Scaled(new Size(212, 58));
        Location = OnScreen(savedLocation) ?? DefaultLocation();
        UpdateShape();

        holdTimer.Tick += (_, _) => OnHoldTick();
        DpiChanged += (_, _) => { Size = Scaled(new Size(212, 58)); UpdateShape(); };
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            const int WS_EX_TOOLWINDOW = 0x00000080;
            const int WS_EX_TOPMOST = 0x00000008;
            const int WS_EX_NOACTIVATE = 0x08000000;
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TOOLWINDOW | WS_EX_TOPMOST | WS_EX_NOACTIVATE;
            return cp;
        }
    }

    /// <summary>Repaints from the current view; call after any state change and every second.</summary>
    public void UpdateView(bool reassertTopMost)
    {
        var v = view();
        if (v.Tooltip != lastTip)
        {
            tip.SetToolTip(this, v.Tooltip);
            lastTip = v.Tooltip;
            AccessibleDescription = v.Tooltip;
            AccessibleName = "Awavio: " + v.Text;
        }
        // Clicking the taskbar can raise it over us; put the widget back on top.
        if (reassertTopMost && Visible) NativeMethods.BringToTopNoActivate(Handle);
        Invalidate();
    }

    /// <summary>Back to the default spot above the clock.</summary>
    public void ResetPosition()
    {
        Location = DefaultLocation();
        onMoved(Location);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        var scale = DeviceDpi / 96f;
        g.ScaleTransform(scale, scale);
        var w = Width / scale;
        var h = Height / scale;
        var v = view();
        var accent = v.Mode switch
        {
            WidgetMode.Active => Theme.Aqua,
            WidgetMode.NeedsAttention => Theme.Coral,
            _ => Theme.Violet,
        };
        var rect = new RectangleF(.5f, .5f, w - 1, h - 1);
        using (var path = Theme.Rounded(rect, 20))
        using (var fill = new LinearGradientBrush(rect, hover ? Theme.Hover : Theme.Surface, Theme.Ink, 10))
        using (var border = new Pen(Color.FromArgb(hover ? 190 : 100, accent), 1))
        {
            g.FillPath(fill, path);
            g.DrawPath(border, path);
        }
        TrayIcons.DrawMark(g, new RectangleF(10, 9, 40, 40), v.Mode == WidgetMode.Active, v.Mode == WidgetMode.NeedsAttention);
        using (var brand = new Font("Segoe UI Semibold", 7.3f))
        using (var fill = new SolidBrush(accent))
            g.DrawString("AWAVIO", brand, fill, 62, 9);
        var text = v.Mode == WidgetMode.Idle ? "Ready when you are" : v.Text;
        using (var font = new Font("Segoe UI Semibold", v.Mode == WidgetMode.Idle ? 9.5f : 11.3f))
        using (var fill = new SolidBrush(Theme.Text))
        using (var format = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            g.DrawString(text, font, fill, new RectangleF(60, 26, w - 70, 25), format);
        using (var dot = new SolidBrush(accent)) g.FillEllipse(dot, w - 19, 14, 5, 5);
        if (holdProgress > 0)
        {
            using var bar = new SolidBrush(Theme.Coral);
            g.FillRectangle(bar, 25, h - 4, (w - 50) * holdProgress, 2);
        }
    }
    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        pressed = true;
        dragging = false;
        holdFired = false;
        pressedAt = DateTime.UtcNow;
        pressScreen = Cursor.Position;
        pressLocation = Location;
        if (view().Mode == WidgetMode.Active) holdTimer.Start();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!pressed) return;
        var now = Cursor.Position;
        var dx = now.X - pressScreen.X;
        var dy = now.Y - pressScreen.Y;
        if (!dragging && (Math.Abs(dx) > SystemInformation.DragSize.Width || Math.Abs(dy) > SystemInformation.DragSize.Height))
        {
            dragging = true;
            StopHold();
        }
        if (dragging) Location = new Point(pressLocation.X + dx, pressLocation.Y + dy);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button == MouseButtons.Right)
        {
            menu.Show(this, e.Location);
            return;
        }
        if (e.Button != MouseButtons.Left || !pressed) return;
        pressed = false;
        var wasDragging = dragging;
        var held = DateTime.UtcNow - pressedAt;
        StopHold();
        if (wasDragging)
        {
            Location = OnScreen(Location) ?? DefaultLocation();
            onMoved(Location);
            return;
        }
        if (holdFired) return;
        if (held <= TapLimit) onClick();
    }

    void OnHoldTick()
    {
        if (!pressed || dragging) { StopHold(); return; }
        var elapsed = DateTime.UtcNow - pressedAt;
        // Show progress only once it's clearly a hold, so a tap doesn't flash red.
        holdProgress = elapsed <= TapLimit ? 0 : (float)Math.Min(1, (elapsed - TapLimit) / (HoldToEnd - TapLimit));
        Invalidate();
        if (elapsed >= HoldToEnd)
        {
            holdFired = true;
            StopHold();
            onHoldEnd();
        }
    }

    void StopHold()
    {
        holdTimer.Stop();
        holdProgress = 0;
        Invalidate();
    }

    void UpdateShape()
    {
        using var path = Theme.Rounded(new RectangleF(0, 0, Width, Height), LogicalToDeviceUnits(20));
        Region?.Dispose();
        Region = new Region(path);
    }

    Size Scaled(Size logical) => new(LogicalToDeviceUnits(logical.Width), LogicalToDeviceUnits(logical.Height));

    Point DefaultLocation()
    {
        // Just above the taskbar, flush with the clock end of the primary screen.
        var area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 720);
        var gap = LogicalToDeviceUnits(8);
        return new Point(area.Right - Width - gap, area.Bottom - Height - gap);
    }

    /// <summary>The location if the widget would be mostly on some screen there, else null.</summary>
    Point? OnScreen(Point? location)
    {
        if (location is not { } p) return null;
        var bounds = new Rectangle(p, Size);
        foreach (var screen in Screen.AllScreens)
        {
            var overlap = Rectangle.Intersect(screen.Bounds, bounds);
            if (overlap.Width * overlap.Height >= bounds.Width * bounds.Height / 2) return p;
        }
        return null;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            holdTimer.Dispose();
            tip.Dispose();
        }
        base.Dispose(disposing);
    }
}
