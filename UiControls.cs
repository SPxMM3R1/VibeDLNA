namespace FolderDlnaServer;

/// <summary>
/// Control base de la ventana 1.1.0: se pinta entero (sin controles nativos) y, si
/// <see cref="Transparent"/> es verdadero, deja ver el fondo del contenedor, como una fila dentro de
/// una tarjeta.
/// </summary>
internal abstract class PaintedControl : Control
{
    private bool _transparent;

    protected PaintedControl()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.UserPaint
            | ControlStyles.ResizeRedraw
            | ControlStyles.SupportsTransparentBackColor
            | ControlStyles.Selectable,
            true);
        BackColor = Ui.Window;
    }

    /// <summary>Verdadero si el control va sobre una tarjeta y debe dejar ver su fondo.</summary>
    public bool Transparent
    {
        get => _transparent;
        set
        {
            _transparent = value;
            BackColor = value ? Color.Transparent : Ui.Window;
            Invalidate();
        }
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        if (Transparent)
        {
            base.OnPaintBackground(e);
            return;
        }

        e.Graphics.Clear(Ui.Window);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Invalidate();
    }
}

/// <summary>Tarjeta redondeada con la superficie de la paleta y su borde.</summary>
internal abstract class Card : PaintedControl
{
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(Ui.Window);
        var rectangle = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
        Ui.FillRounded(e.Graphics, Ui.Surface, rectangle, Ui.S(10));
        Ui.StrokeRounded(e.Graphics, Ui.Line, rectangle, Ui.S(10));
    }
}

/// <summary>Estilos de botón de la ventana: como los «btn» del mockup.</summary>
internal enum ButtonKind
{
    Outline,
    Primary,
    Ghost,
    GhostDanger
}

/// <summary>Botón de texto con los estilos del mockup (contorno, primario, fantasma).</summary>
internal sealed class ActionButton : PaintedControl
{
    private bool _hover;

    public ActionButton(string text, ButtonKind kind = ButtonKind.Outline)
    {
        Text = text;
        Kind = kind;
        Height = Ui.S(32);
        Width = PreferredWidth;
        Cursor = Cursors.Hand;
        TabStop = true;
    }

    public ButtonKind Kind { get; set; }

    /// <summary>Cambia el texto y ajusta el ancho al nuevo texto.</summary>
    public void SetCaption(string text)
    {
        Text = text;
        Width = PreferredWidth;
        Invalidate();
    }

    /// <summary>Ancho natural del texto más el relleno del estilo.</summary>
    public int PreferredWidth =>
        Ui.TextWidth(Text, Ui.Sans(13)) + Ui.S(Kind is ButtonKind.Ghost or ButtonKind.GhostDanger ? 16 : 30);

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _hover = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = false;
        Invalidate();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Enter or Keys.Space)
        {
            OnClick(EventArgs.Empty);
            e.Handled = true;
        }

        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        Ui.Quality(graphics);
        var rectangle = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
        var radius = Ui.S(6);

        Color text;
        var fill = false;
        var outline = false;
        var fillColor = Ui.Hover;
        switch (Kind)
        {
            case ButtonKind.Primary:
                fillColor = _hover ? Ui.Mix(Ui.Accent, Color.White, 0.08f) : Ui.Accent;
                fill = true;
                text = Color.White;
                break;
            case ButtonKind.Ghost:
                fillColor = Ui.AccentSoft;
                fill = _hover;
                text = Ui.Accent;
                break;
            case ButtonKind.GhostDanger:
                fillColor = Ui.BadSoft;
                fill = _hover;
                text = Ui.Bad;
                break;
            default:
                fillColor = Ui.Hover;
                fill = _hover;
                outline = true;
                text = Ui.Ink;
                break;
        }

        if (!Enabled)
        {
            text = Ui.Mix(text, Ui.Surface, 0.55f);
        }

        if (fill)
        {
            Ui.FillRounded(graphics, fillColor, rectangle, radius);
        }

        if (outline)
        {
            Ui.StrokeRounded(graphics, Ui.Line, rectangle, radius);
        }

        Ui.Text(
            graphics,
            Text,
            Ui.Sans(13),
            text,
            ClientRectangle,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
    }
}

/// <summary>Interruptor de 40 × 20 con el estilo del mockup.</summary>
internal sealed class ToggleSwitch : PaintedControl
{
    private bool _checked;

    public ToggleSwitch()
    {
        Size = new Size(Ui.S(40), Ui.S(20));
        Cursor = Cursors.Hand;
        TabStop = true;
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
        }
    }

    /// <summary>Se avisa solo cuando el usuario lo cambia, no al poner el valor desde código.</summary>
    public event EventHandler? Toggled;

    protected override void OnClick(EventArgs e)
    {
        if (!Enabled)
        {
            return;
        }

        Checked = !Checked;
        base.OnClick(e);
        Toggled?.Invoke(this, EventArgs.Empty);
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
        var graphics = e.Graphics;
        Ui.Quality(graphics);
        var rectangle = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
        var radius = Height / 2f;
        var knob = Height - Ui.S(8);
        if (Checked)
        {
            Ui.FillRounded(graphics, Enabled ? Ui.Accent : Ui.Mix(Ui.Accent, Ui.Surface, 0.5f), rectangle, radius);
        }
        else
        {
            Ui.StrokeRounded(graphics, Ui.Ink3, rectangle, radius);
        }

        var x = Checked ? Width - knob - Ui.S(4) : Ui.S(4);
        using var brush = new SolidBrush(Checked ? Color.White : Ui.Ink2);
        graphics.FillEllipse(brush, x, (Height - knob) / 2f, knob, knob);
    }
}

