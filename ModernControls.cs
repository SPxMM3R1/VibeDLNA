using System.Drawing.Drawing2D;

namespace FolderDlnaServer;

internal interface IThemeAware
{
    void ApplyPalette(AppPalette palette);
}

internal sealed class ModernCard : Panel, IThemeAware
{
    private AppPalette _palette = AppPalette.Dark;

    public ModernCard()
    {
        DoubleBuffered = true;
        Padding = new Padding(18);
        Margin = new Padding(0);
    }

    public string Title { get; set; } = string.Empty;

    public Color AccentColor { get; set; } = Color.Empty;

    public void ApplyPalette(AppPalette palette)
    {
        _palette = palette;
        BackColor = palette.Surface;
        ForeColor = palette.Text;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = ClientRectangle;
        bounds.Width -= 1;
        bounds.Height -= 1;

        using var path = RoundedRect(bounds, 8);
        using var brush = new SolidBrush(_palette.Surface);
        using var border = new Pen(_palette.Border);
        using var highlight = new Pen(Color.FromArgb(_palette.IsDark ? 32 : 110, Color.White));
        e.Graphics.FillPath(brush, path);
        e.Graphics.DrawPath(border, path);
        e.Graphics.DrawLine(highlight, bounds.Left + 10, bounds.Top + 1, bounds.Right - 10, bounds.Top + 1);

        if (!AccentColor.IsEmpty)
        {
            using var accentBrush = new LinearGradientBrush(
                new Rectangle(bounds.Left, bounds.Top, 4, bounds.Height),
                AccentColor,
                Color.FromArgb(Math.Max(80, (int)AccentColor.A), AccentColor.R, AccentColor.G, AccentColor.B),
                LinearGradientMode.Vertical);
            using var accentPath = RoundedRect(new Rectangle(bounds.Left, bounds.Top, 4, bounds.Height), 4);
            e.Graphics.FillPath(accentBrush, accentPath);
        }

        base.OnPaint(e);
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
}

internal sealed class ThemeIconButton : Control, IThemeAware
{
    private AppPalette _palette = AppPalette.Dark;
    private bool _hovered;
    private bool _pressed;

    public ThemeIconButton()
    {
        DoubleBuffered = true;
        Cursor = Cursors.Hand;
        Size = new Size(46, 34);
        MinimumSize = new Size(42, 32);
        TabStop = true;
    }

    public bool IsDarkTheme { get; set; } = true;

    public void ApplyPalette(AppPalette palette)
    {
        _palette = palette;
        BackColor = Color.Transparent;
        ForeColor = palette.Text;
        IsDarkTheme = palette.IsDark;
        Invalidate();
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        Invalidate();
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hovered = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hovered = false;
        _pressed = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        _pressed = true;
        Invalidate();
        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        _pressed = false;
        Invalidate();
        base.OnMouseUp(e);
    }

    protected override bool IsInputKey(Keys keyData) =>
        keyData is Keys.Space or Keys.Enter || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Space or Keys.Enter)
        {
            OnClick(EventArgs.Empty);
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = ClientRectangle;
        bounds.Inflate(-1, -1);

        var fill = _hovered
            ? Blend(_palette.Elevated, _palette.Accent, _palette.IsDark ? 0.18f : 0.09f)
            : _palette.Elevated;
        if (_pressed)
        {
            fill = Blend(fill, Color.Black, _palette.IsDark ? 0.22f : 0.06f);
        }

        using var path = RoundedRect(bounds, 8);
        using var brush = new SolidBrush(fill);
        using var border = new Pen(_hovered ? _palette.Accent : _palette.Border);
        e.Graphics.FillPath(brush, path);
        e.Graphics.DrawPath(border, path);

        if (IsDarkTheme)
        {
            DrawMoon(e.Graphics, bounds);
        }
        else
        {
            DrawSun(e.Graphics, bounds);
        }
    }

