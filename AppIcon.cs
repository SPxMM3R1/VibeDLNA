using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace FolderDlnaServer;

internal static class AppIcon
{
    public static Icon CreateIcon(int size, bool active)
    {
        using var bitmap = new Bitmap(size, size);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Color.Transparent);
            DrawLogo(graphics, new Rectangle(0, 0, size, size), active);
        }

        var handle = bitmap.GetHicon();
        try
        {
            using var icon = Icon.FromHandle(handle);
            return (Icon)icon.Clone();
        }
        finally
        {
            _ = DestroyIcon(handle);
        }
    }

    public static void DrawLogo(Graphics graphics, Rectangle bounds, bool active)
    {
        var size = Math.Min(bounds.Width, bounds.Height);
        var x = bounds.X + (bounds.Width - size) / 2;
        var y = bounds.Y + (bounds.Height - size) / 2;
        var pad = Math.Max(2, size / 10);
        var rect = new Rectangle(x + pad, y + pad, size - pad * 2, size - pad * 2);

        using var backgroundBrush = new LinearGradientBrush(
            rect,
            Color.FromArgb(24, 31, 45),
            Color.FromArgb(9, 13, 22),
            45f);
        using var borderPen = new Pen(Color.FromArgb(82, 96, 124), Math.Max(1f, size / 42f));
        using var glowPen = new Pen(active ? Color.FromArgb(88, 255, 171) : Color.FromArgb(231, 57, 156), Math.Max(2f, size / 13f))
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        using var accentPen = new Pen(Color.FromArgb(231, 57, 156), Math.Max(2f, size / 13f))
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        using var folderPen = new Pen(active ? Color.FromArgb(88, 255, 171) : Color.FromArgb(191, 204, 220), Math.Max(1.5f, size / 24f))
        {
            LineJoin = LineJoin.Round,
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        using var dotBrush = new SolidBrush(active ? Color.FromArgb(88, 255, 171) : Color.FromArgb(231, 57, 156));
        using var dimDotBrush = new SolidBrush(Color.FromArgb(80, 92, 116));

        var radius = Math.Max(6, size / 5);
        using var iconPath = RoundedRect(rect, radius);
        graphics.FillPath(backgroundBrush, iconPath);
        graphics.DrawPath(borderPen, iconPath);

        var folderTopY = y + size * 0.38f;
        var folderBottomY = y + size * 0.65f;
        var folderLeftX = x + size * 0.24f;
        var folderRightX = x + size * 0.76f;
        var tabRightX = x + size * 0.46f;
        var tabTopY = y + size * 0.30f;

        using var folderPath = new GraphicsPath();
        folderPath.AddLine(folderLeftX, folderTopY, x + size * 0.35f, folderTopY);
        folderPath.AddLine(x + size * 0.38f, tabTopY, tabRightX, tabTopY);
        folderPath.AddLine(x + size * 0.50f, folderTopY, folderRightX, folderTopY);
        folderPath.AddLine(folderRightX, folderBottomY, folderLeftX, folderBottomY);
        folderPath.CloseFigure();
        graphics.DrawPath(folderPen, folderPath);

        graphics.DrawLine(accentPen, x + size * 0.29f, y + size * 0.72f, x + size * 0.70f, y + size * 0.30f);
        graphics.DrawArc(glowPen, x + size * 0.51f, y + size * 0.18f, size * 0.30f, size * 0.30f, 315, 72);
        graphics.DrawArc(glowPen, x + size * 0.44f, y + size * 0.10f, size * 0.46f, size * 0.46f, 315, 72);

        var dotSize = Math.Max(4, size / 8);
        graphics.FillEllipse(dimDotBrush, x + size * 0.28f, y + size * 0.73f, dotSize, dotSize);
        graphics.FillEllipse(dotBrush, x + size * 0.64f, y + size * 0.24f, dotSize, dotSize);
    }

    private static GraphicsPath RoundedRect(Rectangle rectangle, int radius)
    {
        var path = new GraphicsPath();
        var diameter = radius * 2;
        var arc = new Rectangle(rectangle.Location, new Size(diameter, diameter));
        path.AddArc(arc, 180, 90);
        arc.X = rectangle.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = rectangle.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = rectangle.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr handle);
}