/// <summary>Píldora de filtro (Todo, Reproducción…): seleccionada en tinta sólida.</summary>
internal sealed class FilterPill : PaintedControl
{
    private bool _selected;
    private bool _hover;

    public FilterPill(string text)
    {
        Text = text;
        Height = Ui.S(28);
        Width = PreferredWidth;
        Cursor = Cursors.Hand;
        TabStop = true;
    }

    public bool Selected
    {
        get => _selected;
        set
        {
            _selected = value;
            Invalidate();
        }
    }

    public int PreferredWidth => Ui.TextWidth(Text, Ui.Sans(12.5f)) + Ui.S(24);

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _hover = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = false;
        Invalidate();
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
        var graphics = e.Graphics;
        Ui.Quality(graphics);
        var rectangle = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
        var radius = Height / 2f;
        Color text;
        if (Selected)
        {
            Ui.FillRounded(graphics, Ui.Ink, rectangle, radius);
            text = Ui.Surface;
        }
        else
        {
            Ui.FillRounded(graphics, Ui.Surface, rectangle, radius);
            Ui.StrokeRounded(graphics, Ui.Line, rectangle, radius);
            text = _hover ? Ui.Ink : Ui.Ink2;
        }

        Ui.Text(
            graphics,
            Text,
            Ui.Sans(12.5f),
            text,
            ClientRectangle,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
    }
}

/// <summary>Botón redondo de encendido: verde con halo cuando el servidor comparte.</summary>
internal sealed class PowerButton : PaintedControl
{
    private bool _on;
    private bool _hover;

    public PowerButton()
    {
        Size = new Size(Ui.S(76), Ui.S(76));
        Cursor = Cursors.Hand;
        TabStop = true;
    }

    public bool On
    {
        get => _on;
        set
        {
            _on = value;
            Invalidate();
        }
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _hover = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = false;
        Invalidate();
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
        var graphics = e.Graphics;
        Ui.Quality(graphics);
        var halo = Ui.S(76);
        var body = Ui.S(64);
        var haloRect = new RectangleF((Width - halo) / 2f, (Height - halo) / 2f, halo, halo);
        var bodyRect = new RectangleF((Width - body) / 2f, (Height - body) / 2f, body, body);
        if (On)
        {
            using var haloBrush = new SolidBrush(Ui.OkSoft);
            graphics.FillEllipse(haloBrush, haloRect);
        }

        var fill = On ? Ui.Ok : (_hover ? Ui.Mix(Ui.Hover, Ui.Ink3, 0.2f) : Ui.Hover);
        using (var bodyBrush = new SolidBrush(fill))
        {
            graphics.FillEllipse(bodyBrush, bodyRect);
        }

        var icon = Ui.S(26);
        Ui.Icon(
            graphics,
            UiIcon.Power,
            new Rectangle((Width - icon) / 2, (Height - icon) / 2, icon, icon),
            On ? Color.White : Ui.Ink3);
    }
}

/// <summary>Dirección en un recuadro monoespaciado.</summary>
internal sealed class CodeBox : PaintedControl
{
    public CodeBox(string text)
    {
        Text = text;
        Height = Ui.S(26);
        Width = PreferredWidth;
    }

    public int PreferredWidth => Ui.TextWidth(Text, Ui.Mono(12.5f)) + Ui.S(18);

    public void SetText(string text)
    {
        Text = text;
        Width = PreferredWidth;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        Ui.Quality(graphics);
        var rectangle = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
        Ui.FillRounded(graphics, Ui.Window, rectangle, Ui.S(5));
        Ui.StrokeRounded(graphics, Ui.Line, rectangle, Ui.S(5));
        Ui.Text(
            graphics,
            Text,
            Ui.Mono(12.5f),
            Enabled ? Ui.Ink : Ui.Ink3,
            ClientRectangle,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
    }
}

/// <summary>Aviso breve que aparece abajo y se oculta solo («Guardado»).</summary>
internal sealed class Toast : PaintedControl
{
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 1800 };

    public Toast()
    {
        Transparent = true;
        Height = Ui.S(34);
        Visible = false;
        _timer.Tick += (_, _) =>
        {
            _timer.Stop();
            Visible = false;
        };
    }

    /// <summary>Muestra el texto y devuelve el ancho que ocupa, para centrarlo.</summary>
    public int Show(string text)
    {
        Text = text;
        Width = Ui.TextWidth(text, Ui.Sans(13)) + Ui.S(32);
        Visible = true;
        Invalidate();
        _timer.Stop();
        _timer.Start();
        return Width;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        Ui.Quality(graphics);
        var rectangle = new RectangleF(0, 0, Width, Height);
        Ui.FillRounded(graphics, Ui.Ink, rectangle, Height / 2f);
        Ui.Text(
            graphics,
            Text,
            Ui.Sans(13),
            Ui.Surface,
            ClientRectangle,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
        }

        base.Dispose(disposing);
    }
}
