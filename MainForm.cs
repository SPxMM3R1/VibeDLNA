using System.Runtime.InteropServices;

namespace FolderDlnaServer;

/// <summary>
/// Ventana principal 1.1.0: sin barra de título nativa. Dibuja su propia barra (logo, nombre, versión,
/// tema y los tres botones), riel de cinco secciones y las páginas. Conserva el arrastre, el
/// redimensionado con los bordes y el ajuste de Windows mediante WM_NCHITTEST.
/// </summary>
internal sealed class MainForm : Form, IAppActions
{
    private const int WmSettingChange = 0x001A;
    private const int WmNcCalcSize = 0x0083;
    private const int WmNcHitTest = 0x0084;
    private const int WsThickFrame = 0x00040000;
    private const int WsMinimizeBox = 0x00020000;
    private const int WsMaximizeBox = 0x00010000;
    private const int HtClient = 1;
    private const int HtCaption = 2;
    private const int HtLeft = 10;
    private const int HtRight = 11;
    private const int HtTop = 12;
    private const int HtTopLeft = 13;
    private const int HtTopRight = 14;
    private const int HtBottom = 15;
    private const int HtBottomLeft = 16;
    private const int HtBottomRight = 17;
    private const int SmCxSizeFrame = 32;
    private const int SmCxPaddedBorder = 92;

    private enum TitleButton
    {
        Theme,
        Minimize,
        Maximize,
        Close
    }

    private sealed record PageSet(
        InicioPage Inicio,
        CarpetasPage Carpetas,
        ReproductoresPage Reproductores,
        ActividadPage Actividad,
        OpcionesPage Opciones)
    {
        public IEnumerable<PageView> All => new PageView[] { Inicio, Carpetas, Reproductores, Actividad, Opciones };
    }