    private void DrawMoon(Graphics graphics, Rectangle bounds)
    {
        var center = new PointF(bounds.Left + bounds.Width / 2f, bounds.Top + bounds.Height / 2f);
        var radius = Math.Min(bounds.Width, bounds.Height) * 0.26f;
        using var moonBrush = new SolidBrush(Color.FromArgb(218, 232, 255));
        using var cutBrush = new SolidBrush(_hovered ? Blend(_palette.Elevated, _palette.Accent, 0.18f) : _palette.Elevated);
        graphics.FillEllipse(moonBrush, center.X - radius, center.Y - radius, radius * 2, radius * 2);
        graphics.FillEllipse(cutBrush, center.X - radius * 0.18f, center.Y - radius * 1.08f, radius * 2.08f, radius * 2.08f);

        using var dot = new SolidBrush(Color.FromArgb(118, _palette.AccentAlt));
        graphics.FillEllipse(dot, center.X - radius * 1.45f, center.Y + radius * 0.95f, 3.5f, 3.5f);
    }

    private void DrawSun(Graphics graphics, Rectangle bounds)
    {
        var center = new PointF(bounds.Left + bounds.Width / 2f, bounds.Top + bounds.Height / 2f);
        var radius = Math.Min(bounds.Width, bounds.Height) * 0.20f;
        using var sunBrush = new SolidBrush(Color.FromArgb(255, 184, 76));
        using var rayPen = new Pen(Color.FromArgb(235, 196, 32, 129), 1.7f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };

        for (var index = 0; index < 8; index++)
        {
            var angle = Math.PI * 2 * index / 8;
            var inner = radius * 1.55f;
            var outer = radius * 2.05f;
            var x1 = center.X + (float)Math.Cos(angle) * inner;
            var y1 = center.Y + (float)Math.Sin(angle) * inner;
            var x2 = center.X + (float)Math.Cos(angle) * outer;
            var y2 = center.Y + (float)Math.Sin(angle) * outer;
            graphics.DrawLine(rayPen, x1, y1, x2, y2);
        }

        graphics.FillEllipse(sunBrush, center.X - radius, center.Y - radius, radius * 2, radius * 2);
    }

    private static Color Blend(Color from, Color to, float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);
        return Color.FromArgb(
            from.A + (int)((to.A - from.A) * amount),
            from.R + (int)((to.R - from.R) * amount),
            from.G + (int)((to.G - from.G) * amount),
            from.B + (int)((to.B - from.B) * amount));
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
}

internal sealed class ModernButton : Button, IThemeAware
{
    private AppPalette _palette = AppPalette.Dark;
    private bool _hovered;
    private bool _pressed;

    public ModernButton()
    {
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Height = 38;
        MinimumSize = new Size(112, 38);
        Cursor = Cursors.Hand;
        TextAlign = ContentAlignment.MiddleCenter;
        UseVisualStyleBackColor = false;
    }

    public bool IsPrimary { get; set; }

    public void ApplyPalette(AppPalette palette)
    {
        _palette = palette;
        ForeColor = IsPrimary ? palette.ButtonText : palette.Text;
        BackColor = Color.Transparent;
        Invalidate();
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hovered = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hovered = false;
        _pressed = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs mevent)
    {
        _pressed = true;
        Invalidate();
        base.OnMouseDown(mevent);
    }

