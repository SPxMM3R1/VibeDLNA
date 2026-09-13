using System.Drawing.Drawing2D;

namespace FolderDlnaServer;

internal sealed class WinUiCard : Panel, IThemeAware
{
    private AppPalette _palette = AppPalette.Dark;
    private bool _isActive;

    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive == value)
            {
                return;
            }

            _isActive = value;
            Invalidate();
        }
    }

    public WinUiCard()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw
            | ControlStyles.UserPaint
            | ControlStyles.SupportsTransparentBackColor,
            true);
        BackColor = Color.Transparent;
        Padding = new Padding(1);
    }

    public void ApplyPalette(AppPalette palette)
    {
        _palette = palette;
        Invalidate();
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var brush = new SolidBrush(GetFillColor());
        using var path = CreateRoundedPath(ClientRectangle, 10);
        e.Graphics.FillPath(brush, path);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(IsActive ? Blend(_palette.Success, _palette.Border, 0.35f) : _palette.Border, 1f);
        using var path = CreateRoundedPath(new Rectangle(0, 0, Math.Max(0, Width - 1), Math.Max(0, Height - 1)), 10);
        e.Graphics.DrawPath(pen, path);
    }

    private Color GetFillColor() => IsActive
        ? Blend(_palette.Surface, _palette.Success, _palette.IsDark ? 0.12f : 0.08f)
        : _palette.Surface;

    private static GraphicsPath CreateRoundedPath(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return path;
        }

        var diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
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

internal sealed class WinUiStatusIndicator : Control, IThemeAware
{
    private AppPalette _palette = AppPalette.Dark;
    private bool _isActive;

