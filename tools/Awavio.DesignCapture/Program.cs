using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Awavio.Windows;

namespace Awavio.DesignCapture;

internal static class Program
{
    static readonly Color Ink = Color.FromArgb(16, 19, 34);
    static readonly Color Mint = Color.FromArgb(87, 228, 206);
    static readonly Color Violet = Color.FromArgb(139, 108, 255);

    [STAThread]
    static int Main(string[] args)
    {
        var root = Path.GetFullPath(args.Length == 0 ? "." : args[0]);
        var images = Path.Combine(root, "docs", "images");
        Directory.CreateDirectory(images);
        Application.SetHighDpiMode(HighDpiMode.DpiUnaware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        using var duration = new DurationForm("Awavio — stay awake for", "Start session", TimeSpan.FromMinutes(90));
        using var durationImage = Capture(duration);
        durationImage.Save(Path.Combine(images, "duration.png"));
        using var menu = new ContextMenuStrip();
        menu.Items.Add("Start session");
        menu.Items.Add("Keep the display on");
        menu.Items.Add("Quit Awavio");
        using var active = new WidgetForm(() => new WidgetView(WidgetMode.Active, "01:42:08", "Awake until 18:30"), () => { }, () => { }, _ => { }, menu, null);
        using var activeImage = Capture(active);
        activeImage.Save(Path.Combine(images, "widget-active.png"));
        using var idle = new WidgetForm(() => new WidgetView(WidgetMode.Idle, "Ready", "Click to start a session"), () => { }, () => { }, _ => { }, menu, null);
        using var idleImage = Capture(idle);
        idleImage.Save(Path.Combine(images, "widget-idle.png"));
        using var warning = new WidgetForm(() => new WidgetView(WidgetMode.NeedsAttention, "Needs attention", "Recovery needs attention"), () => { }, () => { }, _ => { }, menu, null);
        using var warningImage = Capture(warning);
        warningImage.Save(Path.Combine(images, "widget-recovery.png"));
        SaveHero(images, durationImage, activeImage, idleImage);
        SaveIdentity(root, images);
        Console.WriteLine("Rendered real WinForms controls and Awavio identity assets without changing power settings.");
        return 0;
    }

    static Bitmap Capture(Form form)
    {
        _ = form.Handle;
        form.CreateControl();
        form.PerformLayout();
        CreateChildren(form);
        var bitmap = new Bitmap(form.Width, form.Height, PixelFormat.Format32bppArgb);
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
        return bitmap;
    }

    static void CreateChildren(Control control)
    {
        foreach (Control child in control.Controls)
        {
            child.CreateControl();
            _ = child.Handle;
            CreateChildren(child);
        }
    }

    static void SaveHero(string images, Bitmap duration, Bitmap active, Bitmap idle)
    {
        using var canvas = new Bitmap(1600, 1000);
        using var g = Graphics.FromImage(canvas);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        using var backdrop = new LinearGradientBrush(new Rectangle(0, 0, 1600, 1000), Ink, Color.FromArgb(27, 23, 51), 25f);
        g.FillRectangle(backdrop, 0, 0, 1600, 1000);
        using var orbit = new Pen(Color.FromArgb(18, Mint), 1f);
        for (var i = 0; i < 6; i++) g.DrawEllipse(orbit, 790 - i * 85, 190 - i * 85, 760 + i * 170, 760 + i * 170);
        using var dot = new SolidBrush(Color.FromArgb(38, 170, 158, 227));
        for (var x = 40; x < 1600; x += 48)
            for (var y = 32; y < 1000; y += 48) g.FillEllipse(dot, x, y, 2, 2);
        using var violetGlow = new SolidBrush(Color.FromArgb(18, Violet));
        g.FillEllipse(violetGlow, 860, 180, 650, 650);
        TrayIcons.DrawEye(g, new RectangleF(104, 104, 76, 76), true, false, Mint, null);
        Text(g, "Awavio", 198, 102, 46, Color.White, FontStyle.Bold);
        Text(g, "A little glow.", 110, 290, 63, Color.White, FontStyle.Bold);
        Text(g, "A lot more time.", 110, 383, 63, Mint, FontStyle.Bold);
        Text(g, "Give Windows a reason to stay awake.", 114, 510, 21, Color.FromArgb(201, 204, 224));
        Text(g, "Pick your time. Keep your flow. Let it rest after.", 114, 554, 21, Color.FromArgb(154, 161, 187));
        Text(g, "YOUR LITTLE DESKTOP COMPANION", 114, 663, 11, Violet, FontStyle.Bold);
        g.DrawImage(active, new Rectangle(114, 708, active.Width * 2, active.Height * 2));
        Text(g, "Quiet when you're done.", 114, 857, 13, Color.FromArgb(177, 186, 206));
        Text(g, "WINDOWS 10 / 11    ·    FREE & OPEN SOURCE", 114, 935, 11, Color.FromArgb(154, 161, 187), FontStyle.Bold);
        var scale = Math.Min(1.13f, 850f / duration.Height);
        var w = (int)(duration.Width * scale);
        var h = (int)(duration.Height * scale);
        using var shadow = new SolidBrush(Color.FromArgb(66, Color.Black));
        g.FillRectangle(shadow, 981, 117, w, h);
        g.DrawImage(duration, new Rectangle(961, 97, w, h));
        canvas.Save(Path.Combine(images, "hero.png"));
        using var social = new Bitmap(1280, 800);
        using var socialGraphics = Graphics.FromImage(social);
        socialGraphics.DrawImage(canvas, new Rectangle(0, 0, 1280, 800));
        social.Save(Path.Combine(images, "social-card.png"));
    }

    static void SaveIdentity(string root, string images)
    {
        using var mark = new Bitmap(256, 256);
        using (var g = Graphics.FromImage(mark))
        {
            g.Clear(Ink);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var ring = new Pen(Violet, 10);
            g.DrawEllipse(ring, 26, 26, 204, 204);
            TrayIcons.DrawEye(g, new RectangleF(44, 44, 168, 168), true, false, Mint, null);
        }
        mark.Save(Path.Combine(images, "app-icon.png"));
        Directory.CreateDirectory(Path.Combine(root, "resources"));
        var handle = mark.GetHicon();
        try
        {
            using var icon = (Icon)Icon.FromHandle(handle).Clone();
            using var stream = File.Create(Path.Combine(root, "resources", "awavio.ico"));
            icon.Save(stream);
        }
        finally { NativeMethods.DestroyIcon(handle); }
        using var white = new Bitmap(640, 320);
        using (var g = Graphics.FromImage(white))
        {
            g.Clear(Color.White);
            TrayIcons.DrawEye(g, new RectangleF(48, 76, 164, 164), true, false, Color.FromArgb(103, 71, 212), null);
            Text(g, "Awavio", 236, 104, 50, Ink, FontStyle.Bold);
        }
        white.Save(Path.Combine(images, "logo-on-white.png"));
    }

    static void Text(Graphics g, string text, float x, float y, float size, Color color, FontStyle style = FontStyle.Regular)
    {
        using var font = new Font("Segoe UI", size, style, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(color);
        g.DrawString(text, font, brush, x, y, StringFormat.GenericTypographic);
    }
}
