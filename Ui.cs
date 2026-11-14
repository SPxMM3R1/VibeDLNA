using System.Drawing.Drawing2D;

namespace FolderDlnaServer;

/// <summary>
/// Tokens y pintura compartidos por la ventana 1.1.0. Los colores vienen de <see cref="AppPalette"/>
/// (claro y oscuro); los tonos suaves son mezclas con la superficie. Las medidas del mockup están
/// en píxeles de 96 ppp y se escalan con <see cref="Scale"/> según el DPI de la ventana.
/// </summary>
internal static class Ui
{
    private static readonly Dictionary<(string Family, int Key, FontStyle Style), Font> FontCache = new();
    private static readonly object FontSync = new();
    private static string? _sans;
    private static string? _display;
    private static string? _mono;

    public static AppPalette Palette { get; set; } = AppPalette.Dark;

    /// <summary>Factor de escala: DPI de la ventana dividido entre 96.</summary>
    public static float Scale { get; set; } = 1f;

    public static Color Window => Palette.Window;

    public static Color Surface => Palette.Surface;

    public static Color Hover => Palette.SurfaceAlt;

    public static Color Line => Mix(Palette.Border, Palette.Surface, 0.45f);

    public static Color Ink => Palette.Text;

    public static Color Ink2 => Palette.MutedText;

    public static Color Ink3 => Mix(Palette.MutedText, Palette.Surface, 0.45f);

    public static Color Accent => Palette.Accent;

    public static Color AccentSoft => Tint(Palette.Accent, 0.16f);

    public static Color Ok => Palette.Success;

    public static Color OkSoft => Tint(Palette.Success, 0.16f);

    public static Color Warn => Palette.Warning;

    public static Color WarnSoft => Tint(Palette.Warning, 0.16f);

    public static Color Bad => Palette.Danger;

    public static Color BadSoft => Tint(Palette.Danger, 0.16f);

    /// <summary>Convierte una medida de diseño (px a 96 ppp) en píxeles del dispositivo.</summary>
    public static int S(float px) => (int)Math.Round(px * Scale);

    public static float SF(float px) => px * Scale;

