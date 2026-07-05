using System.Diagnostics;

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
    private readonly LogoMark _logoMark = new();
    private readonly StatusPill _statusPill = new();
    private readonly Label _titleLabel = new();
    private readonly Label _subtitleLabel = new();
    private readonly Label _networkLabel = new();
    private readonly Label _folderStateLabel = new();
    private readonly Label _deviceStateLabel = new();
    private readonly Label _footerLabel = new();
    private readonly TextBox _folderTextBox = new();
    private readonly TextBox _deviceNameTextBox = new();
    private readonly TextBox _logTextBox = new();
    private readonly LinkLabel _serverLink = new();
    private readonly ThemeIconButton _themeButton = new();
    private readonly ToolTip _toolTip = new();
    private readonly ToggleSwitch _autoStartServerSwitch = new();
    private readonly ToggleSwitch _startWithWindowsSwitch = new();
    private readonly ToggleSwitch _startMinimizedSwitch = new();
    private readonly ToggleSwitch _minimizeToTraySwitch = new();
    private readonly ModernButton _browseButton = new();
    private readonly ModernButton _startStopButton = new();
    private readonly ModernButton _saveButton = new();

    private AppSettings _settings = new();
    private AppPalette _palette = AppPalette.Dark;
    private AppThemeMode _themeMode = AppThemeMode.Dark;
    private DlnaServer? _server;
    private Icon? _windowIcon;
    private Icon? _trayIcon;
    private bool _isExiting;
    private bool _hasShown;
    private bool _isApplyingSettings;

    public MainForm(bool requestedStartMinimized)
    {
        _requestedStartMinimized = requestedStartMinimized;

        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 9.5f);
        Text = "Folder DLNA";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(820, 620);
        Size = new Size(930, 700);

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
            Text = "Folder DLNA",
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
        _root.Padding = new Padding(22);
        _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 118));
        _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 242));
        _root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        Controls.Add(_root);

        BuildHeader();
        BuildMainCards();
        BuildLogCard();
        BuildFooter();
    }

    private void BuildHeader()
    {
        var header = new ModernCard
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18),
            Margin = new Padding(0, 0, 0, 14),
            AccentColor = AppPalette.Dark.Accent
        };
        _root.Controls.Add(header, 0, 0);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Color.Transparent
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 86));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230));
        header.Controls.Add(layout);

        _logoMark.Dock = DockStyle.Fill;
        layout.Controls.Add(_logoMark, 0, 0);

        var titleStack = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Color.Transparent
        };
        titleStack.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        titleStack.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        titleStack.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(titleStack, 1, 0);

        ConfigureLabel(_titleLabel, "Folder DLNA", 22f, FontStyle.Bold);
        _titleLabel.Dock = DockStyle.Fill;
        titleStack.Controls.Add(_titleLabel, 0, 0);

        ConfigureLabel(_subtitleLabel, "Servidor de medios para tu red local", 9.5f, FontStyle.Regular, muted: true);
        _subtitleLabel.Dock = DockStyle.Fill;
        titleStack.Controls.Add(_subtitleLabel, 0, 1);

        ConfigureLabel(_networkLabel, "Selecciona una carpeta para activar el servidor", 9f, FontStyle.Regular, muted: true);
        _networkLabel.Dock = DockStyle.Fill;
        _networkLabel.TextAlign = ContentAlignment.BottomLeft;
        titleStack.Controls.Add(_networkLabel, 0, 2);

        var rightStack = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Color.Transparent
        };
        rightStack.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        rightStack.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        rightStack.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(rightStack, 2, 0);

        _statusPill.Text = "DETENIDO";
        _statusPill.Kind = StatusKind.Offline;
        _statusPill.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        rightStack.Controls.Add(_statusPill, 0, 0);

        var themeRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = Color.Transparent
        };
        rightStack.Controls.Add(themeRow, 0, 1);

        _themeButton.Margin = new Padding(0, 0, 0, 0);
        _themeButton.Click += (_, _) => ToggleTheme();
        _toolTip.SetToolTip(_themeButton, "Cambiar tema");
        themeRow.Controls.Add(_themeButton);

        _serverLink.Text = "Sin direccion activa";
        _serverLink.AutoSize = false;
        _serverLink.Dock = DockStyle.Fill;
        _serverLink.TextAlign = ContentAlignment.BottomRight;
        _serverLink.LinkClicked += (_, _) => OpenServerLink();
        rightStack.Controls.Add(_serverLink, 0, 2);
    }

    private void BuildMainCards()
    {
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 14),
            BackColor = Color.Transparent
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        _root.Controls.Add(grid, 0, 1);

        var mediaCard = new ModernCard
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20),
            Margin = new Padding(0, 0, 8, 0),
            AccentColor = AppPalette.Dark.AccentAlt
        };
        grid.Controls.Add(mediaCard, 0, 0);
        BuildMediaCard(mediaCard);

        var automationCard = new ModernCard
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20),
            Margin = new Padding(8, 0, 0, 0),
            AccentColor = AppPalette.Dark.Accent
        };
        grid.Controls.Add(automationCard, 1, 0);
        BuildAutomationCard(automationCard);
    }

    private void BuildMediaCard(Control host)
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 7,
            BackColor = Color.Transparent
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        host.Controls.Add(layout);

        layout.Controls.Add(CreateSectionTitle("Biblioteca"), 0, 0);
        layout.Controls.Add(CreateCaption("Carpeta compartida"), 0, 1);

        var folderRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            BackColor = Color.Transparent
        };
        folderRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        folderRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 136));
        layout.Controls.Add(folderRow, 0, 2);

        ConfigureTextBox(_folderTextBox);
        _folderTextBox.ReadOnly = true;
        _folderTextBox.Margin = new Padding(0, 2, 8, 4);
        folderRow.Controls.Add(_folderTextBox, 0, 0);

        _browseButton.Text = "Seleccionar";
        _browseButton.Margin = new Padding(0, 2, 0, 4);
        _browseButton.Click += (_, _) => SelectFolder();
        folderRow.Controls.Add(_browseButton, 1, 0);

        layout.Controls.Add(CreateCaption("Nombre visible en la TV"), 0, 3);

        ConfigureTextBox(_deviceNameTextBox);
        _deviceNameTextBox.Margin = new Padding(0, 2, 0, 4);
        layout.Controls.Add(_deviceNameTextBox, 0, 4);

        ConfigureLabel(_folderStateLabel, "Sin carpeta seleccionada", 9f, FontStyle.Bold, muted: true);
        _folderStateLabel.Dock = DockStyle.Fill;
        layout.Controls.Add(_folderStateLabel, 0, 5);

        ConfigureLabel(_deviceStateLabel, "Compatible con DLNA/UPnP en red privada", 9f, FontStyle.Regular, muted: true);
        _deviceStateLabel.Dock = DockStyle.Fill;
        _deviceStateLabel.TextAlign = ContentAlignment.BottomLeft;
        layout.Controls.Add(_deviceStateLabel, 0, 6);
    }

    private void BuildAutomationCard(Control host)
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            BackColor = Color.Transparent
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        host.Controls.Add(layout);

        layout.Controls.Add(CreateSectionTitle("Comportamiento"), 0, 0);

        ConfigureSwitch(_autoStartServerSwitch, "Iniciar servidor al abrir");
        layout.Controls.Add(_autoStartServerSwitch, 0, 1);

        ConfigureSwitch(_startWithWindowsSwitch, "Iniciar con Windows");
        _startWithWindowsSwitch.CheckedChanged += (_, _) => _startMinimizedSwitch.Enabled = _startWithWindowsSwitch.Checked;
        layout.Controls.Add(_startWithWindowsSwitch, 0, 2);

        ConfigureSwitch(_startMinimizedSwitch, "Abrir minimizada");
        layout.Controls.Add(_startMinimizedSwitch, 0, 3);

        ConfigureSwitch(_minimizeToTraySwitch, "Cerrar hacia bandeja");
        layout.Controls.Add(_minimizeToTraySwitch, 0, 4);

        var bottom = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent
        };
        bottom.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        bottom.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        layout.Controls.Add(bottom, 0, 5);

        ConfigureLabel(_networkLabel, "Selecciona una carpeta para activar el servidor", 9f, FontStyle.Regular, muted: true);
    }

    private void BuildLogCard()
    {
        var logCard = new ModernCard
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20),
            AccentColor = Color.FromArgb(88, 105, 138)
        };
        _root.Controls.Add(logCard, 0, 2);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        logCard.Controls.Add(layout);

        layout.Controls.Add(CreateSectionTitle("Actividad"), 0, 0);

        _logTextBox.Dock = DockStyle.Fill;
        _logTextBox.Multiline = true;
        _logTextBox.ScrollBars = ScrollBars.Vertical;
        _logTextBox.ReadOnly = true;
        _logTextBox.BorderStyle = BorderStyle.FixedSingle;
        _logTextBox.Font = new Font("Consolas", 9f);
        _logTextBox.Margin = new Padding(0);
        layout.Controls.Add(_logTextBox, 0, 1);
    }

    private void BuildFooter()
    {
        var footer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 290));
        _root.Controls.Add(footer, 0, 3);

        var left = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent
        };
        footer.Controls.Add(left, 0, 0);

        ConfigureLabel(_footerLabel, "Servicio DLNA local", 9f, FontStyle.Regular, muted: true);
        _footerLabel.Dock = DockStyle.Fill;
        _footerLabel.TextAlign = ContentAlignment.MiddleLeft;
        left.Controls.Add(_footerLabel);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = Color.Transparent
        };
        footer.Controls.Add(buttons, 1, 0);

        _startStopButton.Text = "Iniciar";
        _startStopButton.IsPrimary = true;
        _startStopButton.Width = 128;
        _startStopButton.Margin = new Padding(8, 10, 0, 0);
        _startStopButton.Click += async (_, _) => await ToggleServerAsync();
        buttons.Controls.Add(_startStopButton);

        _saveButton.Text = "Guardar";
        _saveButton.Width = 128;
        _saveButton.Margin = new Padding(0, 10, 0, 0);
        _saveButton.Click += (_, _) => SaveSettingsFromUi(showConfirmation: true);
        buttons.Controls.Add(_saveButton);
    }

    private async Task OnLoadedAsync()
    {
        _settings = SettingsService.Load();
        ApplySettingsToUi();
        ApplyCurrentTheme();
        Log("App lista.");

        if (_settings.AutoStartServer && Directory.Exists(_settings.MediaFolder))
        {
            await StartServerAsync();
        }
        else if (string.IsNullOrWhiteSpace(_settings.MediaFolder))
        {
            Log("Selecciona una carpeta para compartir por DLNA.");
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
        _folderTextBox.Text = _settings.MediaFolder;
        _deviceNameTextBox.Text = _settings.DeviceName;
        _autoStartServerSwitch.Checked = _settings.AutoStartServer;
        _startWithWindowsSwitch.Checked = _settings.StartWithWindows;
        _startMinimizedSwitch.Checked = _settings.StartMinimized;
        _startMinimizedSwitch.Enabled = _settings.StartWithWindows;
        _minimizeToTraySwitch.Checked = _settings.MinimizeToTray;
        SelectThemeMode(ParseThemeMode(_settings.ThemeMode));
        UpdateFolderState();
        _isApplyingSettings = false;
    }

    private void SaveSettingsFromUi(bool showConfirmation)
    {
        _settings.MediaFolder = _folderTextBox.Text.Trim();
        _settings.DeviceName = _deviceNameTextBox.Text.Trim();
        _settings.AutoStartServer = _autoStartServerSwitch.Checked;
        _settings.StartWithWindows = _startWithWindowsSwitch.Checked;
        _settings.StartMinimized = _startMinimizedSwitch.Checked;
        _settings.MinimizeToTray = _minimizeToTraySwitch.Checked;
        _settings.ThemeMode = _themeMode.ToString();

        SettingsService.Save(_settings);
        if (showConfirmation)
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
            SelectedPath = Directory.Exists(_folderTextBox.Text) ? _folderTextBox.Text : Environment.GetFolderPath(Environment.SpecialFolder.MyVideos)
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _folderTextBox.Text = dialog.SelectedPath;
            if (string.IsNullOrWhiteSpace(_deviceNameTextBox.Text))
            {
                _deviceNameTextBox.Text = $"DLNA - {Path.GetFileName(dialog.SelectedPath)}";
            }

            SaveSettingsFromUi(showConfirmation: false);
            UpdateFolderState();
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

        if (!Directory.Exists(_settings.MediaFolder))
        {
            MessageBox.Show(
                "Selecciona una carpeta valida antes de iniciar el servidor.",
                "Folder DLNA",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        SetBusy(true);
        try
        {
            _server?.Dispose();
            _server = new DlnaServer(_settings.MediaFolder, _settings.DeviceName, _settings.Uuid, _settings.Port);
            _server.Message += (_, message) => BeginInvoke(() => Log(message));
            await _server.StartAsync();

            _statusPill.Text = "EN LINEA";
            _statusPill.Kind = StatusKind.Online;
            _statusPill.Invalidate();
            _serverLink.Text = _server.DescriptionUrl;
            _networkLabel.Text = "Visible para TVs y reproductores DLNA en esta red";
            _startStopButton.Text = "Detener";
            _trayStartStopItem.Text = "Detener servidor";
            _notifyIcon.Text = "Folder DLNA - activo";
            _logoMark.Active = true;
            _logoMark.Invalidate();
            SetAppIcons(active: true);
            Log($"Servidor iniciado en {_server.DescriptionUrl}");
            Log("Si Windows pregunta por permiso de red, permite el acceso en red privada.");
        }
        catch (Exception ex)
        {
            _server?.Dispose();
            _server = null;
            _statusPill.Text = "ERROR";
            _statusPill.Kind = StatusKind.Warning;
            _statusPill.Invalidate();
            _serverLink.Text = "Sin direccion activa";
            _networkLabel.Text = "No se pudo iniciar el servidor";
            SetAppIcons(active: false);
            Log($"Error al iniciar: {ex.Message}");
            MessageBox.Show(
                $"No se pudo iniciar el servidor:\n\n{ex.Message}",
                "Folder DLNA",
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
            _statusPill.Text = "DETENIDO";
            _statusPill.Kind = StatusKind.Offline;
            _statusPill.Invalidate();
            _serverLink.Text = "Sin direccion activa";
            _networkLabel.Text = Directory.Exists(_settings.MediaFolder)
                ? "Carpeta lista; servidor detenido"
                : "Selecciona una carpeta para activar el servidor";
            _startStopButton.Text = "Iniciar";
            _trayStartStopItem.Text = "Iniciar servidor";
            _notifyIcon.Text = "Folder DLNA";
            _logoMark.Active = false;
            _logoMark.Invalidate();
            SetAppIcons(active: false);
            Log("Servidor detenido.");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        _startStopButton.Enabled = !busy;
        _trayStartStopItem.Enabled = !busy;
        _saveButton.Enabled = !busy;
        _browseButton.Enabled = !busy;
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

    private async Task ExitApplicationAsync()
    {
        _isExiting = true;
        SaveSettingsFromUi(showConfirmation: false);
        await StopServerAsync();
        Close();
    }

    private void OpenServerLink()
    {
        if (_server is not { IsRunning: true })
        {
            return;
        }

        Process.Start(new ProcessStartInfo(_server.DescriptionUrl)
        {
            UseShellExecute = true
        });
    }

    private void Log(string message)
    {
        _logTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
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
        _themeButton.IsDarkTheme = _palette.IsDark;
        _toolTip.SetToolTip(_themeButton, _palette.IsDark ? "Cambiar a modo claro" : "Cambiar a modo oscuro");
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
        foreach (ToolStripItem item in _trayMenu.Items)
        {
            item.BackColor = _palette.Surface;
            item.ForeColor = _palette.Text;
        }
    }

    private void UpdateFolderState()
    {
        if (Directory.Exists(_folderTextBox.Text))
        {
            _folderStateLabel.Text = "Carpeta lista para compartir";
            _folderStateLabel.Tag = null;
            _networkLabel.Text = _server is { IsRunning: true }
                ? "Visible para TVs y reproductores DLNA en esta red"
                : "Carpeta lista; servidor detenido";
        }
        else
        {
            _folderStateLabel.Text = "Sin carpeta seleccionada";
            _folderStateLabel.Tag = "muted";
            _networkLabel.Text = "Selecciona una carpeta para activar el servidor";
        }

        ApplyCurrentTheme();
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
            _themeButton.IsDarkTheme = SystemTheme.Resolve(AppThemeMode.System).IsDark;
            return;
        }

        _themeButton.IsDarkTheme = mode == AppThemeMode.Dark;
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

    private static void ConfigureSwitch(ToggleSwitch toggleSwitch, string text)
    {
        toggleSwitch.Text = text;
        toggleSwitch.Dock = DockStyle.Fill;
        toggleSwitch.Margin = new Padding(0, 2, 0, 2);
    }

    private static Label CreateSectionTitle(string text)
    {
        var label = new Label();
        ConfigureLabel(label, text, 12f, FontStyle.Bold);
        label.Dock = DockStyle.Fill;
        return label;
    }

    private static Label CreateCaption(string text)
    {
        var label = new Label();
        ConfigureLabel(label, text, 8.5f, FontStyle.Regular, muted: true);
        label.Dock = DockStyle.Fill;
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
}
