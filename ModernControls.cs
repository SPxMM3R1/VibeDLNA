using System.Drawing.Drawing2D;

namespace FolderDlnaServer;

internal interface IThemeAware
{
    void ApplyPalette(AppPalette palette);
}

internal static class ThemePaint
{
    public static Color ResolveBackColor(Control control, AppPalette palette)
    {
        for (var parent = control.Parent; parent is not null; parent = parent.Parent)
        {
            if (parent is ModernCard)
            {
                return palette.Window;
            }

            if (parent.BackColor != Color.Transparent && parent.BackColor.A > 0)
            {
                return parent.BackColor;
            }
        }

        return palette.Window;
    }

    public static void PaintParentBackground(Control control, PaintEventArgs e, AppPalette palette)
    {
        using var brush = new SolidBrush(ResolveBackColor(control, palette));
        e.Graphics.FillRectangle(brush, control.ClientRectangle);
    }
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
        BackColor = palette.Window;
        ForeColor = palette.Text;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var background = new SolidBrush(_palette.Window);
        e.Graphics.FillRectangle(background, ClientRectangle);

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
    private Color _buttonFill;

    public ThemeIconButton()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        Cursor = Cursors.Hand;
        Size = new Size(46, 34);
        MinimumSize = new Size(42, 32);
        TabStop = true;
    }

    public bool IsDarkTheme { get; set; } = true;

    public void ApplyPalette(AppPalette palette)
    {
        _palette = palette;
        BackColor = ThemePaint.ResolveBackColor(this, palette);
        ForeColor = palette.Text;
        IsDarkTheme = palette.IsDark;
        Invalidate();
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        ThemePaint.PaintParentBackground(this, pevent, _palette);
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
        ThemePaint.PaintParentBackground(this, e, _palette);
        var bounds = ClientRectangle;
        bounds.Inflate(-1, -1);

        _buttonFill = ThemePaint.ResolveBackColor(this, _palette);
        if (_pressed)
        {
            _buttonFill = Blend(_buttonFill, _palette.Elevated, 0.35f);
        }

        using var hoverBrush = new SolidBrush(_hovered || _pressed ? Blend(_palette.Elevated, _palette.Accent, _palette.IsDark ? 0.08f : 0.05f) : _buttonFill);
        using var border = new Pen(_hovered ? _palette.Accent : Color.FromArgb(_palette.IsDark ? 72 : 96, _palette.Border));
        e.Graphics.FillRectangle(hoverBrush, bounds);
        e.Graphics.DrawRectangle(border, bounds);

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
        var radius = Math.Min(bounds.Width, bounds.Height) * 0.24f;
        using var moonBrush = new SolidBrush(Color.FromArgb(218, 232, 255));
        using var craterBrush = new SolidBrush(Color.FromArgb(92, 119, 145, 178));
        using var glowPen = new Pen(Color.FromArgb(75, _palette.AccentAlt), 1.6f);

        graphics.DrawEllipse(glowPen, center.X - radius - 3, center.Y - radius - 3, radius * 2 + 6, radius * 2 + 6);
        graphics.FillEllipse(moonBrush, center.X - radius, center.Y - radius, radius * 2, radius * 2);
        graphics.FillEllipse(craterBrush, center.X - radius * 0.35f, center.Y - radius * 0.42f, radius * 0.38f, radius * 0.38f);
        graphics.FillEllipse(craterBrush, center.X + radius * 0.22f, center.Y + radius * 0.08f, radius * 0.28f, radius * 0.28f);
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

internal enum FlatIconKind
{
    Folder,
    Play,
    Stop,
    Save,
    Settings
}

internal sealed class FlatIconButton : Control, IThemeAware
{
    private AppPalette _palette = AppPalette.Dark;
    private bool _hovered;
    private bool _pressed;

    public FlatIconButton()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        Cursor = Cursors.Hand;
        Size = new Size(40, 34);
        MinimumSize = new Size(40, 34);
        TabStop = true;
    }

    public FlatIconKind IconKind { get; set; }

    public bool IsAccent { get; set; }

    public void ApplyPalette(AppPalette palette)
    {
        _palette = palette;
        BackColor = ThemePaint.ResolveBackColor(this, palette);
        ForeColor = palette.Text;
        Invalidate();
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        ThemePaint.PaintParentBackground(this, pevent, _palette);
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

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        ThemePaint.PaintParentBackground(this, e, _palette);

        var bounds = ClientRectangle;
        bounds.Inflate(-2, -2);
        var fill = IsAccent
            ? Blend(_palette.Accent, _palette.Window, _palette.IsDark ? 0.18f : 0.08f)
            : _palette.SurfaceAlt;

        if (!Enabled)
        {
            fill = Blend(fill, _palette.Window, 0.42f);
        }
        else if (_pressed)
        {
            fill = Blend(fill, Color.Black, _palette.IsDark ? 0.15f : 0.05f);
        }
        else if (_hovered)
        {
            fill = Blend(fill, IsAccent ? _palette.AccentAlt : _palette.Elevated, _palette.IsDark ? 0.20f : 0.12f);
        }

        using var fillBrush = new SolidBrush(fill);
        using var border = new Pen(IsAccent
            ? Color.FromArgb(_hovered && Enabled ? 190 : 130, _palette.AccentAlt)
            : Color.FromArgb(_hovered && Enabled ? 120 : 70, _palette.Border));
        using var shape = RoundedRect(bounds, 10);
        e.Graphics.FillPath(fillBrush, shape);
        e.Graphics.DrawPath(border, shape);

        var color = !Enabled ? _palette.MutedText : IsAccent ? _palette.AccentAlt : _palette.Text;
        using var pen = new Pen(color, 2.2f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };
        using var brush = new SolidBrush(color);
        DrawIcon(e.Graphics, bounds, pen, brush);
    }

    private void DrawIcon(Graphics graphics, Rectangle bounds, Pen pen, Brush brush)
    {
        var cx = bounds.Left + bounds.Width / 2f;
        var cy = bounds.Top + bounds.Height / 2f;
        switch (IconKind)
        {
            case FlatIconKind.Folder:
                graphics.DrawLine(pen, cx - 10, cy - 4, cx - 4, cy - 4);
                graphics.DrawLine(pen, cx - 4, cy - 4, cx - 2, cy - 8);
                graphics.DrawLine(pen, cx - 2, cy - 8, cx + 4, cy - 8);
                graphics.DrawLine(pen, cx + 4, cy - 8, cx + 7, cy - 4);
                graphics.DrawRectangle(pen, cx - 10, cy - 4, 20, 14);
                break;
            case FlatIconKind.Play:
                var play = new[]
                {
                    new PointF(cx - 5, cy - 9),
                    new PointF(cx - 5, cy + 9),
                    new PointF(cx + 9, cy)
                };
                graphics.FillPolygon(brush, play);
                break;
            case FlatIconKind.Stop:
                using (var stopShape = RoundedRect(new Rectangle((int)(cx - 7), (int)(cy - 7), 14, 14), 4))
                {
                    graphics.FillPath(brush, stopShape);
                }
                break;
            case FlatIconKind.Save:
                graphics.DrawRectangle(pen, cx - 9, cy - 9, 18, 18);
                graphics.DrawLine(pen, cx - 5, cy - 9, cx - 5, cy - 1);
                graphics.DrawLine(pen, cx + 5, cy - 9, cx + 5, cy - 2);
                graphics.DrawRectangle(pen, cx - 5, cy + 3, 10, 6);
                break;
            case FlatIconKind.Settings:
                graphics.DrawEllipse(pen, cx - 7, cy - 7, 14, 14);
                graphics.FillEllipse(brush, cx - 2.5f, cy - 2.5f, 5, 5);
                graphics.DrawLine(pen, cx, cy - 12, cx, cy - 9);
                graphics.DrawLine(pen, cx, cy + 9, cx, cy + 12);
                graphics.DrawLine(pen, cx - 12, cy, cx - 9, cy);
                graphics.DrawLine(pen, cx + 9, cy, cx + 12, cy);
                break;
        }
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

    private static Color Blend(Color from, Color to, float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);
        return Color.FromArgb(
            from.A + (int)((to.A - from.A) * amount),
            from.R + (int)((to.R - from.R) * amount),
            from.G + (int)((to.G - from.G) * amount),
            from.B + (int)((to.B - from.B) * amount));
    }
}