    protected override void OnMouseUp(MouseEventArgs mevent)
    {
        _pressed = false;
        Invalidate();
        base.OnMouseUp(mevent);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = ClientRectangle;
        bounds.Inflate(-1, -1);

        var fill = IsPrimary
            ? _palette.Accent
            : _palette.Elevated;
        if (!Enabled)
        {
            fill = Blend(fill, _palette.Window, 0.45f);
        }
        else if (_pressed)
        {
            fill = Blend(fill, Color.Black, _palette.IsDark ? 0.25f : 0.08f);
        }
        else if (_hovered)
        {
            fill = IsPrimary ? Blend(fill, Color.White, 0.12f) : Blend(fill, _palette.Accent, 0.12f);
        }

        using var path = RoundedRect(bounds, 8);
        using var brush = new SolidBrush(fill);
        using var border = new Pen(IsPrimary ? Blend(_palette.Accent, Color.White, 0.18f) : _palette.Border);
        e.Graphics.FillPath(brush, path);
        e.Graphics.DrawPath(border, path);

        TextRenderer.DrawText(
            e.Graphics,
            Text,
            Font,
            bounds,
            Enabled ? ForeColor : _palette.MutedText,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    private static Color Blend(Color from, Color to, float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);
        return Color.FromArgb(
            from.A + (int)((to.A - from.A) * amount),
            from.R + (int)((to.R - from.R) * amount),
            from.G + (int)((to.G - from.G) * amount),
            from.B + (int)((to.B - from.B) * amount));
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
}

internal sealed class ToggleSwitch : CheckBox, IThemeAware
{
    private AppPalette _palette = AppPalette.Dark;

    public ToggleSwitch()
    {
        AutoSize = false;
        Height = 34;
        Width = 300;
        Cursor = Cursors.Hand;
        TextAlign = ContentAlignment.MiddleLeft;
        Padding = new Padding(52, 0, 0, 0);
    }

    public void ApplyPalette(AppPalette palette)
    {
        _palette = palette;
        BackColor = Color.Transparent;
        ForeColor = palette.Text;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        pevent.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        pevent.Graphics.Clear(_palette.Surface);

        var track = new Rectangle(0, 6, 42, 22);
        var knobSize = 18;
        var knobX = Checked ? 21 : 3;
        var trackColor = Checked ? _palette.AccentAlt : _palette.Elevated;
        if (!Enabled)
        {
            trackColor = _palette.Border;
        }

        using var trackPath = RoundedRect(track, 11);
        using var trackBrush = new SolidBrush(trackColor);
        using var borderPen = new Pen(Checked ? _palette.AccentAlt : _palette.Border);
        using var innerGlow = new Pen(Color.FromArgb(Checked ? 70 : 28, Color.White));
        pevent.Graphics.FillPath(trackBrush, trackPath);
        pevent.Graphics.DrawPath(borderPen, trackPath);
        pevent.Graphics.DrawLine(innerGlow, track.Left + 8, track.Top + 2, track.Right - 8, track.Top + 2);

        using var knobBrush = new SolidBrush(_palette.IsDark ? Color.White : Color.FromArgb(250, 252, 255));
        pevent.Graphics.FillEllipse(knobBrush, knobX, 8, knobSize, knobSize);

        TextRenderer.DrawText(
            pevent.Graphics,
            Text,
            Font,
            new Rectangle(52, 0, Width - 52, Height),
            Enabled ? _palette.Text : _palette.MutedText,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
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
}

internal enum StatusKind
{
    Offline,
    Online,
    Warning
}

internal sealed class StatusPill : Control, IThemeAware
{
    private AppPalette _palette = AppPalette.Dark;

    public StatusPill()
    {
        DoubleBuffered = true;
        Height = 30;
        Width = 156;
        Font = new Font("Segoe UI", 9f, FontStyle.Bold);
    }

    public StatusKind Kind { get; set; }

    public void ApplyPalette(AppPalette palette)
    {
        _palette = palette;
        BackColor = Color.Transparent;
        ForeColor = palette.Text;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var accent = Kind switch
        {
            StatusKind.Online => _palette.Success,
            StatusKind.Warning => _palette.Warning,
            _ => _palette.MutedText
        };
        var fill = Color.FromArgb(_palette.IsDark ? 42 : 26, accent);
        var bounds = ClientRectangle;
        bounds.Inflate(-1, -1);

        using var path = RoundedRect(bounds, 8);
        using var brush = new SolidBrush(fill);
        using var border = new Pen(Color.FromArgb(_palette.IsDark ? 150 : 110, accent));
        using var dot = new SolidBrush(accent);
        e.Graphics.FillPath(brush, path);
        e.Graphics.DrawPath(border, path);
        e.Graphics.FillEllipse(dot, 12, 11, 8, 8);

        TextRenderer.DrawText(
            e.Graphics,
            Text,
            Font,
            new Rectangle(27, 0, Width - 34, Height),
            _palette.Text,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
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
}

internal sealed class LogoMark : Control, IThemeAware
{
    private AppPalette _palette = AppPalette.Dark;

    public LogoMark()
    {
        DoubleBuffered = true;
        Size = new Size(72, 72);
        MinimumSize = new Size(56, 56);
    }

    public bool Active { get; set; }

    public void ApplyPalette(AppPalette palette)
    {
        _palette = palette;
        BackColor = Color.Transparent;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        AppIcon.DrawLogo(e.Graphics, ClientRectangle, Active);

        if (Active)
        {
            using var pen = new Pen(Color.FromArgb(90, _palette.AccentAlt), 2);
            var rect = ClientRectangle;
            rect.Inflate(-4, -4);
            e.Graphics.DrawEllipse(pen, rect);
        }
    }
}
