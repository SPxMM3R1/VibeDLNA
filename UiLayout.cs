namespace FolderDlnaServer;

/// <summary>Bloque de página con alto calculado según el ancho disponible.</summary>
internal interface IAutoHeight
{
    int HeightFor(int width);
}

/// <summary>
/// Página de la ventana: apila sus bloques en columna con los márgenes del mockup (32 px a los lados,
/// 28 arriba) y se desplaza si no caben. Cada bloque dice su alto con <see cref="IAutoHeight"/>.
/// </summary>
internal class PageView : ScrollableControl
{
    private readonly List<Control> _blocks = new();

    public PageView()
    {
        AutoScroll = true;
        BackColor = Ui.Window;
        // Se pinta el fondo a mano: con Mica activa, un fondo que no se repinta deja ver el material del sistema.
        SetStyle(
            ControlStyles.AllPaintingInWmPaint
            | ControlStyles.UserPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw,
            true);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(Ui.Window);
    }

    /// <summary>Agrega un bloque; <paramref name="gapAfter"/> es el espacio debajo, en px de diseño.</summary>
    public void AddBlock(Control block, int gapAfter)
    {
        block.Margin = new Padding(0, 0, 0, Ui.S(gapAfter));
        _blocks.Add(block);
        Controls.Add(block);
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        var padX = Ui.S(32);
        var top = Ui.S(28);
        var bottom = Ui.S(36);
        var width = Math.Max(1, ClientSize.Width - padX * 2);

        var y = top;
        foreach (var block in _blocks)
        {
            var height = block is IAutoHeight auto ? auto.HeightFor(width) : block.Height;
            block.SetBounds(padX, y, width, height);
            y += height + block.Margin.Bottom;
        }

        var last = _blocks.Count == 0 ? 0 : _blocks[^1].Margin.Bottom;
        var total = y - last + bottom;
        if (AutoScrollMinSize.Height != total)
        {
            AutoScrollMinSize = new Size(0, total);
        }
    }
}

/// <summary>Título de página (26 px) y su línea de descripción.</summary>
internal sealed class PageHeader : PaintedControl, IAutoHeight
{
    private readonly string _subtitle;

    public PageHeader(string title, string subtitle)
    {
        Text = title;
        _subtitle = subtitle;
    }

    public int HeightFor(int width) =>
        TitleHeight() + Ui.S(6) + Ui.TextHeight(_subtitle, Ui.Sans(13.5f), width);

    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        Ui.Quality(graphics);
        var titleHeight = TitleHeight();
        Ui.Text(
            graphics,
            Text,
            Ui.Display(26),
            Ui.Ink,
            new Rectangle(0, 0, Width, titleHeight),
            TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        Ui.Text(
            graphics,
            _subtitle,
            Ui.Sans(13.5f),
            Ui.Ink2,
            new Rectangle(0, titleHeight + Ui.S(6), Width, Math.Max(0, Height - titleHeight - Ui.S(6))),
            TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPadding | TextFormatFlags.WordBreak);
    }

    private static int TitleHeight() => Ui.TextHeight("Hg", Ui.Display(26), 1000);
}

/// <summary>Encabezado de sección en mayúsculas, con un control opcional a la derecha.</summary>
internal sealed class SectionHeader : PaintedControl, IAutoHeight
{
    private Control? _accessory;

    public SectionHeader(string title, Control? accessory = null)
    {
        Text = title;
        if (accessory is not null)
        {
            _accessory = accessory;
            Controls.Add(accessory);
        }
    }

    /// <summary>Nota pequeña a la derecha, sin mayúsculas (p. ej. «lo guarda cada reproductor»).</summary>
    public string Note { get; set; } = string.Empty;

