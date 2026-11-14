namespace FolderDlnaServer;

/// <summary>Inicio: estado del servidor, cifras, lo que se está viendo y los últimos movimientos.</summary>
internal sealed class InicioPage : PageView
{
    private readonly HeroCard _hero = new();
    private readonly StatsCard _stats = new();
    private readonly RecordList _viewing = new();
    private readonly LogList _log = new();
    private readonly ToolTip _approximate = new();

    public InicioPage(IAppActions actions)
    {
        AddBlock(new PageHeader("Inicio", "Lo que tu PC está compartiendo y quién lo está viendo."), 28);
        _approximate.SetToolTip(_viewing, "Posición aproximada: el servidor la deduce de los pedidos de video, no la recibe del reproductor.");

        _hero.Power.Click += (_, _) => actions.ToggleServer();
        _hero.Copy.Click += (_, _) => actions.CopyAddress();
        _hero.Rescan.Click += (_, _) => actions.Rescan();
        AddBlock(_hero, 12);
        AddBlock(_stats, 28);

        AddBlock(new SectionHeader("Viendo ahora"), 10);
        AddBlock(_viewing, 28);

        var verTodo = new ActionButton("Ver todo", ButtonKind.Ghost);
        verTodo.Click += (_, _) => actions.Navigate(AppSection.Actividad);
        AddBlock(new SectionHeader("Últimos movimientos", verTodo), 10);
        AddBlock(_log, 0);
    }

    public void Render(AppView view)
    {
        var library = view.Library;
        var counted = view.Running && library.Counted;
        var total = library.ThumbnailsTotal;

        _hero.SetState(
            view.Running,
            view.Running ? "Compartiendo en la red" : "Detenido",
            view.Running
                ? $"Las TVs de la casa ven «{view.Settings.DeviceName}» como servidor DLNA."
                : "Toca el botón para volver a compartir. Las TVs dejan de verlo en segundos.",
            view.Running ? "Anunciando cada 60 s" : "Sin anunciar",
            view.DescriptionUrl);

        _stats.Set(
            counted
                ? new[] { ViewFormat.Thousands(library.Videos), ViewFormat.Thousands(library.Audio), ViewFormat.Thousands(library.Images), ViewFormat.Thousands(library.ThumbnailsReady) }
                : new[] { "—", "—", "—", "—" },
            new[]
            {
                "videos",
                "canciones",
                "imágenes",
                counted ? $"de {ViewFormat.Thousands(total)} miniaturas listas" : "miniaturas"
            },
            counted && total > 0 ? (double)library.ThumbnailsReady / total : null);

        _viewing.SetRows(
            view.Watching.Select(watching => new RecordRow(UiIcon.Film, watching.Title, ViewFormat.NowPlayingMeta(
                watching.ClientDisplayName,
                watching.Position,
                watching.Duration,
                watching.Percent))
            {
                IconAccent = true,
                Chip = "En reproducción",
                Tone = ChipTone.Live,
                ChipDot = true,
                Progress = watching.Percent / 100.0
            }),
            view.Running
                ? "Nadie está viendo nada ahora."
                : "El servidor está detenido: las TVs no ven las carpetas.");

        _log.SetEntries(view.Log.Take(4).ToList(), "Todavía no hay movimientos en esta sesión.");
    }
}

/// <summary>Carpetas: lo que se comparte, con su estado, y los tipos que se sirven.</summary>
internal sealed class CarpetasPage : PageView
{
    private readonly IAppActions _actions;
    private readonly RecordList _folders = new();
    private readonly ToggleSwitch _video = new();
    private readonly ToggleSwitch _audio = new();
    private readonly ToggleSwitch _images = new();
    private readonly ToggleSwitch _watch = new();
    private bool _lastRunning;