    public WinUiStatusIndicator()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw
            | ControlStyles.UserPaint,
            true);
        BackColor = Color.Transparent;
        AccessibleRole = AccessibleRole.Graphic;
    }

    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive == value)
            {
                return;
            }

            _isActive = value;
            Invalidate();
        }
    }

    public void ApplyPalette(AppPalette palette)
    {
        _palette = palette;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var size = Math.Min(28, Math.Max(0, Math.Min(Width, Height) - 4));
        if (size <= 0)
        {
            return;
        }

        var bounds = new Rectangle((Width - size) / 2, (Height - size) / 2, size, size);
        using (var brush = new SolidBrush(IsActive ? _palette.Success : _palette.MutedText))
        {
            e.Graphics.FillEllipse(brush, bounds);
        }

        if (IsActive)
        {
            using var checkFont = new Font("Segoe UI", Math.Max(9f, size * 0.55f), FontStyle.Bold);
            TextRenderer.DrawText(
                e.Graphics,
                "✓",
                checkFont,
                bounds,
                _palette.ButtonText,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }
}

internal sealed class WinUiButton : Control, IThemeAware
{
    private AppPalette _palette = AppPalette.Dark;
    private bool _hovered;
    private bool _pressed;

    public WinUiButton()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw
            | ControlStyles.UserPaint,
            true);
        AutoSize = false;
        Height = 36;
        MinimumSize = new Size(92, 36);
        Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
        Cursor = Cursors.Hand;
        TabStop = true;
        AccessibleRole = AccessibleRole.PushButton;
    }

    public bool Primary { get; set; }

    public string Glyph { get; set; } = string.Empty;

    public void ApplyPalette(AppPalette palette)
    {
        _palette = palette;
        Invalidate();
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
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
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _pressed = true;
            Invalidate();
        }

        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        _pressed = false;
        Invalidate();
        base.OnMouseUp(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Space or Keys.Enter)
        {
            OnClick(EventArgs.Empty);
            e.Handled = true;
        }

        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = new Rectangle(0, 0, Math.Max(0, Width - 1), Math.Max(0, Height - 1));
        var fill = !Enabled
            ? _palette.SurfaceAlt
            : Primary
                ? (_pressed ? Blend(_palette.Accent, _palette.Text, 0.16f) : _hovered ? Blend(_palette.Accent, Color.White, 0.08f) : _palette.Accent)
                : (_pressed ? _palette.SurfaceAlt : _hovered ? _palette.Elevated : _palette.Surface);
        var foreground = !Enabled ? _palette.MutedText : Primary ? _palette.ButtonText : _palette.Text;
        using (var brush = new SolidBrush(fill))
        using (var path = CreateRoundedPath(bounds, 7))
        {
            e.Graphics.FillPath(brush, path);
        }

        using (var pen = new Pen(Primary ? fill : _palette.Border, 1f))
        using (var path = CreateRoundedPath(bounds, 7))
        {
            e.Graphics.DrawPath(pen, path);
        }

        var textBounds = new Rectangle(10, 0, Math.Max(0, Width - 20), Height);
        if (!string.IsNullOrWhiteSpace(Glyph))
        {
            var glyphBounds = new Rectangle(12, 0, 24, Height);
            using (var glyphFont = new Font("Segoe UI Symbol", 14f))
            {
                TextRenderer.DrawText(
                    e.Graphics,
                    Glyph,
                    glyphFont,
                    glyphBounds,
                    foreground,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
            textBounds.X += 20;
            textBounds.Width = Math.Max(0, textBounds.Width - 20);
        }

        TextRenderer.DrawText(
            e.Graphics,
            Text,
            Font,
            textBounds,
            foreground,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        if (Focused)
        {
            using var focusPen = new Pen(Primary ? _palette.ButtonText : _palette.Accent, 1f);
            var focusBounds = new Rectangle(3, 3, Math.Max(0, Width - 7), Math.Max(0, Height - 7));
            e.Graphics.DrawRectangle(focusPen, focusBounds);
        }
    }

    private static GraphicsPath CreateRoundedPath(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return path;
        }

        var diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
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

internal sealed class WinUiNavItem : Control, IThemeAware
{
    private AppPalette _palette = AppPalette.Dark;
    private bool _hovered;

    public WinUiNavItem()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw
            | ControlStyles.UserPaint,
            true);
        AutoSize = false;
        Height = 42;
        Margin = new Padding(0, 2, 0, 2);
        Font = new Font("Segoe UI", 10f, FontStyle.Regular);
        Cursor = Cursors.Hand;
        TabStop = true;
        AccessibleRole = AccessibleRole.ListItem;
    }

    public string Glyph { get; set; } = string.Empty;

    public bool Selected { get; set; }

    public void ApplyPalette(AppPalette palette)
    {
        _palette = palette;
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
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Space or Keys.Enter)
        {
            OnClick(EventArgs.Empty);
            e.Handled = true;
        }

        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var fill = Selected
            ? _palette.SurfaceAlt
            : _hovered
                ? _palette.Elevated
                : Color.Transparent;
        if (fill != Color.Transparent)
        {
            using var brush = new SolidBrush(fill);
            using var path = CreateRoundedPath(new Rectangle(0, 0, Math.Max(0, Width - 1), Math.Max(0, Height - 1)), 7);
            e.Graphics.FillPath(brush, path);
        }

        if (Selected)
        {
            using var accentBrush = new SolidBrush(_palette.Accent);
            e.Graphics.FillRectangle(accentBrush, new Rectangle(0, 7, 3, Math.Max(0, Height - 14)));
        }

        var foreground = Enabled ? _palette.Text : _palette.MutedText;
        using (var glyphFont = new Font("Segoe UI Symbol", 15f))
        {
            TextRenderer.DrawText(
                e.Graphics,
                Glyph,
                glyphFont,
                new Rectangle(14, 0, 30, Height),
                Selected ? _palette.Accent : foreground,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
        TextRenderer.DrawText(
            e.Graphics,
            Text,
            Font,
            new Rectangle(60, 0, Math.Max(0, Width - 70), Height),
            foreground,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);

        if (Focused)
        {
            using var pen = new Pen(_palette.Accent, 1f);
            e.Graphics.DrawRectangle(pen, new Rectangle(4, 4, Math.Max(0, Width - 9), Math.Max(0, Height - 9)));
        }
    }

    private static GraphicsPath CreateRoundedPath(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return path;
        }

        var diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal sealed class WinUiToggle : Control, IThemeAware
{
    private AppPalette _palette = AppPalette.Dark;
    private bool _hovered;
    private bool _checked;

    public WinUiToggle()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw
            | ControlStyles.UserPaint,
            true);
        AutoSize = false;
        Height = 40;
        Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
        Cursor = Cursors.Hand;
        TabStop = true;
        AccessibleRole = AccessibleRole.CheckButton;
    }

    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked == value)
            {
                return;
            }

            _checked = value;
            Invalidate();
            CheckedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? CheckedChanged;

    public string StateTextOn { get; set; } = string.Empty;

    public string StateTextOff { get; set; } = string.Empty;

    public void ApplyPalette(AppPalette palette)
    {
        _palette = palette;
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
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnClick(EventArgs e)
    {
        if (Enabled)
        {
            Checked = !Checked;
        }

        base.OnClick(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Space or Keys.Enter)
        {
            OnClick(EventArgs.Empty);
            e.Handled = true;
        }

        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var trackBounds = new Rectangle(2, 8, 44, 24);
        var trackColor = !Enabled
            ? _palette.SurfaceAlt
            : Checked
                ? (_hovered ? Blend(_palette.Accent, Color.White, 0.08f) : _palette.Accent)
                : _palette.Border;
        using (var brush = new SolidBrush(trackColor))
        using (var path = CreateRoundedPath(trackBounds, 12))
        {
            e.Graphics.FillPath(brush, path);
        }

        if (!Checked)
        {
            using var borderPen = new Pen(_palette.MutedText, 1f);
            using var borderPath = CreateRoundedPath(trackBounds, 12);
            e.Graphics.DrawPath(borderPen, borderPath);
        }

        var knobX = Checked ? trackBounds.Right - 20 : trackBounds.Left + 4;
        using (var knobBrush = new SolidBrush(Checked ? _palette.ButtonText : _palette.MutedText))
        {
            e.Graphics.FillEllipse(knobBrush, new Rectangle(knobX, trackBounds.Y + 4, 16, 16));
        }

        var stateText = Checked ? StateTextOn : StateTextOff;
        var stateWidth = string.IsNullOrWhiteSpace(stateText) ? 0 : Math.Min(104, Math.Max(0, Width - 58));
        var textWidth = Math.Max(0, Width - 58 - stateWidth - (stateWidth > 0 ? 8 : 0));
        TextRenderer.DrawText(
            e.Graphics,
            Text,
            Font,
            new Rectangle(58, 0, textWidth, Height),
            Enabled ? _palette.Text : _palette.MutedText,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        if (stateWidth > 0)
        {
            TextRenderer.DrawText(
                e.Graphics,
                stateText,
                Font,
                new Rectangle(58 + textWidth + 8, 0, stateWidth, Height),
                _palette.MutedText,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Right | TextFormatFlags.EndEllipsis);
        }

        if (Focused)
        {
            using var pen = new Pen(_palette.Accent, 1f);
            e.Graphics.DrawRectangle(pen, new Rectangle(0, 2, Math.Max(0, Width - 3), Math.Max(0, Height - 5)));
        }
    }

    private static GraphicsPath CreateRoundedPath(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return path;
        }

        var diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
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