    public int HeightFor(int width) => Ui.S(34);

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        if (_accessory is not null)
        {
            _accessory.Location = new Point(Width - _accessory.Width, (Height - _accessory.Height) / 2);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        Ui.Quality(graphics);
        var right = _accessory is null ? Width : _accessory.Left - Ui.S(12);
        Ui.Text(
            graphics,
            Text.ToUpperInvariant(),
            Ui.Sans(11.5f, FontStyle.Bold),
            Ui.Ink3,
            new Rectangle(0, 0, Math.Max(0, right), Height),
            Ui.LeftMiddle);

        if (Note.Length > 0)
        {
            var noteWidth = Ui.TextWidth(Note, Ui.Sans(12)) + Ui.S(8);
            Ui.Text(
                graphics,
                Note,
                Ui.Sans(12),
                Ui.Ink3,
                new Rectangle(Math.Max(0, right - noteWidth), 0, noteWidth, Height),
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
        }
    }
}

/// <summary>
/// Tarjeta con filas apiladas y líneas entre ellas (opciones, cuatro interruptores).
/// </summary>
internal sealed class CardStack : Card, IAutoHeight
{
    private readonly List<Control> _rows = new();

    public void AddRow(Control row)
    {
        if (row is PaintedControl painted)
        {
            painted.Transparent = true;
        }

        _rows.Add(row);
        Controls.Add(row);
        LayoutRows();
    }

    public int HeightFor(int width) => _rows.Sum(row => RowHeight(row, width));

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        LayoutRows();
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        LayoutRows();
    }

    private static int RowHeight(Control row, int width) =>
        row is IAutoHeight auto ? auto.HeightFor(width) : row.Height;

    private void LayoutRows()
    {
        var y = 0;
        for (var index = 0; index < _rows.Count; index++)
        {
            var row = _rows[index];
            var height = RowHeight(row, Width);
            row.SetBounds(0, y, Width, height);
            if (row is SettingRow settingRow)
            {
                settingRow.TopDivider = index > 0;
            }

            y += height;
        }
    }
}

/// <summary>
/// Fila de opciones: etiqueta y aclaración a la izquierda, control a la derecha (como «.opt» del mockup).
/// </summary>
internal sealed class SettingRow : PaintedControl, IAutoHeight
{
    private Control? _accessory;

    private string _label;
    private string _note;

    public SettingRow(string label, string? note = null)
    {
        _label = label;
        _note = note ?? string.Empty;
    }

    public string Label => _label;

    public string Note => _note;

    /// <summary>Cambia la etiqueta y la aclaración; reacomoda la página si el alto cambia.</summary>
    public void SetText(string label, string? note)
    {
        _label = label;
        _note = note ?? string.Empty;
        Invalidate();
        Parent?.PerformLayout();
    }

    /// <summary>Línea superior que separa esta fila de la anterior.</summary>
    public bool TopDivider { get; set; }

    public void SetAccessory(Control accessory)
    {
        if (accessory is PaintedControl painted)
        {
            painted.Transparent = true;
        }

        _accessory = accessory;
        Controls.Add(accessory);
        PositionAccessory();
    }

    public int HeightFor(int width)
    {
        var textWidth = TextWidth(width);
        var labelHeight = Ui.TextHeight(Label, Ui.Sans(14), textWidth);
        var noteHeight = Note.Length == 0 ? 0 : Ui.S(2) + Ui.TextHeight(Note, Ui.Sans(12.5f), textWidth);
        var accessoryHeight = _accessory?.Height ?? 0;
        return Math.Max(Ui.S(52), Math.Max(labelHeight + noteHeight, accessoryHeight) + Ui.S(28));
    }

