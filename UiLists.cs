namespace FolderDlnaServer;

/// <summary>Color del indicador de una fila: lista, escaneando, en reproducción, etc.</summary>
internal enum ChipTone
{
    Neutral,
    Ok,
    Warn,
    Live,
    Bad
}

/// <summary>
/// Una fila de <see cref="RecordList"/>: ícono, título, línea secundaria, indicador opcional,
/// barra de avance opcional y una acción de texto a la derecha.
/// </summary>
internal sealed class RecordRow
{
    public RecordRow(UiIcon icon, string title, string meta)
    {
        Icon = icon;
        Title = title;
        Meta = meta;
    }

    public UiIcon Icon { get; }

    public string Title { get; }

    public string Meta { get; }

    /// <summary>El ícono va en acento (en reproducción) en vez de neutro.</summary>
    public bool IconAccent { get; init; }

    public string? Chip { get; init; }

    public ChipTone Tone { get; init; }

    /// <summary>Punto dentro del indicador (pulsa en «en reproducción»).</summary>
    public bool ChipDot { get; init; }

    /// <summary>Avance de 0 a 1; null si la fila no tiene barra.</summary>
    public double? Progress { get; init; }

    public string? ActionText { get; init; }

    public bool ActionDanger { get; init; }

    public Action? Action { get; init; }

    /// <summary>La línea secundaria se corta por el medio como una ruta (conserva el final).</summary>
    public bool MetaPathEllipsis { get; init; }

    /// <summary>Zona de la acción en coordenadas del control, calculada al pintar.</summary>
    internal Rectangle ActionBounds { get; set; }
}

/// <summary>
/// Lista de filas dentro de una tarjeta, con la misma geometría que las listas del mockup
/// (ícono de 36, texto, indicador y acción). Su alto depende de las filas.
/// </summary>
internal sealed class RecordList : Card, IAutoHeight
{
    private readonly List<RecordRow> _rows = new();
    private string _emptyText = string.Empty;

    /// <summary>Reemplaza las filas. Si cambia el alto, avisa a la página para que la reacomode.</summary>
    public void SetRows(IEnumerable<RecordRow> rows, string emptyText)
    {
        var next = rows.ToList();
        var same = next.Count == _rows.Count
            && next.Zip(_rows).All(pair => pair.First.Title == pair.Second.Title
                && pair.First.Meta == pair.Second.Meta
                && pair.First.Chip == pair.Second.Chip
                && pair.First.Progress == pair.Second.Progress
                && pair.First.ActionText == pair.Second.ActionText)
            && emptyText == _emptyText;
        if (same)
        {
            return;
        }

        _rows.Clear();
        _rows.AddRange(next);
        _emptyText = emptyText;
        Invalidate();
        Parent?.PerformLayout();
    }

