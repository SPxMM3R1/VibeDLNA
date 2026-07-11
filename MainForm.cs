namespace FolderDlnaServer;

internal sealed class MainForm : Form
{
    private const int WmSettingChange = 0x001A;

    private readonly bool _requestedStartMinimized;
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _trayMenu;
    private readonly ToolStripMenuItem _trayStartStopItem;
    private readonly ToolStripMenuItem _trayShowItem;
    private readonly ToolStripMenuItem _trayExitItem;

    private readonly TableLayoutPanel _root = new();
    private readonly Label _footerLabel = new();
    private readonly TextBox _folderTextBox = new();
    private readonly TextBox _deviceNameTextBox = new();
    private readonly Label _themeGlyphLabel = new();
    private readonly Label _optionsMenuLabel = new();
    private readonly ToolTip _toolTip = new();
    private readonly Label _startCommandLabel = new();
    private readonly Label _stopCommandLabel = new();
    private readonly Label _saveCommandLabel = new();
    private readonly DataGridView _statusGrid = new();
    private readonly DataGridView _logGrid = new();
    private readonly Label _sidebarServerLabel = new();
    private readonly Label _sidebarFolderLabel = new();
    private readonly Label _sidebarWindowsLabel = new();
    private readonly Label _bottomAddressLabel = new();
    private readonly Label _bottomStateLabel = new();

    private AppSettings _settings = new();
    private AppPalette _palette = AppPalette.Dark;
    private AppThemeMode _themeMode = AppThemeMode.Dark;
    private DlnaServer? _server;
    private Icon? _windowIcon;
    private Icon? _trayIcon;
    private bool _isExiting;
    private bool _hasShown;
    private bool _isApplyingSettings;
    private bool _startCommandAvailable = true;
    private bool _stopCommandAvailable;
    private bool _saveCommandAvailable = true;