internal sealed class ModernButton : Button, IThemeAware
{
    private AppPalette _palette = AppPalette.Dark;
    private bool _hovered;
    private bool _pressed;

    public ModernButton()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        FlatAppearance.MouseDownBackColor = Color.Transparent;
        FlatAppearance.MouseOverBackColor = Color.Transparent;
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
        BackColor = ThemePaint.ResolveBackColor(this, palette);
        Invalidate();
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        ThemePaint.PaintParentBackground(this, pevent, _palette);
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
        ThemePaint.PaintParentBackground(this, e, _palette);
        var bounds = ClientRectangle;
        bounds.Inflate(-1, -1);

        var fill = ThemePaint.ResolveBackColor(this, _palette);
        if (!Enabled)
        {
            fill = Blend(fill, _palette.Window, 0.45f);
        }
        else if (_pressed)
        {
            fill = Blend(fill, _palette.Elevated, 0.45f);
        }
        else if (_hovered)
        {
            fill = Blend(fill, IsPrimary ? _palette.Accent : _palette.Elevated, _palette.IsDark ? 0.18f : 0.10f);
        }

        using var brush = new SolidBrush(fill);
        using var border = new Pen(IsPrimary ? _palette.Accent : Color.FromArgb(_palette.IsDark ? 80 : 110, _palette.Border));
        e.Graphics.FillRectangle(brush, bounds);
        e.Graphics.DrawRectangle(border, bounds);