    public int HeightFor(int width)
    {
        if (_rows.Count == 0)
        {
            return Ui.S(64);
        }

        return _rows.Sum(RowHeight);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        Ui.Quality(graphics);
        if (_rows.Count == 0)
        {
            Ui.Text(
                graphics,
                _emptyText,
                Ui.Sans(13),
                Ui.Ink2,
                ClientRectangle,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
            return;
        }

        var top = 0;
        for (var index = 0; index < _rows.Count; index++)
        {
            var row = _rows[index];
            var height = RowHeight(row);
            if (index > 0)
            {
                using var pen = new Pen(Ui.Line);
                graphics.DrawLine(pen, 0, top, Width, top);
            }

            DrawRow(graphics, row, new Rectangle(0, top, Width, height));
            top += height;
        }
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        foreach (var row in _rows)
        {
            if (row.Action is not null && row.ActionBounds.Contains(e.Location))
            {
                row.Action();
                return;
            }
        }
    }

    private static int RowHeight(RecordRow row) => Ui.S(row.Progress is null ? 66 : 77);

    private static void DrawRow(Graphics graphics, RecordRow row, Rectangle bounds)
    {
        var padX = Ui.S(18);
        var iconSize = Ui.S(36);
        var iconBox = new Rectangle(bounds.X + padX, bounds.Y + (bounds.Height - iconSize) / 2, iconSize, iconSize);
        Ui.FillRounded(graphics, row.IconAccent ? Ui.AccentSoft : Ui.Hover, iconBox, Ui.S(8));
        var glyph = Ui.S(18);
        Ui.Icon(
            graphics,
            row.Icon,
            new Rectangle(iconBox.X + (iconSize - glyph) / 2, iconBox.Y + (iconSize - glyph) / 2, glyph, glyph),
            row.IconAccent ? Ui.Accent : Ui.Ink2);

        var centerY = bounds.Y + bounds.Height / 2;
        var rightWidth = 0;
        var chipWidth = 0;
        if (row.Chip is not null)
        {
            chipWidth = Ui.TextWidth(row.Chip, Ui.Sans(12)) + Ui.S(24) + (row.ChipDot ? Ui.S(12) : 0);
            rightWidth += chipWidth;
        }

        var actionWidth = 0;
        if (row.ActionText is not null)
        {
            actionWidth = Ui.TextWidth(row.ActionText, Ui.Sans(13)) + Ui.S(16);
            rightWidth += actionWidth + (row.Chip is null ? 0 : Ui.S(8));
        }

        var rightStart = bounds.Right - padX - rightWidth;
        var cursorX = rightStart;
        if (row.Chip is not null)
        {
            var chipRect = new Rectangle(cursorX, centerY - Ui.S(11), chipWidth, Ui.S(22));
            var (background, foreground) = ToneColors(row.Tone);
            Ui.FillRounded(graphics, background, chipRect, Ui.S(11));
            var textX = chipRect.X + Ui.S(12);
            if (row.ChipDot)
            {
                var dot = Ui.S(6);
                using var dotBrush = new SolidBrush(foreground);
                graphics.FillEllipse(dotBrush, chipRect.X + Ui.S(10), centerY - dot / 2f, dot, dot);
                textX += Ui.S(12);
            }

            Ui.Text(
                graphics,
                row.Chip,
                Ui.Sans(12),
                foreground,
                new Rectangle(textX, chipRect.Y, chipRect.Right - textX - Ui.S(12), chipRect.Height),
                Ui.LeftMiddle);
            cursorX = chipRect.Right + Ui.S(8);
        }

        if (row.ActionText is not null)
        {
            var actionRect = new Rectangle(bounds.Right - padX - actionWidth, centerY - Ui.S(16), actionWidth, Ui.S(32));
            row.ActionBounds = actionRect;
            Ui.Text(
                graphics,
                row.ActionText,
                Ui.Sans(13),
                row.ActionDanger ? Ui.Bad : Ui.Accent,
                actionRect,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
        }

        var textLeft = iconBox.Right + Ui.S(14);
        var textRight = (rightWidth > 0 ? rightStart : bounds.Right - padX) - Ui.S(14);
        var textWidth = Math.Max(0, textRight - textLeft);
        var titleHeight = Ui.S(19);
        var metaHeight = Ui.S(17);
        var contentHeight = titleHeight + Ui.S(1) + metaHeight + (row.Progress is null ? 0 : Ui.S(11));
        var top = bounds.Y + (bounds.Height - contentHeight) / 2;

        Ui.Text(
            graphics,
            row.Title,
            Ui.Sans(14, FontStyle.Bold),
            Ui.Ink,
            new Rectangle(textLeft, top, textWidth, titleHeight),
            Ui.LeftMiddle);
        Ui.Text(
            graphics,
            row.Meta,
            Ui.Sans(12.5f),
            Ui.Ink2,
            new Rectangle(textLeft, top + titleHeight + Ui.S(1), textWidth, metaHeight),
            row.MetaPathEllipsis
                ? TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.PathEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine
                : Ui.LeftMiddle);

        if (row.Progress is { } progress)
        {
            var track = new Rectangle(textLeft, top + titleHeight + metaHeight + Ui.S(9), Math.Min(textWidth, Ui.S(360)), Ui.S(3));
            Ui.FillRounded(graphics, Ui.Hover, track, Ui.S(2));
            var filled = (int)Math.Round(track.Width * Math.Clamp(progress, 0, 1));
            if (filled > 0)
            {
                Ui.FillRounded(graphics, Ui.Accent, new Rectangle(track.X, track.Y, filled, track.Height), Ui.S(2));
            }
        }
    }

    internal static (Color Background, Color Foreground) ToneColors(ChipTone tone) => tone switch
    {
        ChipTone.Ok => (Ui.OkSoft, Ui.Ok),
        ChipTone.Warn => (Ui.WarnSoft, Ui.Warn),
        ChipTone.Live => (Ui.AccentSoft, Ui.Accent),
        ChipTone.Bad => (Ui.BadSoft, Ui.Bad),
        _ => (Ui.Hover, Ui.Ink2)
    };
}

/// <summary>
/// Registro de actividad: hora, tipo en color y mensaje en monoespaciada (filas que se
/// ajustan al ancho). Se usa completo en Actividad y compacto en Inicio.
/// </summary>
internal sealed class LogList : Card, IAutoHeight
{
    private IReadOnlyList<ActivityEntry> _entries = Array.Empty<ActivityEntry>();
    private string _emptyText = string.Empty;

    public void SetEntries(IReadOnlyList<ActivityEntry> entries, string emptyText)
    {
        var same = entries.SequenceEqual(_entries) && emptyText == _emptyText;
        if (same)
        {
            return;
        }

        _entries = entries.ToList();
        _emptyText = emptyText;
        Invalidate();
        Parent?.PerformLayout();
    }

    public int HeightFor(int width)
    {
        if (_entries.Count == 0)
        {
            return Ui.S(64);
        }

        return _entries.Sum(entry => RowHeight(entry, width));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        Ui.Quality(graphics);
        if (_entries.Count == 0)
        {
            Ui.Text(
                graphics,
                _emptyText,
                Ui.Sans(13),
                Ui.Ink2,
                ClientRectangle,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
            return;
        }

        var top = 0;
        for (var index = 0; index < _entries.Count; index++)
        {
            var entry = _entries[index];
            var height = RowHeight(entry, Width);
            if (index > 0)
            {
                using var pen = new Pen(Ui.Line);
                graphics.DrawLine(pen, 0, top, Width, top);
            }

            if (top + height >= e.ClipRectangle.Top && top <= e.ClipRectangle.Bottom)
            {
                DrawEntry(graphics, entry, new Rectangle(0, top, Width, height));
            }

            top += height;
        }
    }

    private static int RowHeight(ActivityEntry entry, int width)
    {
        var messageWidth = MessageWidth(width);
        var lines = Ui.TextHeight(entry.Message, Ui.Mono(12.5f), messageWidth);
        return Math.Max(Ui.S(30), lines + Ui.S(16));
    }

    private static int MessageWidth(int width) =>
        Math.Max(40, width - Ui.S(18) * 2 - Ui.S(70) - Ui.S(92) - Ui.S(12) * 2);

    private static void DrawEntry(Graphics graphics, ActivityEntry entry, Rectangle bounds)
    {
        var padX = Ui.S(18);
        var timeX = bounds.X + padX;
        var kindX = timeX + Ui.S(70) + Ui.S(12);
        var messageX = kindX + Ui.S(92) + Ui.S(12);
        var top = bounds.Y + Ui.S(8);
        var flags = TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPadding | TextFormatFlags.WordBreak;

        Ui.Text(
            graphics,
            entry.Time.ToString("HH:mm:ss"),
            Ui.Mono(12.5f),
            Ui.Ink3,
            new Rectangle(timeX, top, Ui.S(70), Ui.S(20)),
            flags);

        var kindColor = entry.Kind switch
        {
            ActivityKind.Play => Ui.Accent,
            ActivityKind.Sys => Ui.Ok,
            ActivityKind.Err => Ui.Bad,
            _ => Ui.Ink2
        };
        Ui.Text(
            graphics,
            ActivityLog.KindLabel(entry.Kind),
            Ui.Sans(12, FontStyle.Bold),
            kindColor,
            new Rectangle(kindX, top, Ui.S(92), Ui.S(20)),
            flags);

        Ui.Text(
            graphics,
            entry.Message,
            Ui.Mono(12.5f),
            Ui.Ink,
            new Rectangle(messageX, top, bounds.Right - messageX - padX, bounds.Height - Ui.S(8)),
            flags);
    }
}