    public CarpetasPage(IAppActions actions)
    {
        _actions = actions;
        AddBlock(new PageHeader("Carpetas", "Cada carpeta aparece como una entrada en la TV. Quitar una no cambia las demás ni su avance."), 28);

        var add = new ActionButton("Agregar carpeta", ButtonKind.Primary);
        add.Click += (_, _) => actions.AddFolder();
        AddBlock(new SectionHeader("Compartidas", add), 10);
        AddBlock(_folders, 28);

        AddBlock(new SectionHeader("Qué se comparte"), 10);
        var share = new CardStack();
        share.AddRow(ToggleRow("Video", "mkv, mp4, m4v, avi, ts, webm · sin convertir: la TV debe poder reproducirlos", _video, (s, v) => s.ShareVideos = v));
        share.AddRow(ToggleRow("Música", "mp3, flac, m4a, ogg, wav", _audio, (s, v) => s.ShareAudio = v));
        share.AddRow(ToggleRow("Imágenes", "jpg, png, webp, heic", _images, (s, v) => s.ShareImages = v));
        share.AddRow(ToggleRow("Detectar cambios solo", "Un archivo nuevo aparece en la TV sin reescanear", _watch, (s, v) => s.AutoRescanLibrary = v));
        AddBlock(share, 0);
    }

    public void Render(AppView view)
    {
        _lastRunning = view.Running;
        _video.Checked = view.Settings.ShareVideos;
        _audio.Checked = view.Settings.ShareAudio;
        _images.Checked = view.Settings.ShareImages;
        _watch.Checked = view.Settings.AutoRescanLibrary;

        var rows = view.Settings.MediaFolders.Select(folder => FolderRow(folder, view)).ToList();
        _folders.SetRows(rows, "Sin carpetas. Agrega una para que la TV vea algo.");
    }

    private RecordRow FolderRow(string folder, AppView view)
    {
        var stats = view.Library.Folders.FirstOrDefault(item => string.Equals(item.Path, folder, StringComparison.OrdinalIgnoreCase));
        string meta;
        string chip;
        var tone = ChipTone.Ok;
        double? progress = null;

        if (!Directory.Exists(folder))
        {
            meta = "La carpeta no está disponible.";
            chip = "No disponible";
            tone = ChipTone.Bad;
        }
        else if (!view.Running)
        {
            meta = "Inicia el servidor para contar sus archivos.";
            chip = "Lista";
        }
        else if (stats is null || !view.Library.Counted)
        {
            meta = "Contando archivos…";
            chip = "Escaneando…";
            tone = ChipTone.Warn;
        }
        else
        {
            meta = ViewFormat.FolderSummary(stats.Videos, stats.Audio, stats.Images, stats.Bytes);
            if (stats.Scanning)
            {
                chip = $"Escaneando {stats.Percent} %";
                tone = ChipTone.Warn;
                progress = stats.Percent / 100.0;
            }
            else
            {
                chip = "Lista";
            }
        }

        return new RecordRow(UiIcon.Folder, folder, meta)
        {
            Chip = chip,
            Tone = tone,
            Progress = progress,
            ActionText = "Quitar",
            ActionDanger = true,
            Action = () => _actions.RemoveFolder(folder)
        };
    }

    private SettingRow ToggleRow(string label, string note, ToggleSwitch toggle, Action<AppSettings, bool> apply)
    {
        var row = new SettingRow(label, note);
        row.SetAccessory(toggle);
        toggle.Toggled += (_, _) =>
        {
            var value = toggle.Checked;
            var others = new[] { _video.Checked, _audio.Checked, _images.Checked };
            if (!value && others.All(enabled => !enabled))
            {
                toggle.Checked = true;
                _actions.Notify("Deja al menos un tipo de archivo activo.");
                return;
            }

            _actions.ChangeSettings(settings => apply(settings, value), restartServer: true);
        };
        return row;
    }
}

/// <summary>Reproductores: quién pidió algo a este servidor y lo que cada uno dejó a medio ver.</summary>
internal sealed class ReproductoresPage : PageView
{
    private readonly RecordList _players = new();
    private readonly RecordList _resume = new();

    public ReproductoresPage()
    {
        AddBlock(new PageHeader("Reproductores", "Equipos que pidieron algo a este servidor. Se reconocen por su identidad, no por el nombre."), 28);
        AddBlock(new SectionHeader("Vistos en la red"), 10);
        AddBlock(_players, 28);

        AddBlock(new SectionHeader("Seguir viendo") { Note = "lo guarda cada reproductor" }, 10);
        AddBlock(_resume, 0);
    }

