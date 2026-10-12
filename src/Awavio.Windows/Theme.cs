using System.Drawing.Drawing2D;

namespace Awavio.Windows;

/// <summary>The shared visual vocabulary used by Awavio's native controls.</summary>
static class Theme
{
    public static readonly Color Ink = Color.FromArgb(16, 19, 34);
    public static readonly Color Surface = Color.FromArgb(26, 30, 50);
    public static readonly Color Hover = Color.FromArgb(38, 42, 67);
    public static readonly Color Border = Color.FromArgb(58, 61, 87);
    public static readonly Color Violet = Color.FromArgb(139, 108, 255);
    public static readonly Color Aqua = Color.FromArgb(87, 228, 206);
    public static readonly Color Coral = Color.FromArgb(255, 140, 122);
    public static readonly Color Text = Color.FromArgb(244, 243, 255);
    public static readonly Color Muted = Color.FromArgb(167, 172, 196);
    // A bounded set shared by controls for the life of the application.
    public static readonly Font Body = new("Segoe UI", 10f);
    public static readonly Font Button = new("Segoe UI Semibold", 11f);
    public static readonly Font Small = new("Segoe UI", 9f);

    public static GraphicsPath Rounded(RectangleF r, float radius)
    {
        var d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        var path = new GraphicsPath();
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    public static void FillRounded(Graphics g, RectangleF bounds, float radius, Color color)
    {
        using var path = Rounded(bounds, radius);
        using var fill = new SolidBrush(color);
        g.FillPath(fill, path);
    }

    public static void ApplyMenu(ContextMenuStrip menu)
    {
        menu.Renderer = new AwavioMenuRenderer();
        menu.BackColor = Ink;
        menu.ForeColor = Text;
        menu.Font = Body;
        menu.ShowImageMargin = false;
        menu.ShowCheckMargin = true;
        menu.Padding = new Padding(6, 8, 6, 8);
        menu.AccessibleName = "Awavio controls";
    }

    sealed class AwavioMenuRenderer : ToolStripProfessionalRenderer
    {
        public AwavioMenuRenderer() : base(new MenuColors()) => RoundedEdges = false;
        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            using var fill = new SolidBrush(Ink);
            e.Graphics.FillRectangle(fill, e.AffectedBounds);
        }
        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            using var pen = new Pen(Border);
            e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
        }
        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (e.Item.Selected && e.Item.Enabled)
                FillRounded(e.Graphics, new RectangleF(2, 1, e.Item.Width - 4, e.Item.Height - 2), 6, Hover);
        }
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? e.Item.ForeColor : Muted;
            base.OnRenderItemText(e);
        }
        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            using var pen = new Pen(Border);
            e.Graphics.DrawLine(pen, 10, e.Item.Height / 2, e.Item.Width - 10, e.Item.Height / 2);
        }
        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = e.Item?.Enabled == true ? Muted : Border;
            base.OnRenderArrow(e);
        }
        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            using var pen = new Pen(Aqua, 1.7f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            var r = e.ImageRectangle;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.DrawLines(pen, [new PointF(r.Left + 3, r.Top + 8), new PointF(r.Left + 6, r.Top + 11), new PointF(r.Left + 12, r.Top + 4)]);
        }
        sealed class MenuColors : ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground => Ink;
            public override Color ImageMarginGradientBegin => Ink;
            public override Color ImageMarginGradientMiddle => Ink;
            public override Color ImageMarginGradientEnd => Ink;
            public override Color MenuBorder => Border;
            public override Color MenuItemBorder => Hover;
            public override Color MenuItemSelected => Hover;
        }
    }
}

/// <summary>A regular keyboard-accessible WinForms button with the Awavio finish.</summary>
sealed class OrbitButton : Button
{
    bool hovered;
    bool pressed;
    bool selected;
    public bool Primary { get; init; }
    public bool Quiet { get; init; }
    public bool CloseGlyph { get; init; }
    public bool Selected { get => selected; set { selected = value; Invalidate(); } }

    public OrbitButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
        BackColor = Theme.Ink;
        ForeColor = Theme.Text;
        Font = Theme.Button;
        Cursor = Cursors.Hand;
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var scale = DeviceDpi / 96f;
        var bounds = new RectangleF(.5f, .5f, Width - 1f, Height - 1f);
        var fill = Primary ? (hovered ? Color.FromArgb(161, 138, 255) : Theme.Violet)
            : selected ? Color.FromArgb(45, 37, 75) : hovered ? Theme.Hover : Quiet ? Theme.Ink : Theme.Surface;
        if (pressed) fill = Primary ? Color.FromArgb(125, 94, 238) : Theme.Hover;
        if (!Enabled) fill = Theme.Surface;
        Theme.FillRounded(g, bounds, 12 * scale, fill);
        if (!Primary && !Quiet)
        {
            using var path = Theme.Rounded(bounds, 12 * scale);
            using var border = new Pen(selected ? Theme.Violet : Theme.Border, scale);
            g.DrawPath(border, path);
        }
        var foreground = !Enabled ? Theme.Muted : Primary ? Theme.Ink : selected ? Theme.Text : Theme.Muted;
        if (CloseGlyph)
        {
            using var pen = new Pen(hovered ? Theme.Text : Theme.Muted, 1.7f * scale) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            var cx = Width / 2f;
            var cy = Height / 2f;
            g.DrawLine(pen, cx - 4 * scale, cy - 4 * scale, cx + 4 * scale, cy + 4 * scale);
            g.DrawLine(pen, cx + 4 * scale, cy - 4 * scale, cx - 4 * scale, cy + 4 * scale);
        }
        else
        {
            var textBounds = new Rectangle(0, 0, Width - (Primary ? (int)(24 * scale) : 0), Height);
            TextRenderer.DrawText(g, Text, Font, textBounds, foreground,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            if (Primary)
            {
                using var arrow = new Pen(foreground, 1.8f * scale) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                var x = Width - 26 * scale;
                var y = Height / 2f;
                g.DrawLine(arrow, x - 8 * scale, y, x + 3 * scale, y);
                g.DrawLines(arrow, [new PointF(x - scale, y - 4 * scale), new PointF(x + 3 * scale, y), new PointF(x - scale, y + 4 * scale)]);
            }
        }
        if (Focused && ShowFocusCues)
        {
            using var focus = Theme.Rounded(new RectangleF(3 * scale, 3 * scale, Width - 6 * scale, Height - 6 * scale), 9 * scale);
            using var pen = new Pen(Primary ? Theme.Ink : Theme.Aqua, scale) { DashStyle = DashStyle.Dot };
            g.DrawPath(pen, focus);
        }
    }
    protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hovered = false; pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { pressed = e.Button == MouseButtons.Left; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
}