    protected override void OnResize(EventArgs eventargs)
    {
        base.OnResize(eventargs);
        PositionAccessory();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        Ui.Quality(graphics);
        if (TopDivider)
        {
            using var pen = new Pen(Ui.Line);
            graphics.DrawLine(pen, 0, 0, Width, 0);
        }

        var textWidth = TextWidth(Width);
        var labelHeight = Ui.TextHeight(Label, Ui.Sans(14), textWidth);
        var noteHeight = Note.Length == 0 ? 0 : Ui.S(2) + Ui.TextHeight(Note, Ui.Sans(12.5f), textWidth);
        var top = (Height - labelHeight - noteHeight) / 2;
        Ui.Text(
            graphics,
            Label,
            Ui.Sans(14),
            Ui.Ink,
            new Rectangle(Ui.S(18), top, textWidth, labelHeight),
            TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
        if (Note.Length > 0)
        {
            Ui.Text(
                graphics,
                Note,
                Ui.Sans(12.5f),
                Ui.Ink2,
                new Rectangle(Ui.S(18), top + labelHeight + Ui.S(2), textWidth, noteHeight - Ui.S(2)),
                TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
        }
    }

    private int TextWidth(int width) =>
        Math.Max(40, width - Ui.S(36) - (_accessory is null ? 0 : _accessory.Width + Ui.S(16)));

    private void PositionAccessory()
    {
        if (_accessory is null)
        {
            return;
        }

        _accessory.Location = new Point(Width - Ui.S(18) - _accessory.Width, (Height - _accessory.Height) / 2);
    }
}

/// <summary>Tarjeta de cuatro cifras (videos, canciones, imágenes y miniaturas con su barra).</summary>
internal sealed class StatsCard : Card, IAutoHeight
{
    private readonly string[] _values = { "—", "—", "—", "—" };
    private readonly string[] _captions = { "videos", "canciones", "imágenes", "miniaturas" };
    private double? _meter;

    public void Set(string[] values, string[] captions, double? meter)
    {
        Array.Copy(values, _values, 4);
        Array.Copy(captions, _captions, 4);
        _meter = meter;
        Invalidate();
    }

    public int HeightFor(int width) => Ui.S(94);

    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        Ui.Quality(graphics);
        var cellWidth = Width / 4f;
        for (var index = 0; index < 4; index++)
        {
            var x = index * cellWidth;
            if (index > 0)
            {
                using var pen = new Pen(Ui.Line);
                graphics.DrawLine(pen, x, 0, x, Height);
            }

            var inner = (int)x + Ui.S(20);
            var innerWidth = (int)cellWidth - Ui.S(40);
            Ui.Text(
                graphics,
                _values[index],
                Ui.Display(24),
                Ui.Ink,
                new Rectangle(inner, Ui.S(16), innerWidth, Ui.S(30)),
                TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
            Ui.Text(
                graphics,
                _captions[index],
                Ui.Sans(12.5f),
                Ui.Ink2,
                new Rectangle(inner, Ui.S(50), innerWidth, Ui.S(18)),
                TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);

            if (index == 3 && _meter is { } meter)
            {
                var track = new Rectangle(inner, Ui.S(74), innerWidth, Ui.S(4));
                Ui.FillRounded(graphics, Ui.Hover, track, Ui.S(2));
                var filled = (int)Math.Round(track.Width * Math.Clamp(meter, 0, 1));
                if (filled > 0)
                {
                    Ui.FillRounded(graphics, Ui.Accent, new Rectangle(track.X, track.Y, filled, track.Height), Ui.S(2));
                }
            }
        }
    }
}

/// <summary>
/// Tarjeta de estado del servidor: botón de encendido de 64 px, título, aclaración, la dirección con
/// «Copiar» y «Reescanear» a la derecha.
/// </summary>
internal sealed class HeroCard : Card, IAutoHeight
{
    private string _title = string.Empty;
    private string _subtitle = string.Empty;
    private string _status = string.Empty;
    private bool _running;

    public HeroCard()
    {
        Power = new PowerButton { Transparent = true };
        Address = new CodeBox("—") { Transparent = true };
        Copy = new ActionButton("Copiar", ButtonKind.Ghost) { Transparent = true };
        Rescan = new ActionButton("Reescanear") { Transparent = true };
        Controls.Add(Power);
        Controls.Add(Address);
        Controls.Add(Copy);
        Controls.Add(Rescan);
    }

    public PowerButton Power { get; }

    public CodeBox Address { get; }

    public ActionButton Copy { get; }

    public ActionButton Rescan { get; }

    public int HeightFor(int width) => Ui.S(118);

    public void SetState(bool running, string title, string subtitle, string status, string address)
    {
        _running = running;
        _title = title;
        _subtitle = subtitle;
        _status = status;
        Power.On = running;
        Address.SetText(address);
        Rescan.Enabled = running;
        LayoutChildren();
        Invalidate();
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        LayoutChildren();
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        LayoutChildren();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        Ui.Quality(graphics);
        var geometry = Geometry();
        Ui.Text(
            graphics,
            _title,
            Ui.Display(22),
            Ui.Ink,
            geometry.Title,
            TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
        Ui.Text(
            graphics,
            _subtitle,
            Ui.Sans(13.5f),
            Ui.Ink2,
            geometry.Subtitle,
            TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);

        var dot = Ui.S(8);
        var dotRect = new RectangleF(geometry.Row.X, geometry.Row.Y + (geometry.Row.Height - dot) / 2f, dot, dot);
        if (_running)
        {
            using var halo = new SolidBrush(Ui.OkSoft);
            graphics.FillEllipse(halo, dotRect.X - Ui.S(3), dotRect.Y - Ui.S(3), dot + Ui.S(6), dot + Ui.S(6));
        }

        using (var dotBrush = new SolidBrush(_running ? Ui.Ok : Ui.Ink3))
        {
            graphics.FillEllipse(dotBrush, dotRect);
        }

        Ui.Text(
            graphics,
            _status,
            Ui.Sans(12.5f),
            Ui.Ink2,
            geometry.Status,
            Ui.LeftMiddle);
    }

    private void LayoutChildren()
    {
        var geometry = Geometry();
        Rescan.Location = new Point(Width - Ui.S(24) - Rescan.Width, (Height - Rescan.Height) / 2);
        Power.Location = new Point(Ui.S(24) - Ui.S(6), (Height - Power.Height) / 2);
        Address.Location = new Point(geometry.Address.X, geometry.Address.Y);
        Copy.Location = new Point(geometry.Copy.X, geometry.Copy.Y);
    }

    private HeroGeometry Geometry()
    {
        var pad = Ui.S(24);
        var textLeft = Ui.S(24 + 64 + 24);
        var rescanWidth = Rescan.Width;
        var available = Math.Max(120, Width - textLeft - pad - rescanWidth - Ui.S(20));
        var titleHeight = Ui.TextHeight(_title.Length == 0 ? "Hg" : _title, Ui.Display(22), available);
        var subtitleHeight = Ui.TextHeight(_subtitle.Length == 0 ? "Hg" : _subtitle, Ui.Sans(13.5f), available);
        var rowHeight = Ui.S(26);
        var blockHeight = titleHeight + Ui.S(2) + subtitleHeight + Ui.S(14) + rowHeight;
        var top = (Height - blockHeight) / 2;

        var title = new Rectangle(textLeft, top, available, titleHeight);
        var subtitle = new Rectangle(textLeft, top + titleHeight + Ui.S(2), available, subtitleHeight);
        var rowY = top + titleHeight + Ui.S(2) + subtitleHeight + Ui.S(14);
        var row = new Rectangle(textLeft, rowY, Ui.S(8), rowHeight);

        var statusX = textLeft + Ui.S(16);
        var statusWidth = Ui.TextWidth(_status, Ui.Sans(12.5f)) + Ui.S(4);
        var status = new Rectangle(statusX, rowY, statusWidth, rowHeight);

        var addressX = statusX + statusWidth + Ui.S(14);
        var addressWidth = Address.Width;
        var addressRect = new Rectangle(addressX, rowY + (rowHeight - Address.Height) / 2, addressWidth, Address.Height);
        var copyRect = new Rectangle(addressRect.Right + Ui.S(8), rowY + (rowHeight - Copy.Height) / 2, Copy.Width, Copy.Height);
        return new HeroGeometry(title, subtitle, row, status, addressRect, copyRect);
    }

    private sealed record HeroGeometry(
        Rectangle Title,
        Rectangle Subtitle,
        Rectangle Row,
        Rectangle Status,
        Rectangle Address,
        Rectangle Copy);
}

/// <summary>Riel izquierdo con las cinco secciones. La elegida lleva barra magenta de 3 px.</summary>
internal sealed class NavRail : PaintedControl
{
    private static readonly (AppSection Section, string Label, UiIcon Icon)[] Items =
    {
        (AppSection.Inicio, "Inicio", UiIcon.Home),
        (AppSection.Carpetas, "Carpetas", UiIcon.Folder),
        (AppSection.Reproductores, "Reproductores", UiIcon.Tv),
        (AppSection.Actividad, "Actividad", UiIcon.Activity),
        (AppSection.Opciones, "Opciones", UiIcon.Gear)
    };

    private readonly Dictionary<AppSection, int?> _counts = new();
    private readonly Dictionary<AppSection, Rectangle> _bounds = new();
    private AppSection? _hover;

    public NavRail()
    {
        Cursor = Cursors.Hand;
    }

    public AppSection Current { get; set; } = AppSection.Inicio;

    public event EventHandler<AppSection>? Selected;

    public void SetCount(AppSection section, int? count)
    {
        if (_counts.TryGetValue(section, out var previous) && previous == count)
        {
            return;
        }

        _counts[section] = count;
        Invalidate();
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        _bounds.Clear();
        var y = Ui.S(12);
        foreach (var item in Items)
        {
            _bounds[item.Section] = new Rectangle(Ui.S(8), y, Width - Ui.S(16), Ui.S(36));
            y += Ui.S(38);
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var hit = SectionAt(e.Location);
        if (hit != _hover)
        {
            _hover = hit;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = null;
        Invalidate();
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (SectionAt(e.Location) is { } section)
        {
            Selected?.Invoke(this, section);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        Ui.Quality(graphics);
        using (var line = new Pen(Ui.Line))
        {
            graphics.DrawLine(line, Width - 1, 0, Width - 1, Height);
        }

        foreach (var item in Items)
        {
            if (!_bounds.TryGetValue(item.Section, out var rect))
            {
                continue;
            }

            var selected = item.Section == Current;
            if (selected)
            {
                Ui.FillRounded(graphics, Ui.Surface, rect, Ui.S(6));
                Ui.FillRounded(graphics, Ui.Accent, new Rectangle(rect.X, rect.Y + Ui.S(10), Ui.S(3), rect.Height - Ui.S(20)), Ui.S(2));
            }
            else if (_hover == item.Section)
            {
                Ui.FillRounded(graphics, Ui.Hover, rect, Ui.S(6));
            }

            var color = selected ? Ui.Ink : Ui.Ink2;
            var glyph = Ui.S(16);
            Ui.Icon(
                graphics,
                item.Icon,
                new Rectangle(rect.X + Ui.S(12), rect.Y + (rect.Height - glyph) / 2, glyph, glyph),
                color);
            Ui.Text(
                graphics,
                item.Label,
                Ui.Sans(13.5f, selected ? FontStyle.Bold : FontStyle.Regular),
                color,
                new Rectangle(rect.X + Ui.S(40), rect.Y, rect.Width - Ui.S(40) - Ui.S(36), rect.Height),
                Ui.LeftMiddle);

            if (_counts.TryGetValue(item.Section, out var count) && count is { } value)
            {
                Ui.Text(
                    graphics,
                    ViewFormat.Thousands(value),
                    Ui.Sans(11.5f),
                    Ui.Ink3,
                    new Rectangle(rect.Right - Ui.S(36), rect.Y, Ui.S(24), rect.Height),
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
            }
        }

        Ui.Text(
            graphics,
            "Todo se queda en tu red local.",
            Ui.Sans(12),
            Ui.Ink3,
            new Rectangle(Ui.S(20), Height - Ui.S(56), Width - Ui.S(40), Ui.S(40)),
            TextFormatFlags.Left | TextFormatFlags.Bottom | TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
    }

    private AppSection? SectionAt(Point point)
    {
        foreach (var pair in _bounds)
        {
            if (pair.Value.Contains(point))
            {
                return pair.Key;
            }
        }

        return null;
    }
}
