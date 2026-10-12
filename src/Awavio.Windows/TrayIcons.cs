using System.Drawing.Drawing2D;

namespace Awavio.Windows;

/// <summary>A radiant eye in an orbit: closed at rest, open while awake, coral for attention.</summary>
static class TrayIcons
{
    public static Icon Draw(bool open, bool warning)
    {
        var size = SystemInformation.SmallIconSize.Width;
        using var bitmap = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.Transparent);
            DrawEye(g, new RectangleF(0, 0, size, size), open, warning,
                warning ? Theme.Coral : open ? Theme.Aqua : Theme.Violet, Color.FromArgb(130, 0, 0, 0));
        }
        var handle = bitmap.GetHicon();
        try { return (Icon)Icon.FromHandle(handle).Clone(); }
        finally { NativeMethods.DestroyIcon(handle); }
    }

    /// <summary>A larger, circular badge version for the popup and floating widget.</summary>
    public static void DrawMark(Graphics g, RectangleF bounds, bool open, bool warning)
    {
        var accent = warning ? Theme.Coral : open ? Theme.Aqua : Theme.Violet;
        using var circle = new GraphicsPath();
        circle.AddEllipse(bounds);
        using var fill = new LinearGradientBrush(bounds, Color.FromArgb(51, 41, 87), Theme.Surface, 50);
        g.FillPath(fill, circle);
        using var edge = new Pen(Color.FromArgb(150, Theme.Violet), Math.Max(1, bounds.Width / 50));
        g.DrawPath(edge, circle);
        var inset = bounds.Width * .14f;
        DrawEye(g, new RectangleF(bounds.X + inset, bounds.Y + inset, bounds.Width - inset * 2, bounds.Height - inset * 2), open, warning, accent, null);
    }

    /// <summary>Draws the vector mark on a centered 16-unit grid.</summary>
    public static void DrawEye(Graphics g, RectangleF bounds, bool open, bool warning, Color stroke, Color? outline)
    {
        var state = g.Save();
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var size = Math.Min(bounds.Width, bounds.Height);
        g.TranslateTransform(bounds.X + (bounds.Width - size) / 2, bounds.Y + (bounds.Height - size) / 2);
        g.ScaleTransform(size / 16f, size / 16f);
        using var pen = new Pen(stroke, 1.3f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        using var halo = outline is { } o ? new Pen(o, 2.7f) { StartCap = LineCap.Round, EndCap = LineCap.Round } : null;
        using var orbit = new Pen(Color.FromArgb(115, Theme.Violet), .6f);
        g.DrawArc(orbit, .75f, .75f, 14.5f, 14.5f, 135, 260);
        using var path = new GraphicsPath();
        if (open)
        {
            path.AddBezier(1.4f, 8, 4.4f, 2.6f, 11.6f, 2.6f, 14.6f, 8);
            path.AddBezier(14.6f, 8, 11.6f, 13.4f, 4.4f, 13.4f, 1.4f, 8);
            if (halo is not null) g.DrawPath(halo, path);
            g.DrawPath(pen, path);
            using var pupil = new SolidBrush(stroke);
            g.FillEllipse(pupil, 5.5f, 5.5f, 5, 5);
            using var glint = new SolidBrush(Theme.Ink);
            g.FillEllipse(glint, 7, 7, 2, 2);
            using var shine = new SolidBrush(Theme.Text);
            g.FillEllipse(shine, 6.3f, 6.1f, 1.2f, 1.2f);
        }
        else
        {
            path.AddBezier(1.7f, 6.8f, 4.7f, 10.8f, 11.3f, 10.8f, 14.3f, 6.8f);
            if (halo is not null) g.DrawPath(halo, path);
            g.DrawPath(pen, path);
            g.DrawLine(pen, 4, 9, 3.2f, 10.5f);
            g.DrawLine(pen, 8, 9.8f, 8, 11.3f);
            g.DrawLine(pen, 12, 9, 12.8f, 10.5f);
        }
        using (var sparkle = new Pen(stroke, .9f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
        {
            g.DrawLine(sparkle, 12.8f, 1.2f, 12.8f, 3.6f);
            g.DrawLine(sparkle, 11.6f, 2.4f, 14, 2.4f);
        }
        if (warning)
        {
            using var dot = new SolidBrush(Theme.Coral);
            using var border = new Pen(Theme.Ink, .7f);
            g.FillEllipse(dot, 11, 0, 4.5f, 4.5f);
            g.DrawEllipse(border, 11, 0, 4.5f, 4.5f);
        }
        g.Restore(state);
    }
}