    private readonly bool _requestedStartMinimized;
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _trayMenu;
    private readonly ToolStripMenuItem _trayStartStopItem;
    private readonly ToolStripMenuItem _trayShowItem;
    private readonly ToolStripMenuItem _trayExitItem;
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 1000 };
    private readonly GitHubUpdateService _updateService = new();
    private readonly List<ActivityEntry> _log = new();
    private readonly NavRail _rail = new();
    private readonly Toast _toast = new();

    private AppSettings _settings = new();
    private bool _startHidden;
    private AppPalette _palette = AppPalette.Dark;
    private AppThemeMode _themeMode = AppThemeMode.Dark;
    private DlnaServer? _server;
    private ViewingActivity? _idleViewing;
    private PageSet? _pages;
    private Icon? _windowIcon;
    private Icon? _trayIcon;
    private bool _isExiting;
    private bool _hasShown;
    private bool _serverBusy;
    private bool _keepAwakeActive;
    private bool _updateBusy;
    private bool _updateAvailable;
    private bool _updateChecked;
    private string _updateStatus = "Sin revisar todavía.";
    private GitHubReleaseInfo? _latestRelease;
    private DateTime? _lastCheck;
    private string _localIp = "127.0.0.1";
    private Rectangle _contentRect;
    private Rectangle _themeRect;
    private Rectangle _minimizeRect;
    private Rectangle _maximizeRect;
    private Rectangle _closeRect;
    private TitleButton? _hoverButton;
    private TitleButton? _pressedButton;

    public MainForm(bool requestedStartMinimized)
    {
        _requestedStartMinimized = requestedStartMinimized;
        Ui.Scale = DeviceDpi / 96f;

        // La configuración se lee antes de mostrar nada: con arranque oculto, la ventana nunca aparece.
        _settings = SettingsService.Load();
        _startHidden = requestedStartMinimized && _settings.StartMinimized && _settings.MinimizeToTray;
        if (_startHidden)
        {
            Opacity = 0;
            ShowInTaskbar = false;
        }

        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        Text = "VibeDLNA";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(Ui.S(1120), Ui.S(640));
        Size = new Size(Ui.S(1180), Ui.S(720));
        BackColor = Ui.Window;
        Font = Ui.Sans(13);
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);

        SetAppIcons(active: false);

        _trayShowItem = new ToolStripMenuItem("Mostrar", null, (_, _) => ShowFromTray());
        _trayStartStopItem = new ToolStripMenuItem("Iniciar servidor", null, async (_, _) => await ToggleServerAsync());
        _trayExitItem = new ToolStripMenuItem("Salir", null, async (_, _) => await ExitApplicationAsync());
        _trayMenu = new ContextMenuStrip();
        _trayMenu.Items.Add(_trayShowItem);
        _trayMenu.Items.Add(_trayStartStopItem);
        _trayMenu.Items.Add(new ToolStripSeparator());
        _trayMenu.Items.Add(_trayExitItem);

        _notifyIcon = new NotifyIcon
        {
            Icon = _trayIcon,
            Text = "VibeDLNA · detenido",
            ContextMenuStrip = _trayMenu,
            Visible = true
        };
        _notifyIcon.DoubleClick += (_, _) => ShowFromTray();

        BuildUi();
        DpiChanged += (_, e) =>
        {
            Ui.Scale = e.DeviceDpiNew / 96f;
            BuildPages();
            LayoutChrome();
            Render();
        };
        HandleCreated += (_, _) => NativeTheme.ApplyWindowEffects(this, _palette.IsDark);
        Load += async (_, _) => await OnLoadedAsync();
        Shown += (_, _) => HideIfStartupRequested();
        FormClosing += async (_, e) => await OnFormClosingAsync(e);
        _timer.Tick += (_, _) => Render();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            // Ventana sin marco que conserva el borde de redimensión y los botones de maximizar y minimizar.
            var parameters = base.CreateParams;
            parameters.Style |= WsThickFrame | WsMinimizeBox | WsMaximizeBox;
            return parameters;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
            _notifyIcon.Dispose();
            _trayMenu.Dispose();
            _server?.Dispose();
            _windowIcon?.Dispose();
            _trayIcon?.Dispose();
            if (_keepAwakeActive)
            {
                PowerKeepAwake.SetEnabled(false);
            }
        }

        base.Dispose(disposing);
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == WmNcCalcSize && message.WParam != IntPtr.Zero)
        {
            // Sin marco: el área cliente ocupa toda la ventana. Al maximizar, se retira el borde invisible.
            // Se pregunta al sistema (IsZoomed): WindowState va por detrás durante la restauración.
            if (IsZoomed(Handle))
            {
                var rectangle = Marshal.PtrToStructure<NativeRect>(message.LParam);
                var frame = GetSystemMetrics(SmCxSizeFrame) + GetSystemMetrics(SmCxPaddedBorder);
                rectangle.Left += frame;
                rectangle.Top += frame;
                rectangle.Right -= frame;
                rectangle.Bottom -= frame;
                Marshal.StructureToPtr(rectangle, message.LParam, false);
            }

            message.Result = IntPtr.Zero;
            return;
        }

        if (message.Msg == WmNcHitTest)
        {
            message.Result = (IntPtr)HitTest(message.LParam);
            return;
        }

        base.WndProc(ref message);

        if (message.Msg == WmSettingChange && _themeMode == AppThemeMode.System)
        {
            BeginInvoke(ApplyCurrentTheme);
        }
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(Ui.Window);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        Ui.Quality(graphics);
        var titleHeight = TitleHeight();
        using (var line = new Pen(Ui.Line))
        {
            graphics.DrawLine(line, 0, titleHeight - 1, ClientSize.Width, titleHeight - 1);
        }

        var logo = Ui.S(18);
        AppIcon.DrawLogo(graphics, new Rectangle(Ui.S(14), (titleHeight - logo) / 2, logo, logo), _server is { IsRunning: true });

        var nameFont = Ui.Sans(12.5f, FontStyle.Bold);
        var nameX = Ui.S(14) + logo + Ui.S(10);
        var nameWidth = Ui.TextWidth("VibeDLNA", nameFont);
        Ui.Text(graphics, "VibeDLNA", nameFont, Ui.Ink, new Rectangle(nameX, 0, nameWidth + Ui.S(4), titleHeight), Ui.LeftMiddle);
        Ui.Text(
            graphics,
            VersionText,
            Ui.Sans(12),
            Ui.Ink3,
            new Rectangle(nameX + nameWidth + Ui.S(8), 0, Ui.S(80), titleHeight),
            Ui.LeftMiddle);

        DrawThemeButton(graphics);
        DrawWindowButton(graphics, _minimizeRect, TitleButton.Minimize, _hoverButton == TitleButton.Minimize);
        DrawWindowButton(graphics, _maximizeRect, TitleButton.Maximize, _hoverButton == TitleButton.Maximize);
        DrawWindowButton(graphics, _closeRect, TitleButton.Close, _hoverButton == TitleButton.Close);
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        LayoutChrome();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        SetHoverButton(ButtonAt(e.Location));
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        SetHoverButton(null);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left)
        {
            _pressedButton = ButtonAt(e.Location);
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button == MouseButtons.Left && _pressedButton is { } pressed && ButtonAt(e.Location) == pressed)
        {
            ActivateTitleButton(pressed);
        }

        _pressedButton = null;
    }

    private void SetHoverButton(TitleButton? button)
    {
        if (button == _hoverButton)
        {
            return;
        }

        _hoverButton = button;
        Invalidate(new Rectangle(0, 0, ClientSize.Width, TitleHeight()));
    }

    private void BuildUi()
    {
        Controls.Add(_rail);
        _rail.Selected += (_, section) => Navigate(section);
        Controls.Add(_toast);
        BuildPages();
    }

    /// <summary>Crea las páginas con la escala actual. Al cambiar el DPI se vuelven a crear.</summary>
    private void BuildPages()
    {
        if (_pages is not null)
        {
            foreach (var page in _pages.All)
            {
                Controls.Remove(page);
                page.Dispose();
            }
        }

        _pages = new PageSet(
            new InicioPage(this),
            new CarpetasPage(this),
            new ReproductoresPage(),
            new ActividadPage(LogPathText),
            new OpcionesPage(this));
        foreach (var page in _pages.All)
        {
            Controls.Add(page);
        }

        Navigate(_rail.Current);
        LayoutChrome();
    }

    private void LayoutChrome()
    {
        var inset = IsZoomed(Handle) ? 0 : Ui.S(6);
        var top = TitleHeight();
        var width = ClientSize.Width;
        var height = ClientSize.Height;
        var railWidth = Ui.S(208);

        _rail.SetBounds(inset, top, railWidth, Math.Max(0, height - top - inset));
        _contentRect = new Rectangle(
            inset + railWidth,
            top,
            Math.Max(0, width - inset - railWidth - inset),
            Math.Max(0, height - top - inset));

        if (_pages is not null)
        {
            foreach (var page in _pages.All)
            {
                page.SetBounds(_contentRect.X, _contentRect.Y, _contentRect.Width, _contentRect.Height);
            }
        }

        var buttonWidth = Ui.S(46);
        _closeRect = new Rectangle(width - buttonWidth, 0, buttonWidth, top);
        _maximizeRect = new Rectangle(_closeRect.X - buttonWidth, 0, buttonWidth, top);
        _minimizeRect = new Rectangle(_maximizeRect.X - buttonWidth, 0, buttonWidth, top);
        var themeWidth = ThemeButtonWidth();
        _themeRect = new Rectangle(_minimizeRect.X - themeWidth - Ui.S(4), Ui.S(6), themeWidth, top - Ui.S(12));
        Invalidate();
    }

    private void Navigate(AppSection section)
    {
        _rail.Current = section;
        _rail.Invalidate();
        if (_pages is null)
        {
            return;
        }

        _pages.Inicio.Visible = section == AppSection.Inicio;
        _pages.Carpetas.Visible = section == AppSection.Carpetas;
        _pages.Reproductores.Visible = section == AppSection.Reproductores;
        _pages.Actividad.Visible = section == AppSection.Actividad;
        _pages.Opciones.Visible = section == AppSection.Opciones;

        if (section == AppSection.Opciones && !_updateChecked)
        {
            _updateChecked = true;
            CheckForUpdates();
        }

        Render();
    }

    private void Render()
    {
        if (IsDisposed || _pages is null)
        {
            return;
        }

        var running = _server is { IsRunning: true };
        var viewing = _server?.Viewing ?? _idleViewing;
        var library = running && _server is not null ? _server.Overview : LibraryOverview.Empty;
        IReadOnlyList<WatchingNow> watching = running && _server is not null
            ? _server.Viewing.GetWatchingNow()
            : Array.Empty<WatchingNow>();
        IReadOnlyList<KnownPlayer> players = viewing?.GetKnownPlayers() ?? Array.Empty<KnownPlayer>();
        IReadOnlyList<ContinueWatchingEntry> resume = viewing?.GetContinueWatching() ?? Array.Empty<ContinueWatchingEntry>();

        var view = new AppView(
            running,
            _settings,
            HostAddress(running),
            DescriptionUrl(running),
            library,
            watching,
            players,
            resume,
            _log,
            DateTimeOffset.Now,
            LogPathText,
            VersionText,
            _updateStatus,
            _updateAvailable,
            _updateBusy);

        _pages.Inicio.Render(view);
        _pages.Carpetas.Render(view);
        _pages.Reproductores.Render(view);
        _pages.Actividad.Render(view);
        _pages.Opciones.Render(view);

        _rail.SetCount(AppSection.Carpetas, _settings.MediaFolders.Count);
        _rail.SetCount(AppSection.Reproductores, players.Count);

        UpdateKeepAwake(running && watching.Count > 0 && _settings.KeepAwake);
        UpdateTrayText(running);
        Invalidate(new Rectangle(0, 0, ClientSize.Width, TitleHeight()));
    }

    private string HostAddress(bool running) =>
        running && _server is not null
            ? new Uri(_server.DescriptionUrl).Authority
            : $"{_localIp}:{_settings.Port}";

    private string DescriptionUrl(bool running) =>
        running && _server is not null
            ? _server.DescriptionUrl
            : $"http://{_localIp}:{_settings.Port}/description.xml";

    /// <summary>
    /// Evita la suspensión solo mientras alguien ve algo. SetThreadExecutionState actúa sobre el hilo
    /// que lo llama, por eso se llama desde la interfaz y no desde el servidor.
    /// </summary>
    private void UpdateKeepAwake(bool wanted)
    {
        if (wanted == _keepAwakeActive)
        {
            return;
        }

        PowerKeepAwake.SetEnabled(wanted);
        _keepAwakeActive = wanted;
    }

    private void UpdateTrayText(bool running)
    {
        _trayStartStopItem.Text = running ? "Detener servidor" : "Iniciar servidor";
        _notifyIcon.Text = running ? "VibeDLNA · compartiendo" : "VibeDLNA · detenido";
    }

    private async Task OnLoadedAsync()
    {
        _themeMode = ParseThemeMode(_settings.ThemeMode);
        RefreshLocalAddress();
        _idleViewing = new ViewingActivity(SettingsService.AppDataFolder);
        ApplyCurrentTheme();
        _timer.Start();
        AddLog("App lista.");

        if (!File.Exists(SettingsService.SettingsPath))
        {
            AddLog($"No hay configuración guardada en {SettingsService.AppDataFolder}: se usan valores por defecto.");
        }

        if (SettingsService.LastLoadWarning is { } warning)
        {
            ShowDialog(warning, MessageBoxIcon.Warning);
        }

        try
        {
            SettingsService.RefreshStartupRegistration(_settings);
        }
        catch (Exception ex)
        {
            AddLog($"No se pudo actualizar el inicio con Windows: {ex.Message}");
        }

        if (_settings.AutoStartServer && GetValidMediaFolders().Count > 0)
        {
            await StartServerAsync();
        }
        else if (_settings.MediaFolders.Count == 0)
        {
            AddLog("Agrega una carpeta para compartir por DLNA.");
        }
        else if (GetValidMediaFolders().Count == 0)
        {
            AddLog("Las carpetas configuradas no estan disponibles. El servidor no se inicio.");
        }

        Render();
    }

    private void HideIfStartupRequested()
    {
        if (_hasShown)
        {
            return;
        }

        _hasShown = true;
        if (_startHidden)
        {
            // Se oculta sin que se vea; la opacidad vuelve a 1 para cuando el usuario abra la ventana.
            BeginInvoke(() =>
            {
                HideToTray();
                Opacity = 1;
                ShowInTaskbar = true;
            });
        }
    }

    private async Task OnFormClosingAsync(FormClosingEventArgs e)
    {
        if (!_isExiting && _settings.MinimizeToTray && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }

        await StopServerAsync();
    }

    private void ShowFromTray()
    {
        Opacity = 1;
        ShowInTaskbar = true;
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    private void HideToTray()
    {
        Hide();
        _notifyIcon.Visible = true;
        AddLog("La app quedó en el área de notificación.");
    }

    private async Task ToggleServerAsync()
    {
        if (_serverBusy)
        {
            return;
        }

        if (_server is { IsRunning: true })
        {
            await StopServerAsync();
        }
        else
        {
            await StartServerAsync();
        }
    }

    private async Task StartServerAsync()
    {
        var validFolders = GetValidMediaFolders();
        if (validFolders.Count == 0)
        {
            ShowDialog("Agrega al menos una carpeta válida antes de iniciar el servidor.", MessageBoxIcon.Warning);
            return;
        }

        _serverBusy = true;
        try
        {
            AddLog($"Iniciando el servidor con {validFolders.Count} carpeta(s)…");
            _server?.Dispose();
            _server = new DlnaServer(
                validFolders,
                _settings.DeviceName,
                _settings.Uuid,
                _settings.Port,
                _settings.ShareVideos,
                _settings.ShareAudio,
                _settings.ShareImages,
                _settings.AutoRescanLibrary);
            _server.Message += (_, message) => BeginInvoke(() => AddLog(message));
            await _server.StartAsync();
            RefreshLocalAddress();
            SetAppIcons(active: true);
            AddLog($"Servidor iniciado en {_server.DescriptionUrl}");
            AddLog("Si Windows pregunta por permiso de red, permite el acceso en red privada.");
        }
        catch (Exception ex)
        {
            _server?.Dispose();
            _server = null;
            SetAppIcons(active: false);
            AddLog($"Error al iniciar: {ex.Message}");
            ShowDialog($"No se pudo iniciar el servidor:\n\n{ex.Message}", MessageBoxIcon.Error);
        }
        finally
        {
            _serverBusy = false;
            Render();
        }
    }

    private async Task StopServerAsync()
    {
        if (_server is null)
        {
            return;
        }

        _serverBusy = true;
        var server = _server;
        _server = null;
        try
        {
            await server.StopAsync();
        }
        finally
        {
            server.Dispose();
            _idleViewing = new ViewingActivity(SettingsService.AppDataFolder);
            SetAppIcons(active: false);
            AddLog("Servidor detenido.");
            _serverBusy = false;
            Render();
        }
    }

    private async Task RestartServerAsync()
    {
        await StopServerAsync();
        if (GetValidMediaFolders().Count > 0)
        {
            await StartServerAsync();
        }
    }

    private async Task ExitApplicationAsync()
    {
        _isExiting = true;
        await StopServerAsync();
        Close();
    }

    private void AddLog(string message)
    {
        AppLog.Write(message);
        _log.Insert(0, new ActivityEntry(DateTime.Now, ActivityLog.Classify(message), message));
        if (_log.Count > ActivityLog.MaximumEntries)
        {
            _log.RemoveAt(_log.Count - 1);
        }
    }

    private void ApplyCurrentTheme()
    {
        _palette = SystemTheme.Resolve(_themeMode);
        Ui.Palette = _palette;
        BackColor = Ui.Window;
        NativeTheme.ApplyWindowEffects(this, _palette.IsDark);
        ApplyControlTheme(this);
        ApplyTrayTheme();
        Invalidate(true);
    }

    /// <summary>Los controles nativos no se pintan solos: aquí se les dan los colores de la paleta.</summary>
    private static void ApplyControlTheme(Control control)
    {
        switch (control)
        {
            case TextBox box:
                box.BackColor = Ui.Window;
                box.ForeColor = Ui.Ink;
                break;
            case PaintedControl:
            case Form:
                break;
            default:
                control.BackColor = Ui.Window;
                control.ForeColor = Ui.Ink;
                break;
        }

        foreach (Control child in control.Controls)
        {
            ApplyControlTheme(child);
        }
    }

    private void ApplyTrayTheme()
    {
        _trayMenu.BackColor = _palette.Surface;
        _trayMenu.ForeColor = _palette.Text;
        _trayMenu.RenderMode = ToolStripRenderMode.Professional;
        _trayMenu.Renderer = new ToolStripProfessionalRenderer(new AppMenuColorTable(_palette));
        _trayMenu.ShowImageMargin = false;
        _trayMenu.ShowCheckMargin = false;
        _trayMenu.Padding = new Padding(2);
        foreach (ToolStripItem item in _trayMenu.Items)
        {
            item.BackColor = _palette.Surface;
            item.ForeColor = _palette.Text;
        }
    }

    private void SetAppIcons(bool active)
    {
        var nextWindowIcon = AppIcon.CreateIcon(64, active);
        var nextTrayIcon = AppIcon.CreateIcon(32, active);

        var oldWindowIcon = _windowIcon;
        var oldTrayIcon = _trayIcon;

        _windowIcon = nextWindowIcon;
        _trayIcon = nextTrayIcon;
        Icon = _windowIcon;

        if (_notifyIcon is not null)
        {
            _notifyIcon.Icon = _trayIcon;
        }

        oldWindowIcon?.Dispose();
        oldTrayIcon?.Dispose();
    }

    private void RefreshLocalAddress()
    {
        try
        {
            _localIp = NetworkHelper.GetLocalIPv4Address().ToString();
        }
        catch
        {
            _localIp = "127.0.0.1";
        }
    }

    private List<string> GetValidMediaFolders() =>
        _settings.MediaFolders
            .Where(folder => !string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private void ToggleTheme()
    {
        _themeMode = _palette.IsDark ? AppThemeMode.Light : AppThemeMode.Dark;
        _settings.ThemeMode = _themeMode.ToString();
        ApplyCurrentTheme();
        PersistSettings();
    }

    private static AppThemeMode ParseThemeMode(string value) =>
        Enum.TryParse<AppThemeMode>(value, ignoreCase: true, out var mode) ? mode : AppThemeMode.Dark;

    /// <summary>
    /// Muestra un aviso en producción. En modo de desarrollo no abre ventanas en la pantalla del usuario:
    /// deja la línea en el registro y sigue.
    /// </summary>
    private void ShowDialog(string text, MessageBoxIcon icon)
    {
        var line = text.Replace("\n", " ").Trim();
        if (DevMode.IsEnabled)
        {
            AddLog($"[dev, sin ventana] {line}");
            return;
        }

        AddLog(line);
        MessageBox.Show(this, text, "VibeDLNA", MessageBoxButtons.OK, icon);
    }

    private bool PersistSettings()
    {
        try
        {
            SettingsService.Save(_settings);
            return true;
        }
        catch (Exception ex)
        {
            ShowDialog($"No se pudo guardar la configuración.\n\n{ex.Message}", MessageBoxIcon.Error);
            return false;
        }
    }

    private void ApplySettings(Action<AppSettings> change, bool restartServer, string notice)
    {
        change(_settings);
        if (PersistSettings())
        {
            ShowNotice(notice);
        }

        if (restartServer && _server is { IsRunning: true })
        {
            _ = RestartServerAsync();
        }

        Render();
    }

    private void ShowNotice(string text)
    {
        var width = _toast.Show(text);
        _toast.Location = new Point(
            _contentRect.X + (_contentRect.Width - width) / 2,
            _contentRect.Bottom - Ui.S(44));
        _toast.BringToFront();
    }

    private void DrawThemeButton(Graphics graphics)
    {
        if (_hoverButton == TitleButton.Theme)
        {
            Ui.FillRounded(graphics, Ui.Hover, _themeRect, Ui.S(6));
        }

        var dark = _palette.IsDark;
        var glyph = dark ? "☀" : "☾";
        var label = dark ? "Claro" : "Oscuro";
        var glyphWidth = Ui.S(16);
        Ui.Text(
            graphics,
            glyph,
            Ui.Symbol(12.5f),
            Ui.Ink2,
            new Rectangle(_themeRect.X + Ui.S(10), _themeRect.Y, glyphWidth, _themeRect.Height),
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        Ui.Text(
            graphics,
            label,
            Ui.Sans(12.5f),
            Ui.Ink2,
            new Rectangle(_themeRect.X + Ui.S(10) + glyphWidth + Ui.S(4), _themeRect.Y, Ui.TextWidth(label, Ui.Sans(12.5f)) + Ui.S(4), _themeRect.Height),
            Ui.LeftMiddle);
    }

    private int ThemeButtonWidth() =>
        Ui.S(10) + Ui.S(16) + Ui.S(4) + Ui.TextWidth(_palette.IsDark ? "Claro" : "Oscuro", Ui.Sans(12.5f)) + Ui.S(14);

    private void DrawWindowButton(Graphics graphics, Rectangle bounds, TitleButton kind, bool hover)
    {
        var closing = kind == TitleButton.Close;
        var glyphColor = Ui.Ink2;
        if (hover)
        {
            if (closing)
            {
                using var closeBrush = new SolidBrush(Color.FromArgb(232, 17, 35));
                graphics.FillRectangle(closeBrush, bounds);
                glyphColor = Color.White;
            }
            else
            {
                using var hoverBrush = new SolidBrush(Ui.Hover);
                graphics.FillRectangle(hoverBrush, bounds);
                glyphColor = Ui.Ink;
            }
        }

        var size = Ui.S(10);
        var x = bounds.X + (bounds.Width - size) / 2;
        var y = bounds.Y + (bounds.Height - size) / 2;
        using var pen = new Pen(glyphColor, 1f);
        switch (kind)
        {
            case TitleButton.Minimize:
                graphics.DrawLine(pen, x, y + size / 2, x + size, y + size / 2);
                break;
            case TitleButton.Maximize when IsZoomed(Handle):
                graphics.DrawRectangle(pen, x + 2, y, size - 2, size - 2);
                graphics.DrawLine(pen, x, y + 2, x, y + size);
                graphics.DrawLine(pen, x, y + size, x + size - 2, y + size);
                break;
            case TitleButton.Maximize:
                graphics.DrawRectangle(pen, x, y, size, size);
                break;
            case TitleButton.Close:
                graphics.DrawLine(pen, x, y, x + size, y + size);
                graphics.DrawLine(pen, x + size, y, x, y + size);
                break;
        }
    }

    /// <summary>
    /// Bordes para redimensionar y la barra de título para arrastrar. Los botones de la barra devuelven
    /// HTCLIENT para que lleguen como clics normales.
    /// </summary>
    private int HitTest(IntPtr lParam)
    {
        var screenX = (short)(lParam.ToInt64() & 0xFFFF);
        var screenY = (short)((lParam.ToInt64() >> 16) & 0xFFFF);
        var point = PointToClient(new Point(screenX, screenY));

        if (!IsZoomed(Handle))
        {
            var border = Ui.S(6);
            var left = point.X < border;
            var right = point.X >= ClientSize.Width - border;
            var top = point.Y < border;
            var bottom = point.Y >= ClientSize.Height - border;
            if (top && left)
            {
                return HtTopLeft;
            }

            if (top && right)
            {
                return HtTopRight;
            }

            if (bottom && left)
            {
                return HtBottomLeft;
            }

            if (bottom && right)
            {
                return HtBottomRight;
            }

            if (left)
            {
                return HtLeft;
            }

            if (right)
            {
                return HtRight;
            }

            if (top)
            {
                return HtTop;
            }

            if (bottom)
            {
                return HtBottom;
            }
        }

        if (point.Y < TitleHeight() && ButtonAt(point) is null)
        {
            // Sobre la barra y fuera de los botones: arrastrar y doble clic para maximizar.
            SetHoverButton(null);
            return HtCaption;
        }

        return HtClient;
    }

    private TitleButton? ButtonAt(Point point)
    {
        if (_themeRect.Contains(point))
        {
            return TitleButton.Theme;
        }

        if (_minimizeRect.Contains(point))
        {
            return TitleButton.Minimize;
        }

        if (_maximizeRect.Contains(point))
        {
            return TitleButton.Maximize;
        }

        if (_closeRect.Contains(point))
        {
            return TitleButton.Close;
        }

        return null;
    }

    private void ActivateTitleButton(TitleButton button)
    {
        switch (button)
        {
            case TitleButton.Theme:
                ToggleTheme();
                break;
            case TitleButton.Minimize:
                WindowState = FormWindowState.Minimized;
                break;
            case TitleButton.Maximize:
                WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
                LayoutChrome();
                break;
            case TitleButton.Close:
                Close();
                break;
        }
    }

    private static int TitleHeight() => Ui.S(40);

    /// <summary>Versión visible: solo número de versión, sin el hash de commit de ProductVersion.</summary>
    private string VersionText => _updateService.CurrentVersion.ToString(3);

    /// <summary>Ruta del registro tal como la ve el usuario: %APPDATA% y la carpeta de datos activa.</summary>
    private static string LogPathText => $@"%APPDATA%\{DevMode.AppDataFolderName}\VibeDLNA.log";

    // ---- IAppActions ----

    void IAppActions.Navigate(AppSection section) => Navigate(section);

    void IAppActions.ToggleServer() => _ = ToggleServerAsync();

    void IAppActions.CopyAddress()
    {
        try
        {
            Clipboard.SetText(DescriptionUrl(_server is { IsRunning: true }));
            ShowNotice("Dirección copiada");
        }
        catch
        {
            ShowNotice("No se pudo copiar: selecciona la dirección y usa Ctrl+C");
        }
    }

    void IAppActions.Rescan()
    {
        if (_server is { IsRunning: true })
        {
            _server.Rescan();
            ShowNotice("Reescaneando");
        }
        else
        {
            ShowNotice("Inicia el servidor para reescanear");
        }

        Render();
    }

    void IAppActions.AddFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Selecciona una carpeta para compartir por DLNA",
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var folder = dialog.SelectedPath;
        if (_settings.MediaFolders.Any(existing => string.Equals(existing, folder, StringComparison.OrdinalIgnoreCase)))
        {
            ShowNotice("Esa carpeta ya se comparte");
            return;
        }

        AddLog($"Carpeta agregada: {folder}");
        ApplySettings(
            settings =>
            {
                settings.MediaFolders.Add(folder);
                settings.MediaFolder = settings.MediaFolders[0];
            },
            restartServer: true,
            notice: "Carpeta agregada");
    }

    void IAppActions.RemoveFolder(string folder)
    {
        AddLog($"Carpeta quitada: {folder}");
        ApplySettings(
            settings =>
            {
                settings.MediaFolders.RemoveAll(existing => string.Equals(existing, folder, StringComparison.OrdinalIgnoreCase));
                settings.MediaFolder = settings.MediaFolders.FirstOrDefault() ?? string.Empty;
            },
            restartServer: true,
            notice: "Carpeta quitada");
    }

    void IAppActions.ChangeSettings(Action<AppSettings> change, bool restartServer) =>
        ApplySettings(change, restartServer, "Guardado");

    void IAppActions.CheckForUpdates() => CheckForUpdates();

    void IAppActions.InstallUpdate() => _ = InstallUpdateAsync();

    void IAppActions.Notify(string text) => ShowNotice(text);

    private void CheckForUpdates() => _ = CheckForUpdatesAsync();

    private async Task CheckForUpdatesAsync()
    {
        if (_updateBusy)
        {
            return;
        }

        _updateBusy = true;
        _updateStatus = "Comprobando actualizaciones en GitHub…";
        Render();
        try
        {
            var result = await _updateService.CheckAsync(CancellationToken.None);
            _lastCheck = DateTime.Now;
            _latestRelease = result.Release;
            _updateAvailable = result.State == UpdateCheckState.Available;
            _updateStatus = BuildUpdateStatus(result);
        }
        catch
        {
            _updateStatus = "No se pudo consultar GitHub en este momento.";
        }
        finally
        {
            _updateBusy = false;
            Render();
        }
    }

    private string BuildUpdateStatus(UpdateCheckResult result)
    {
        var when = _lastCheck is { } checkedAt ? $"Revisado hoy a las {checkedAt:HH:mm}" : "Sin revisar";
        var phrase = result.State switch
        {
            UpdateCheckState.UpToDate => "estás al día",
            UpdateCheckState.Available when result.Release is { } release => $"hay v{release.Version} disponible",
            _ => result.Message
        };
        return $"{when} · {phrase}";
    }

    private async Task InstallUpdateAsync()
    {
        if (_updateBusy || _latestRelease is null)
        {
            return;
        }

        _updateBusy = true;
        _updateStatus = $"Descargando v{_latestRelease.Version} desde GitHub…";
        Render();
        try
        {
            var result = await _updateService.DownloadAndStartAsync(_latestRelease, CancellationToken.None);
            _updateStatus = result.Message;
            if (result.Started)
            {
                await ExitApplicationAsync();
                return;
            }
        }
        catch
        {
            _updateStatus = "No se pudo preparar la actualización.";
        }
        finally
        {
            _updateBusy = false;
            Render();
        }
    }

    /// <summary>Primer RECT de NCCALCSIZE_PARAMS.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    [DllImport("user32.dll")]
    private static extern bool IsZoomed(IntPtr hWnd);

    private sealed class AppMenuColorTable : ProfessionalColorTable
    {
        private readonly AppPalette _palette;

        public AppMenuColorTable(AppPalette palette)
        {
            _palette = palette;
            UseSystemColors = false;
        }

        public override Color MenuStripGradientBegin => _palette.Window;
        public override Color MenuStripGradientEnd => _palette.Window;
        public override Color ToolStripDropDownBackground => _palette.Surface;
        public override Color ImageMarginGradientBegin => _palette.Surface;
        public override Color ImageMarginGradientMiddle => _palette.Surface;
        public override Color ImageMarginGradientEnd => _palette.Surface;
        public override Color MenuItemSelected => _palette.Elevated;
        public override Color MenuItemSelectedGradientBegin => _palette.Elevated;
        public override Color MenuItemSelectedGradientEnd => _palette.Elevated;
        public override Color MenuItemPressedGradientBegin => _palette.SurfaceAlt;
        public override Color MenuItemPressedGradientEnd => _palette.SurfaceAlt;
        public override Color MenuItemBorder => _palette.Border;
        public override Color SeparatorDark => _palette.Border;
        public override Color SeparatorLight => _palette.Border;
        public override Color CheckBackground => _palette.SurfaceAlt;
        public override Color CheckSelectedBackground => _palette.Elevated;
        public override Color CheckPressedBackground => _palette.Elevated;
    }
}