        using var accentPen = new Pen(IsPrimary ? _palette.Accent : _palette.Border, 2f);
        e.Graphics.DrawLine(accentPen, bounds.Left + 8, bounds.Bottom - 3, bounds.Right - 8, bounds.Bottom - 3);

        TextRenderer.DrawText(
            e.Graphics,
            Text,
            Font,
            bounds,
            Enabled ? (IsPrimary ? _palette.Accent : ForeColor) : _palette.MutedText,
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
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
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
        BackColor = ThemePaint.ResolveBackColor(this, palette);
        ForeColor = palette.Text;
        Invalidate();
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        ThemePaint.PaintParentBackground(this, pevent, _palette);
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        pevent.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        ThemePaint.PaintParentBackground(this, pevent, _palette);

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
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        Height = 30;
        Width = 156;
        Font = new Font("Segoe UI", 9f, FontStyle.Bold);
    }

    public StatusKind Kind { get; set; }

    public void ApplyPalette(AppPalette palette)
    {
        _palette = palette;
        BackColor = ThemePaint.ResolveBackColor(this, palette);
        ForeColor = palette.Text;
        Invalidate();
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        ThemePaint.PaintParentBackground(this, pevent, _palette);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        ThemePaint.PaintParentBackground(this, e, _palette);
        var accent = Kind switch
        {
            StatusKind.Online => _palette.Success,
            StatusKind.Warning => _palette.Warning,
            _ => _palette.MutedText
        };
        var fill = Color.FromArgb(_palette.IsDark ? 42 : 26, accent);
        var bounds = ClientRectangle;
        bounds.Inflate(-1, -1);

        using var brush = new SolidBrush(fill);
        using var border = new Pen(Color.FromArgb(_palette.IsDark ? 150 : 110, accent));
        using var dot = new SolidBrush(accent);
        e.Graphics.FillRectangle(brush, bounds);
        e.Graphics.DrawRectangle(border, bounds);
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
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        Size = new Size(72, 72);
        MinimumSize = new Size(56, 56);
    }

    public bool Active { get; set; }

    public void ApplyPalette(AppPalette palette)
    {
        _palette = palette;
        BackColor = ThemePaint.ResolveBackColor(this, palette);
        Invalidate();
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        ThemePaint.PaintParentBackground(this, pevent, _palette);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        ThemePaint.PaintParentBackground(this, e, _palette);
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