    public MainForm(bool requestedStartMinimized)
    {
        _requestedStartMinimized = requestedStartMinimized;

        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 9.5f);
        Text = "VibeDLNA";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(960, 620);
        Size = new Size(1180, 720);

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
            Text = "VibeDLNA",
            ContextMenuStrip = _trayMenu,
            Visible = true
        };
        _notifyIcon.DoubleClick += (_, _) => ShowFromTray();

        BuildUi();
        HandleCreated += (_, _) => NativeTheme.ApplyWindowEffects(this, _palette.IsDark);
        Load += async (_, _) => await OnLoadedAsync();
        Shown += (_, _) => HideIfStartupRequested();
        FormClosing += async (_, e) => await OnFormClosingAsync(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _notifyIcon.Dispose();
            _trayMenu.Dispose();
            _toolTip.Dispose();
            _server?.Dispose();
            _windowIcon?.Dispose();
            _trayIcon?.Dispose();
        }

        base.Dispose(disposing);
    }

    protected override void WndProc(ref Message message)
    {
        base.WndProc(ref message);

        if (message.Msg == WmSettingChange && _themeMode == AppThemeMode.System)
        {
            BeginInvoke(ApplyCurrentTheme);
        }
    }

    private void BuildUi()
    {
        _root.Dock = DockStyle.Fill;
        _root.ColumnCount = 1;
        _root.RowCount = 4;
        _root.Padding = new Padding(0);
        _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        _root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        Controls.Add(_root);

        BuildOptionsMenu();
        BuildFlatToolbar();
        BuildFlatWorkspace();
        BuildFlatStatusBar();
    }

    private void BuildOptionsMenu()
    {
        var host = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(12, 2, 0, 0)
        };
        ConfigureMenuLabel(_optionsMenuLabel, "Opciones", OpenOptionsDialog);
        host.Controls.Add(_optionsMenuLabel);
        _root.Controls.Add(host, 0, 0);
    }

    private void BuildFlatToolbar()
    {
        var toolbar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            Padding = new Padding(12, 8, 12, 8)
        };
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 158));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 410));
        _root.Controls.Add(toolbar, 0, 1);

        var left = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        toolbar.Controls.Add(left, 0, 0);

        ConfigureCommandLabel(_startCommandLabel, "▶", "Iniciar servidor", async () => await StartServerAsync());
        ConfigureCommandLabel(_stopCommandLabel, "■", "Detener servidor", async () => await StopServerAsync());
        ConfigureCommandLabel(_saveCommandLabel, "✓", "Guardar opciones", () => SaveSettingsFromUi(showConfirmation: true));
        left.Controls.AddRange(new Control[] { _startCommandLabel, _stopCommandLabel, _saveCommandLabel });

        ConfigureTextBox(_folderTextBox);
        _folderTextBox.ReadOnly = true;
        _folderTextBox.PlaceholderText = "Selecciona la carpeta para compartir...";
        _folderTextBox.Cursor = Cursors.Hand;
        _folderTextBox.Margin = new Padding(22, 4, 22, 4);
        _folderTextBox.TextAlign = HorizontalAlignment.Center;
        _folderTextBox.Click += (_, _) => SelectFolder();
        _toolTip.SetToolTip(_folderTextBox, "Click para seleccionar la carpeta compartida");
        toolbar.Controls.Add(_folderTextBox, 1, 0);

        var right = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1
        };
        right.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        right.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 12));
        right.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 50));
        toolbar.Controls.Add(right, 2, 0);

        ConfigureTextBox(_deviceNameTextBox);
        _deviceNameTextBox.Margin = new Padding(0, 4, 0, 4);
        right.Controls.Add(_deviceNameTextBox, 0, 0);

        ConfigureThemeGlyph();
        right.Controls.Add(_themeGlyphLabel, 2, 0);
    }

    private void BuildFlatWorkspace()
    {
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            FixedPanel = FixedPanel.Panel1,
            SplitterWidth = 1,
            SplitterDistance = 190,
            BackColor = AppPalette.Dark.Border
        };
        _root.Controls.Add(split, 0, 2);

        BuildFlatSidebar(split.Panel1);
        BuildFlatMain(split.Panel2);
    }

    private void BuildFlatSidebar(Control host)
    {
        var sidebar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(10, 8, 8, 8)
        };
        host.Controls.Add(sidebar);
        sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        sidebar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        sidebar.Controls.Add(CreateSidebarHeader("ESTADO"), 0, 0);
        ConfigureLabel(_sidebarServerLabel, "Servidor detenido", 9f, FontStyle.Regular);
        ConfigureLabel(_sidebarFolderLabel, "Biblioteca sin revisar", 9f, FontStyle.Regular);
        ConfigureLabel(_sidebarWindowsLabel, "Inicio con Windows: no", 9f, FontStyle.Regular);
        sidebar.Controls.Add(_sidebarServerLabel, 0, 1);
        sidebar.Controls.Add(_sidebarFolderLabel, 0, 2);
        sidebar.Controls.Add(_sidebarWindowsLabel, 0, 3);
    }

    private void BuildFlatMain(Control host)
    {
        var main = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(0, 0, 0, 0)
        };
        main.RowStyles.Add(new RowStyle(SizeType.Percent, 58));
        main.RowStyles.Add(new RowStyle(SizeType.Percent, 42));
        host.Controls.Add(main);

        ConfigureGrid(_statusGrid);
        _statusGrid.Columns.Add("name", "Nombre");
        _statusGrid.Columns.Add("state", "Estado");
        _statusGrid.Columns.Add("detail", "Detalle");
        _statusGrid.Columns[0].Width = 190;
        _statusGrid.Columns[1].Width = 150;
        _statusGrid.Columns[2].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        main.Controls.Add(_statusGrid, 0, 0);

        ConfigureGrid(_logGrid);
        _logGrid.Columns.Add("time", "Hora");
        _logGrid.Columns.Add("message", "Actividad");
        _logGrid.Columns[0].Width = 84;
        _logGrid.Columns[1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        main.Controls.Add(_logGrid, 0, 1);
    }

    private void BuildFlatStatusBar()
    {
        var status = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            Padding = new Padding(10, 4, 10, 4)
        };
        status.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        status.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 360));
        status.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        _root.Controls.Add(status, 0, 3);

        ConfigureLabel(_footerLabel, "VibeDLNA", 9f, FontStyle.Regular, muted: true);
        ConfigureLabel(_bottomAddressLabel, "Sin direccion activa", 9f, FontStyle.Regular, muted: true);
        ConfigureLabel(_bottomStateLabel, "DLNA detenido", 9f, FontStyle.Bold);
        _bottomAddressLabel.TextAlign = ContentAlignment.MiddleRight;
        _bottomStateLabel.TextAlign = ContentAlignment.MiddleRight;
        status.Controls.Add(_footerLabel, 0, 0);
        status.Controls.Add(_bottomAddressLabel, 1, 0);
        status.Controls.Add(_bottomStateLabel, 2, 0);
    }
    private async Task OnLoadedAsync()
    {
        _settings = SettingsService.Load();
        ApplySettingsToUi();
        ApplyCurrentTheme();
        Log("App lista.");

        if (SettingsService.LastLoadWarning is { } warning)
        {
            Log(warning);
            MessageBox.Show(this, warning, "VibeDLNA", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        try
        {
            SettingsService.RefreshStartupRegistration(_settings);
        }
        catch (Exception ex)
        {
            Log($"No se pudo actualizar el inicio con Windows: {ex.Message}");
        }

        if (_settings.AutoStartServer && GetValidMediaFolders().Count > 0)
        {
            await StartServerAsync();
        }
        else if (_settings.MediaFolders.Count == 0)
        {
            Log("Selecciona una carpeta para compartir por DLNA.");
        }
        else if (GetValidMediaFolders().Count == 0)
        {
            Log("Las carpetas configuradas no estan disponibles. El servidor no se inicio.");
        }
    }

    private void HideIfStartupRequested()
    {
        if (_hasShown)
        {
            return;
        }

        _hasShown = true;
        if (_requestedStartMinimized && _settings.StartMinimized && _settings.MinimizeToTray)
        {
            BeginInvoke(HideToTray);
        }
    }

    private async Task OnFormClosingAsync(FormClosingEventArgs e)
    {
        SaveSettingsFromUi(showConfirmation: false);

        if (!_isExiting && _settings.MinimizeToTray && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }

        await StopServerAsync();
    }

    private void ApplySettingsToUi()
    {
        _isApplyingSettings = true;
        _folderTextBox.Text = GetFolderSummary();
        _deviceNameTextBox.Text = _settings.DeviceName;
        SelectThemeMode(ParseThemeMode(_settings.ThemeMode));
        RefreshStatusView();
        _isApplyingSettings = false;
    }

    private void SaveSettingsFromUi(bool showConfirmation)
    {
        _settings.DeviceName = _deviceNameTextBox.Text.Trim();
        _settings.ThemeMode = _themeMode.ToString();

        var saved = TryPersistSettings();
        _folderTextBox.Text = GetFolderSummary();
        RefreshStatusView();
        if (showConfirmation && saved)
        {
            Log("Opciones guardadas.");
        }
    }

    private void SelectFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Selecciona la carpeta que quieres compartir por DLNA",
            UseDescriptionForTitle = true,
            SelectedPath = Directory.Exists(_settings.MediaFolder) ? _settings.MediaFolder : Environment.GetFolderPath(Environment.SpecialFolder.MyVideos)
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _settings.MediaFolders = new List<string> { dialog.SelectedPath };
            _settings.MediaFolder = dialog.SelectedPath;
            _folderTextBox.Text = GetFolderSummary();
            if (string.IsNullOrWhiteSpace(_deviceNameTextBox.Text))
            {
                _deviceNameTextBox.Text = $"DLNA - {Path.GetFileName(dialog.SelectedPath)}";
            }

            SaveSettingsFromUi(showConfirmation: false);
            RefreshStatusView();
            Log($"Carpeta seleccionada: {dialog.SelectedPath}");
        }
    }

    private async Task ToggleServerAsync()
    {
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
        SaveSettingsFromUi(showConfirmation: false);

        var validFolders = GetValidMediaFolders();
        if (validFolders.Count == 0)
        {
            MessageBox.Show(
                "Selecciona al menos una carpeta valida antes de iniciar el servidor.",
                "VibeDLNA",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        SetBusy(true);
        try
        {
            _server?.Dispose();
            _server = new DlnaServer(
                validFolders,
                _settings.DeviceName,
                _settings.Uuid,
                _settings.Port,
                _settings.ShareVideos,
                _settings.ShareAudio,
                _settings.ShareImages,
                _settings.AutoRescanLibrary,
                _settings.KeepAwake);
            _server.Message += (_, message) => BeginInvoke(() => Log(message));
            await _server.StartAsync();

            _trayStartStopItem.Text = "Detener servidor";
            _notifyIcon.Text = "VibeDLNA - activo";
            SetAppIcons(active: true);
            RefreshStatusView();
            RefreshCommandState();
            Log($"Servidor iniciado en {_server.DescriptionUrl}");
            Log("Si Windows pregunta por permiso de red, permite el acceso en red privada.");
        }
        catch (Exception ex)
        {
            _server?.Dispose();
            _server = null;
            SetAppIcons(active: false);
            RefreshStatusView();
            RefreshCommandState();
            Log($"Error al iniciar: {ex.Message}");
            MessageBox.Show(
                $"No se pudo iniciar el servidor:\n\n{ex.Message}",
                "VibeDLNA",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task StopServerAsync()
    {
        if (_server is null)
        {
            return;
        }

        SetBusy(true);
        try
        {
            await _server.StopAsync();
            _server.Dispose();
            _server = null;
            _trayStartStopItem.Text = "Iniciar servidor";
            _notifyIcon.Text = "VibeDLNA";
            SetAppIcons(active: false);
            RefreshStatusView();
            RefreshCommandState();
            Log("Servidor detenido.");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        _trayStartStopItem.Enabled = !busy;
        _folderTextBox.Enabled = !busy;
        _optionsMenuLabel.Enabled = !busy;
        _saveCommandAvailable = !busy;
        _startCommandAvailable = !busy && _server is not { IsRunning: true };
        _stopCommandAvailable = !busy && _server is { IsRunning: true };
        RefreshCommandColors();
    }

    private void ShowFromTray()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    private void HideToTray()
    {
        Hide();
        _notifyIcon.Visible = true;
        Log("La app quedo en el area de notificacion.");
    }

    private void OpenOptionsDialog()
    {
        using var dialog = new OptionsDialog(_settings, _palette, RescanLibrary);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var wasRunning = _server is { IsRunning: true };
        _settings = dialog.Settings;
        ApplySettingsToUi();
        TryPersistSettings();
        _folderTextBox.Text = GetFolderSummary();
        RefreshStatusView();
        Log("Opciones actualizadas.");

        if (wasRunning)
        {
            BeginInvoke(async () =>
            {
                await StopServerAsync();
                await StartServerAsync();
            });
        }
    }

    private void RescanLibrary()
    {
        if (_server is { IsRunning: true })
        {
            _server.Rescan();
            Log("Reescaneo solicitado.");
        }
        else
        {
            Log("Reescaneo pendiente: inicia el servidor para anunciar la biblioteca.");
        }
    }

    private async Task ExitApplicationAsync()
    {
        _isExiting = true;
        SaveSettingsFromUi(showConfirmation: false);
        await StopServerAsync();
        Close();
    }

    private void Log(string message)
    {
        AppLog.Write(message);
        if (_logGrid.Columns.Count > 0)
        {
            _logGrid.Rows.Insert(0, DateTime.Now.ToString("HH:mm:ss"), message);
            if (_logGrid.Rows.Count > 250)
            {
                _logGrid.Rows.RemoveAt(_logGrid.Rows.Count - 1);
            }
        }
    }

    private void ToggleTheme()
    {
        if (_isApplyingSettings)
        {
            return;
        }

        _themeMode = _palette.IsDark ? AppThemeMode.Light : AppThemeMode.Dark;
        _settings.ThemeMode = _themeMode.ToString();
        ApplyCurrentTheme();
        SaveSettingsFromUi(showConfirmation: false);
    }

    private void ApplyCurrentTheme()
    {
        _palette = SystemTheme.Resolve(_themeMode);
        BackColor = _palette.Window;
        ForeColor = _palette.Text;
        NativeTheme.ApplyWindowEffects(this, _palette.IsDark);
        ApplyPaletteRecursive(this);
        ApplyTrayTheme();
        RefreshThemeGlyph();
        RefreshStatusView();
    }

    private void ApplyPaletteRecursive(Control control)
    {
        if (control is IThemeAware themeAware)
        {
            themeAware.ApplyPalette(_palette);
        }
        else if (control is LinkLabel linkLabel)
        {
            linkLabel.BackColor = Color.Transparent;
            linkLabel.ForeColor = _palette.MutedText;
            linkLabel.LinkColor = _palette.AccentAlt;
            linkLabel.ActiveLinkColor = _palette.Accent;
            linkLabel.VisitedLinkColor = _palette.AccentAlt;
        }
        else if (control is TextBox textBox)
        {
            textBox.BackColor = _palette.Elevated;
            textBox.ForeColor = _palette.Text;
        }
        else if (control is DataGridView grid)
        {
            ApplyGridPalette(grid);
        }
        else if (control is MenuStrip menu)
        {
            ApplyMenuPalette(menu);
        }
        else if (control is Label label)
        {
            label.BackColor = Color.Transparent;
            label.ForeColor = Equals(label.Tag, "muted") ? _palette.MutedText : _palette.Text;
        }
        else if (control is TableLayoutPanel or FlowLayoutPanel)
        {
            control.BackColor = control == _root ? _palette.Window : ThemePaint.ResolveBackColor(control, _palette);
            control.ForeColor = _palette.Text;
        }
        else
        {
            control.BackColor = control == this || control == _root ? _palette.Window : ThemePaint.ResolveBackColor(control, _palette);
            control.ForeColor = _palette.Text;
        }

        foreach (Control child in control.Controls)
        {
            ApplyPaletteRecursive(child);
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

    private void ApplyMenuPalette(MenuStrip menu)
    {
        menu.BackColor = _palette.Window;
        menu.ForeColor = _palette.Text;
        menu.RenderMode = ToolStripRenderMode.Professional;
        menu.Renderer = new ToolStripProfessionalRenderer(new AppMenuColorTable(_palette));

        foreach (ToolStripItem item in menu.Items)
        {
            ApplyToolStripItemPalette(item);
        }
    }

    private void ApplyToolStripItemPalette(ToolStripItem item)
    {
        item.BackColor = _palette.Window;
        item.ForeColor = _palette.Text;

        if (item is ToolStripMenuItem menuItem)
        {
            menuItem.DropDown.BackColor = _palette.Surface;
            menuItem.DropDown.ForeColor = _palette.Text;
            foreach (ToolStripItem child in menuItem.DropDownItems)
            {
                child.BackColor = _palette.Surface;
                child.ForeColor = _palette.Text;
                ApplyToolStripItemPalette(child);
            }
        }
    }

    private void RefreshStatusView()
    {
        var running = _server is { IsRunning: true };
        var validFolders = GetValidMediaFolders();
        var folderExists = validFolders.Count > 0;
        _sidebarServerLabel.Text = running ? "Servidor activo (1)" : "Servidor detenido (0)";
        _sidebarFolderLabel.Text = folderExists ? $"Biblioteca lista ({validFolders.Count})" : "Biblioteca pendiente (0)";
        _sidebarWindowsLabel.Text = _settings.StartWithWindows ? "Inicio Win: si" : "Inicio Win: no";

        _bottomStateLabel.Text = running ? "DLNA activo" : "DLNA detenido";
        _bottomAddressLabel.Text = running && _server is not null ? _server.DescriptionUrl : "Sin direccion activa";
        if (_statusGrid.Columns.Count == 0)
        {
            return;
        }

        _statusGrid.Rows.Clear();
        _statusGrid.Rows.Add("Servidor DLNA", running ? "Activo" : "Detenido", running ? "Anunciando por UPnP en la red local" : "Listo para iniciar");
        _statusGrid.Rows.Add("Biblioteca", folderExists ? "Lista" : "Pendiente", GetFolderSummary());
        _statusGrid.Rows.Add("Nombre visible", "Configurado", _settings.DeviceName);
        _statusGrid.Rows.Add("Direccion", running ? "Disponible" : "No disponible", running && _server is not null ? _server.DescriptionUrl : "Sin direccion activa");
        _statusGrid.Rows.Add("Tipos", "Filtro", GetMediaFilterSummary());
        _statusGrid.Rows.Add("Reescaneo", _settings.AutoRescanLibrary ? "Automatico" : "Manual", _settings.AutoRescanLibrary ? "Detecta cambios en carpetas" : "Usa Opciones > Reescanear");
        _statusGrid.Rows.Add("Energia", _settings.KeepAwake ? "Activo" : "Normal", _settings.KeepAwake ? "Evita suspension con DLNA activo" : "Windows decide suspension");
        _statusGrid.Rows.Add("Inicio con Windows", _settings.StartWithWindows ? "Activado" : "Desactivado", _settings.StartMinimized ? "Abrira minimizada" : "Abrira visible");
        _statusGrid.Rows.Add("Bandeja", _settings.MinimizeToTray ? "Activada" : "Desactivada", _settings.MinimizeToTray ? "Cerrar envia al area de notificacion" : "Cerrar sale de la app");
        RefreshCommandState();
    }

    private List<string> GetValidMediaFolders() =>
        _settings.MediaFolders
            .Where(Directory.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private string GetFolderSummary()
    {
        var configured = _settings.MediaFolders
            .Where(folder => !string.IsNullOrWhiteSpace(folder))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var available = configured.Where(Directory.Exists).ToList();
        if (configured.Count == 0)
        {
            return "Sin carpeta seleccionada";
        }

        if (available.Count == 0)
        {
            return configured.Count == 1
                ? $"{configured[0]} (no disponible)"
                : $"{configured.Count} carpetas no disponibles";
        }

        return available.Count switch
        {
            1 when configured.Count == 1 => available[0],
            _ => $"{available.Count} de {configured.Count} carpetas disponibles"
        };
    }

    private bool TryPersistSettings()
    {
        try
        {
            SettingsService.Save(_settings);
            return true;
        }
        catch (Exception ex)
        {
            Log($"No se pudo guardar la configuracion: {ex.Message}");
            MessageBox.Show(
                this,
                $"No se pudo guardar la configuracion.\n\n{ex.Message}",
                "VibeDLNA",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return false;
        }
    }

    private string GetMediaFilterSummary()
    {
        var enabled = new List<string>();
        if (_settings.ShareVideos)
        {
            enabled.Add("videos");
        }

        if (_settings.ShareAudio)
        {
            enabled.Add("audio");
        }

        if (_settings.ShareImages)
        {
            enabled.Add("fotos");
        }

        return string.Join(", ", enabled);
    }

    private void RefreshCommandState()
    {
        var running = _server is { IsRunning: true };
        _startCommandAvailable = !running;
        _stopCommandAvailable = running;
        _saveCommandAvailable = true;
        RefreshCommandColors();
    }

    private void RefreshCommandColors()
    {
        _startCommandLabel.ForeColor = _startCommandAvailable ? _palette.Success : _palette.MutedText;
        _stopCommandLabel.ForeColor = _stopCommandAvailable ? _palette.Danger : _palette.MutedText;
        _saveCommandLabel.ForeColor = _saveCommandAvailable ? _palette.Text : _palette.MutedText;
        _startCommandLabel.Cursor = _startCommandAvailable ? Cursors.Hand : Cursors.Default;
        _stopCommandLabel.Cursor = _stopCommandAvailable ? Cursors.Hand : Cursors.Default;
        _saveCommandLabel.Cursor = _saveCommandAvailable ? Cursors.Hand : Cursors.Default;
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

    private void SelectThemeMode(AppThemeMode mode)
    {
        _themeMode = mode;

        if (mode == AppThemeMode.System)
        {
            RefreshThemeGlyph(SystemTheme.Resolve(AppThemeMode.System).IsDark);
            return;
        }

        RefreshThemeGlyph(mode == AppThemeMode.Dark);
    }

    private AppThemeMode GetSelectedThemeMode() =>
        _themeMode;

    private static AppThemeMode ParseThemeMode(string value) =>
        Enum.TryParse<AppThemeMode>(value, ignoreCase: true, out var mode) ? mode : AppThemeMode.System;

    private static void ConfigureTextBox(TextBox textBox)
    {
        textBox.Dock = DockStyle.Fill;
        textBox.BorderStyle = BorderStyle.FixedSingle;
        textBox.Font = new Font("Segoe UI", 10f);
    }

    private void ConfigureCommandLabel(Label label, string text, string tooltip, Action action)
    {
        label.Text = text;
        label.AutoSize = false;
        label.Size = new Size(38, 42);
        label.Margin = new Padding(0, 0, 8, 0);
        label.BackColor = Color.Transparent;
        label.TextAlign = ContentAlignment.MiddleCenter;
        label.Font = new Font("Segoe UI Symbol", 20f, FontStyle.Regular);
        label.Cursor = Cursors.Hand;
        label.Click += (_, _) =>
        {
            if (IsCommandAvailable(label))
            {
                action();
            }
        };
        _toolTip.SetToolTip(label, tooltip);
    }

    private void ConfigureCommandLabel(Label label, string text, string tooltip, Func<Task> action)
    {
        ConfigureCommandLabel(label, text, tooltip, () =>
        {
            _ = action();
        });
    }

    private void ConfigureThemeGlyph()
    {
        _themeGlyphLabel.Text = "☾";
        _themeGlyphLabel.AutoSize = false;
        _themeGlyphLabel.Size = new Size(50, 42);
        _themeGlyphLabel.Margin = new Padding(0);
        _themeGlyphLabel.Dock = DockStyle.Fill;
        _themeGlyphLabel.BackColor = Color.Transparent;
        _themeGlyphLabel.TextAlign = ContentAlignment.MiddleCenter;
        _themeGlyphLabel.Font = new Font("Segoe UI Symbol", 20f, FontStyle.Regular);
        _themeGlyphLabel.Cursor = Cursors.Hand;
        _themeGlyphLabel.Click += (_, _) => ToggleTheme();
        RefreshThemeGlyph();
    }

    private void ConfigureMenuLabel(Label label, string text, Action action)
    {
        label.Text = text;
        label.AutoSize = false;
        label.Size = new Size(82, 28);
        label.Margin = new Padding(0);
        label.BackColor = Color.Transparent;
        label.TextAlign = ContentAlignment.MiddleLeft;
        label.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
        label.Cursor = Cursors.Hand;
        label.MouseDown += (_, _) =>
        {
            label.BackColor = _palette.SurfaceAlt;
            label.Invalidate();
        };
        label.MouseUp += (_, _) =>
        {
            label.BackColor = Color.Transparent;
            label.Invalidate();
        };
        label.MouseLeave += (_, _) =>
        {
            label.BackColor = Color.Transparent;
            label.Invalidate();
        };
        label.Click += (_, _) =>
        {
            if (label.Enabled)
            {
                action();
            }
        };
    }

    private void RefreshThemeGlyph(bool? darkOverride = null)
    {
        var isDark = darkOverride ?? _palette.IsDark;
        _themeGlyphLabel.Text = isDark ? "☾" : "☀";
        _themeGlyphLabel.ForeColor = isDark
            ? Color.FromArgb(218, 232, 255)
            : Color.FromArgb(255, 184, 76);
        _toolTip.SetToolTip(_themeGlyphLabel, isDark ? "Cambiar a modo claro" : "Cambiar a modo oscuro");
    }

    private bool IsCommandAvailable(Label label)
    {
        if (ReferenceEquals(label, _startCommandLabel))
        {
            return _startCommandAvailable;
        }

        if (ReferenceEquals(label, _stopCommandLabel))
        {
            return _stopCommandAvailable;
        }

        if (ReferenceEquals(label, _saveCommandLabel))
        {
            return _saveCommandAvailable;
        }

        return false;
    }

    private void ConfigureGrid(DataGridView grid)
    {
        grid.Dock = DockStyle.Fill;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.AllowUserToResizeRows = false;
        grid.ReadOnly = true;
        grid.RowHeadersVisible = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.MultiSelect = false;
        grid.BorderStyle = BorderStyle.None;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        grid.EnableHeadersVisualStyles = false;
        grid.BackgroundColor = AppPalette.Dark.Window;
        grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
        grid.RowTemplate.Height = 26;
        ApplyGridPalette(grid);
    }

    private void ApplyGridPalette(DataGridView grid)
    {
        grid.BackgroundColor = _palette.Window;
        grid.GridColor = _palette.IsDark
            ? Color.FromArgb(34, 42, 55)
            : Color.FromArgb(210, 221, 232);
        grid.DefaultCellStyle.BackColor = _palette.Window;
        grid.DefaultCellStyle.ForeColor = _palette.Text;
        grid.DefaultCellStyle.SelectionBackColor = _palette.SurfaceAlt;
        grid.DefaultCellStyle.SelectionForeColor = _palette.Text;
        grid.AlternatingRowsDefaultCellStyle.BackColor = _palette.IsDark ? Color.FromArgb(13, 18, 27) : Color.FromArgb(235, 241, 247);
        grid.ColumnHeadersDefaultCellStyle.BackColor = _palette.SurfaceAlt;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = _palette.MutedText;
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = _palette.SurfaceAlt;
        grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = _palette.Text;
    }

    private static Label CreateSidebarHeader(string text)
    {
        var label = new Label();
        ConfigureLabel(label, text, 8.5f, FontStyle.Bold);
        label.Dock = DockStyle.Fill;
        label.TextAlign = ContentAlignment.MiddleLeft;
        return label;
    }

    private static void ConfigureLabel(Label label, string text, float size, FontStyle style, bool muted = false)
    {
        label.Text = text;
        label.AutoSize = false;
        label.Font = new Font("Segoe UI", size, style);
        label.TextAlign = ContentAlignment.MiddleLeft;
        label.Tag = muted ? "muted" : null;
    }

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