    public void Render(AppView view)
    {
        _players.SetRows(
            view.Players.Select(player =>
            {
                var watching = view.Running && view.Watching.Any(item => item.ClientKey == player.Key);
                return new RecordRow(
                    UiIcon.Tv,
                    player.Name,
                    ViewFormat.PlayerMeta(player.DeviceName, player.Model, player.PlayerId, player.Ip))
                {
                    IconAccent = watching,
                    Chip = watching ? "Viendo" : ViewFormat.Ago(player.LastSeen, view.Now),
                    Tone = watching ? ChipTone.Live : ChipTone.Neutral,
                    ChipDot = watching
                };
            }),
            "Ningún reproductor ha pedido nada todavía.");

        _resume.SetRows(
            view.Resume.Select(entry => new RecordRow(
                UiIcon.Film,
                entry.Title,
                $"{Path.GetDirectoryName(entry.FilePath) ?? string.Empty} · en {entry.PlayerName}")
            {
                Chip = $"{ViewFormat.Percent(entry.Percent)} %",
                Progress = entry.Percent / 100.0,
                MetaPathEllipsis = true
            }),
            "Nadie ha dejado nada a medio ver.");
    }
}

/// <summary>Actividad: el registro de la sesión, con filtros por tipo y búsqueda.</summary>
internal sealed class ActividadPage : PageView
{
    private readonly ActivityFilterBar _filters = new();
    private readonly LogList _log = new();
    private IReadOnlyList<ActivityEntry> _entries = Array.Empty<ActivityEntry>();

    public ActividadPage(string logPath)
    {
        AddBlock(new PageHeader("Actividad", $"Registro de esta sesión. El completo queda en {logPath}."), 28);
        AddBlock(_filters, 12);
        AddBlock(_log, 0);

        _filters.FilterChanged += (_, _) => RefreshLog();
        _filters.SearchChanged += (_, _) => RefreshLog();
    }

    public void Render(AppView view)
    {
        _entries = view.Log;
        RefreshLog();
    }

    private void RefreshLog()
    {
        var visible = ActivityLog.Apply(_entries, _filters.Filter, _filters.Query);
        _log.SetEntries(visible, "Nada coincide con el filtro.");
    }
}

/// <summary>Barra de filtros de Actividad: píldoras y un cuadro de búsqueda a la derecha.</summary>
internal sealed class ActivityFilterBar : Control, IAutoHeight
{
    private static readonly (ActivityFilter Filter, string Label)[] Options =
    {
        (ActivityFilter.All, "Todo"),
        (ActivityFilter.Play, "Reproducción"),
        (ActivityFilter.Nav, "Navegación"),
        (ActivityFilter.Sys, "Servidor"),
        (ActivityFilter.Err, "Errores")
    };

    private readonly List<FilterPill> _pills = new();
    private readonly TextBox _search = new();

    public ActivityFilterBar()
    {
        BackColor = Ui.Window;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        foreach (var option in Options)
        {
            var pill = new FilterPill(option.Label) { Tag = option.Filter };
            pill.Click += (_, _) => SelectFilter(option.Filter);
            _pills.Add(pill);
            Controls.Add(pill);
        }

        SelectFilter(ActivityFilter.All, notify: false);
        _search.BorderStyle = BorderStyle.FixedSingle;
        _search.Font = Ui.Sans(13);
        _search.Width = Ui.S(220);
        _search.PlaceholderText = "Buscar en el registro";
        _search.TextChanged += (_, _) => SearchChanged?.Invoke(this, EventArgs.Empty);
        Controls.Add(_search);
    }

    public ActivityFilter Filter { get; private set; } = ActivityFilter.All;

    public string Query => _search.Text;

    public event EventHandler? FilterChanged;

    public event EventHandler? SearchChanged;

    public int HeightFor(int width) => Math.Max(Ui.S(30), _search.Height);

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(Ui.Window);
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        var x = 0;
        foreach (var pill in _pills)
        {
            pill.Location = new Point(x, (Height - pill.Height) / 2);
            x += pill.Width + Ui.S(6);
        }