    public static Color Mix(Color from, Color to, float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);
        return Color.FromArgb(
            from.R + (int)Math.Round((to.R - from.R) * amount),
            from.G + (int)Math.Round((to.G - from.G) * amount),
            from.B + (int)Math.Round((to.B - from.B) * amount));
    }

    /// <summary>Color con la intensidad dada sobre la superficie: un tono suave del acento.</summary>
    public static Color Tint(Color color, float amount) => Mix(Palette.Surface, color, amount);

    public static string SansFamily => _sans ??= Pick("Segoe UI Variable Text", "Segoe UI");

    public static string DisplayFamily => _display ??= Pick("Segoe UI Variable Display", "Segoe UI Variable Text", "Segoe UI");

    public static string MonoFamily => _mono ??= Pick("Cascadia Mono", "Consolas");

    /// <summary>Texto de interfaz. Tamaño en px de diseño.</summary>
    public static Font Sans(float px, FontStyle style = FontStyle.Regular) => Get(SansFamily, px, style);

    /// <summary>Títulos grandes.</summary>
    public static Font Display(float px, FontStyle style = FontStyle.Bold) => Get(DisplayFamily, px, style);

    /// <summary>Rutas, direcciones y registro.</summary>
    public static Font Mono(float px) => Get(MonoFamily, px, FontStyle.Regular);

    /// <summary>Símbolos de tema y de título.</summary>
    public static Font Symbol(float px) => Get("Segoe UI Symbol", px, FontStyle.Regular);

    public static void Quality(Graphics graphics)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
    }

    public static TextFormatFlags LeftMiddle =>
        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;

    public static void Text(Graphics graphics, string text, Font font, Color color, Rectangle bounds, TextFormatFlags flags)
    {
        if (string.IsNullOrEmpty(text) || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        TextRenderer.DrawText(graphics, text, font, bounds, color, flags);
    }

    public static int TextWidth(string text, Font font) =>
        TextRenderer.MeasureText(text, font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width;

    public static int TextHeight(string text, Font font, int width) =>
        TextRenderer.MeasureText(text, font, new Size(Math.Max(1, width), int.MaxValue), TextFormatFlags.NoPadding | TextFormatFlags.WordBreak).Height;

    public static GraphicsPath Rounded(RectangleF rectangle, float radius)
    {
        var path = new GraphicsPath();
        var diameter = Math.Max(0, Math.Min(radius * 2, Math.Min(rectangle.Width, rectangle.Height)));
        if (diameter <= 0)
        {
            path.AddRectangle(rectangle);
            return path;
        }

        path.AddArc(rectangle.X, rectangle.Y, diameter, diameter, 180, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Y, diameter, diameter, 270, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rectangle.X, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    public static void FillRounded(Graphics graphics, Color color, RectangleF rectangle, float radius)
    {
        using var brush = new SolidBrush(color);
        using var path = Rounded(rectangle, radius);
        graphics.FillPath(brush, path);
    }

    public static void StrokeRounded(Graphics graphics, Color color, RectangleF rectangle, float radius, float width = 1f)
    {
        var inset = width / 2f;
        var inner = RectangleF.Inflate(rectangle, -inset, -inset);
        using var pen = new Pen(color, width);
        using var path = Rounded(inner, Math.Max(0, radius - inset));
        graphics.DrawPath(pen, path);
    }

    /// <summary>Dibuja un icono del mockup (viewBox de 16 unidades; el energía usa 24).</summary>
    public static void Icon(Graphics graphics, UiIcon icon, Rectangle bounds, Color color)
    {
        var state = graphics.Save();
        try
        {
            Quality(graphics);
            if (icon == UiIcon.Power)
            {
                DrawPower(graphics, bounds, color);
                return;
            }

            var scale = Math.Min(bounds.Width, bounds.Height) / 16f;
            var originX = bounds.X + (bounds.Width - 16f * scale) / 2f;
            var originY = bounds.Y + (bounds.Height - 16f * scale) / 2f;
            PointF P(float x, float y) => new(originX + x * scale, originY + y * scale);
            RectangleF R(float x, float y, float w, float h) => new(originX + x * scale, originY + y * scale, w * scale, h * scale);

            using var pen = new Pen(color, 1.4f * scale)
            {
                LineJoin = LineJoin.Round,
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };

            switch (icon)
            {
                case UiIcon.Home:
                    graphics.DrawPolygon(pen, new[] { P(2, 7), P(8, 2), P(14, 7), P(14, 14), P(2, 14) });
                    break;
                case UiIcon.Folder:
                    graphics.DrawPolygon(pen, new[] { P(1.5f, 4.5f), P(5.5f, 4.5f), P(7f, 6f), P(14.5f, 6f), P(14.5f, 13.5f), P(1.5f, 13.5f) });
                    break;
                case UiIcon.Tv:
                    graphics.DrawRectangle(pen, Rectangle.Round(R(1.5f, 2.5f, 13, 9)));
                    graphics.DrawLine(pen, P(5, 14), P(11, 14));
                    break;
                case UiIcon.Film:
                    graphics.DrawRectangle(pen, Rectangle.Round(R(1.5f, 3, 13, 10)));
                    graphics.DrawLine(pen, P(5, 3), P(5, 13));
                    graphics.DrawLine(pen, P(11, 3), P(11, 13));
                    break;
                case UiIcon.Activity:
                    graphics.DrawLines(pen, new[] { P(1, 8), P(4, 8), P(6, 3), P(10, 13), P(12, 8), P(15, 8) });
                    break;
                case UiIcon.Gear:
                    graphics.DrawEllipse(pen, R(5.8f, 5.8f, 4.4f, 4.4f));
                    graphics.DrawLines(pen, new[] { P(8, 1.5f), P(8, 3.5f) });
                    graphics.DrawLines(pen, new[] { P(8, 12.5f), P(8, 14.5f) });
                    graphics.DrawLines(pen, new[] { P(1.5f, 8), P(3.5f, 8) });
                    graphics.DrawLines(pen, new[] { P(12.5f, 8), P(14.5f, 8) });
                    graphics.DrawLines(pen, new[] { P(3.4f, 3.4f), P(4.8f, 4.8f) });
                    graphics.DrawLines(pen, new[] { P(11.2f, 11.2f), P(12.6f, 12.6f) });
                    graphics.DrawLines(pen, new[] { P(3.4f, 12.6f), P(4.8f, 11.2f) });
                    graphics.DrawLines(pen, new[] { P(11.2f, 4.8f), P(12.6f, 3.4f) });
                    break;
                case UiIcon.Close:
                    graphics.DrawLines(pen, new[] { P(3, 3), P(13, 13) });
                    graphics.DrawLines(pen, new[] { P(13, 3), P(3, 13) });
                    break;
            }
        }
        finally
        {
            graphics.Restore(state);
        }
    }

    private static void DrawPower(Graphics graphics, Rectangle bounds, Color color)
    {
        // Mockup: viewBox 24, trazo 2.2, línea de 3 a 11 y arco de radio 8 centrado en (12, 12.2).
        var scale = Math.Min(bounds.Width, bounds.Height) / 24f;
        var originX = bounds.X + (bounds.Width - 24f * scale) / 2f;
        var originY = bounds.Y + (bounds.Height - 24f * scale) / 2f;
        using var pen = new Pen(color, 2.2f * scale) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        var center = new PointF(originX + 12f * scale, originY + 12.2f * scale);
        var radius = 8f * scale;
        graphics.DrawLine(pen, originX + 12f * scale, originY + 3f * scale, originX + 12f * scale, originY + 11f * scale);
        graphics.DrawArc(
            pen,
            center.X - radius,
            center.Y - radius,
            radius * 2,
            radius * 2,
            224.5f,
            -260f);
    }

    private static string Pick(params string[] names)
    {
        var installed = new HashSet<string>(FontFamily.Families.Select(family => family.Name), StringComparer.OrdinalIgnoreCase);
        return names.FirstOrDefault(installed.Contains) ?? names[^1];
    }

    private static Font Get(string family, float px, FontStyle style)
    {
        var size = px * Scale * 0.75f;
        var key = (family, (int)Math.Round(px * Scale * 10), style);
        lock (FontSync)
        {
            if (!FontCache.TryGetValue(key, out var font))
            {
                font = new Font(family, size, style, GraphicsUnit.Point);
                FontCache[key] = font;
            }

            return font;
        }
    }
}

/// <summary>Iconos de la barra lateral, de las tarjetas y de los controles de la ventana.</summary>
internal enum UiIcon
{
    Home,
    Folder,
    Tv,
    Film,
    Activity,
    Gear,
    Power,
    Close
}