        _search.Location = new Point(Width - _search.Width, (Height - _search.Height) / 2);
    }

    private void SelectFilter(ActivityFilter filter, bool notify = true)
    {
        Filter = filter;
        foreach (var pill in _pills)
        {
            pill.Selected = pill.Tag is ActivityFilter tagged && tagged == filter;
        }

        if (notify)
        {
            FilterChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}

/// <summary>Opciones: servidor, Windows y actualizaciones. Se guardan al cambiarlas.</summary>
internal sealed class OpcionesPage : PageView
{
    private readonly IAppActions _actions;
    private readonly TextBox _name = new();
    private readonly CodeBox _address = new("—");
    private readonly ToggleSwitch _autoStart = new();
    private readonly ToggleSwitch _startWithWindows = new();
    private readonly ToggleSwitch _tray = new();
    private readonly ToggleSwitch _keepAwake = new();
    private readonly SettingRow _updateRow = new("VibeDLNA");
    private readonly ActionButton _updateButton = new("Buscar ahora");
    private AppSettings? _settings;

    public OpcionesPage(IAppActions actions)
    {
        _actions = actions;
        AddBlock(new PageHeader("Opciones", "Se guardan solas al cambiarlas."), 28);

        AddBlock(new SectionHeader("Servidor"), 10);
        var server = new CardStack();
        server.AddRow(NameRow());
        var addressRow = new SettingRow("Dirección", $"Puerto fijo {DlnaServer.DefaultPort} · red local");
        addressRow.SetAccessory(_address);
        server.AddRow(addressRow);
        server.AddRow(ToggleRow("Compartir al abrir la app", null, _autoStart, (s, v) => s.AutoStartServer = v, restart: false));
        AddBlock(server, 28);

        AddBlock(new SectionHeader("Windows"), 10);
        var windows = new CardStack();
        windows.AddRow(ToggleRow("Abrir con Windows", "Abre minimizada en la bandeja", _startWithWindows, (s, v) => s.StartWithWindows = v, restart: false));
        windows.AddRow(ToggleRow("Cerrar la ventana la deja en la bandeja", null, _tray, (s, v) => s.MinimizeToTray = v, restart: false));
        windows.AddRow(ToggleRow(
            "Evitar que el PC se suspenda mientras alguien ve algo",
            "Solo mientras hay alguien en «Viendo ahora»; el resto del tiempo manda tu plan de energía.",
            _keepAwake,
            (s, v) => s.KeepAwake = v,
            restart: false));
        AddBlock(windows, 28);

        AddBlock(new SectionHeader("Actualizaciones"), 10);
        _updateRow.SetAccessory(_updateButton);
        _updateButton.Click += (_, _) =>
        {
            if (_updateButton.Text == "Actualizar")
            {
                _actions.InstallUpdate();
            }
            else
            {
                _actions.CheckForUpdates();
            }
        };
        var updates = new CardStack();
        updates.AddRow(_updateRow);
        AddBlock(updates, 0);

        _name.Leave += (_, _) => CommitName();
        _name.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                CommitName();
            }
        };
    }

    public void Render(AppView view)
    {
        _settings = view.Settings;
        if (!_name.Focused)
        {
            _name.Text = view.Settings.DeviceName;
        }

        _address.SetText(view.HostAddress);
        _autoStart.Checked = view.Settings.AutoStartServer;
        _startWithWindows.Checked = view.Settings.StartWithWindows;
        _tray.Checked = view.Settings.MinimizeToTray;
        _keepAwake.Checked = view.Settings.KeepAwake;

        _updateRow.SetText($"VibeDLNA {view.Version}", view.UpdateStatus);
        var caption = view.UpdateAvailable ? "Actualizar" : "Buscar ahora";
        if (_updateButton.Text != caption)
        {
            _updateButton.SetCaption(caption);
        }

        _updateButton.Enabled = !view.UpdateBusy;
    }

    private SettingRow NameRow()
    {
        var row = new SettingRow("Nombre en la TV", "Cambiarlo no hace perder el avance: la TV reconoce el servidor por su identidad");
        _name.BorderStyle = BorderStyle.FixedSingle;
        _name.Font = Ui.Sans(13);
        _name.Width = Ui.S(240);
        _name.BackColor = Ui.Window;
        _name.ForeColor = Ui.Ink;
        row.SetAccessory(_name);
        return row;
    }

    private void CommitName()
    {
        var text = _name.Text.Trim();
        if (_settings is null || text.Length == 0 || text == _settings.DeviceName)
        {
            _name.Text = _settings?.DeviceName ?? _name.Text;
            return;
        }

        _actions.ChangeSettings(settings => settings.DeviceName = text, restartServer: true);
    }

    private SettingRow ToggleRow(string label, string? note, ToggleSwitch toggle, Action<AppSettings, bool> apply, bool restart)
    {
        var row = new SettingRow(label, note);
        row.SetAccessory(toggle);
        toggle.Toggled += (_, _) => _actions.ChangeSettings(settings => apply(settings, toggle.Checked), restart);
        return row;
    }
}
