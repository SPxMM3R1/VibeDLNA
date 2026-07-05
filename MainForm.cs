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
    private readonly FlatIconButton _toolbarStartButton = new();
    private readonly FlatIconButton _toolbarStopButton = new();
    private readonly FlatIconButton _toolbarSaveButton = new();
    private readonly ComboBox _toolsComboBox = new();
    private readonly DataGridView _statusGrid = new();
    private readonly DataGridView _logGrid = new();
    private readonly Label _sidebarServerLabel = new();
    private readonly Label _sidebarFolderLabel = new();
    private readonly Label _sidebarWindowsLabel = new();
    private readonly Label _bottomAddressLabel = new();
    private readonly Label _bottomStateLabel = new();
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
    private bool _isRefreshingTools;

    public MainForm(bool requestedStartMinimized)
    {
        _requestedStartMinimized = requestedStartMinimized;

        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 9.5f);
        Text = "Folder DLNA";
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
        _root.RowCount = 3;
        _root.Padding = new Padding(0);
        _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        _root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        Controls.Add(_root);

        BuildFlatToolbar();
        BuildFlatWorkspace();
        BuildFlatStatusBar();
    }

    private void BuildFlatToolbar()
    {
        var toolbar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            Padding = new Padding(12, 8, 12, 8)
        };
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 148));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 560));
        _root.Controls.Add(toolbar, 0, 0);

        var left = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        toolbar.Controls.Add(left, 0, 0);

        ConfigureToolbarButton(_toolbarStartButton, FlatIconKind.Play, "Iniciar servidor", async () => await StartServerAsync(), accent: true);
        ConfigureToolbarButton(_toolbarStopButton, FlatIconKind.Stop, "Detener servidor", async () => await StopServerAsync());
        ConfigureToolbarButton(_toolbarSaveButton, FlatIconKind.Save, "Guardar opciones", () => SaveSettingsFromUi(showConfirmation: true));
        left.Controls.AddRange(new Control[] { _toolbarStartButton, _toolbarStopButton, _toolbarSaveButton });

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
            ColumnCount = 5,
            RowCount = 1
        };
        right.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        right.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 12));
        right.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        right.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 12));
        right.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 50));
        toolbar.Controls.Add(right, 2, 0);

        ConfigureToolsDropDown();
        right.Controls.Add(_toolsComboBox, 0, 0);

        ConfigureTextBox(_deviceNameTextBox);
        _deviceNameTextBox.Margin = new Padding(0, 4, 0, 4);
        right.Controls.Add(_deviceNameTextBox, 2, 0);

        _themeButton.Margin = new Padding(4, 4, 0, 4);
        _themeButton.Click += (_, _) => ToggleTheme();
        _toolTip.SetToolTip(_themeButton, "Cambiar tema");
        right.Controls.Add(_themeButton, 4, 0);
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
        _root.Controls.Add(split, 0, 1);

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
        _root.Controls.Add(status, 0, 2);

        ConfigureLabel(_footerLabel, "Folder DLNA", 9f, FontStyle.Regular, muted: true);
        ConfigureLabel(_bottomAddressLabel, "Sin direccion activa", 9f, FontStyle.Regular, muted: true);
        ConfigureLabel(_bottomStateLabel, "DLNA detenido", 9f, FontStyle.Bold);
        _bottomAddressLabel.TextAlign = ContentAlignment.MiddleRight;
        _bottomStateLabel.TextAlign = ContentAlignment.MiddleRight;
        status.Controls.Add(_footerLabel, 0, 0);
        status.Controls.Add(_bottomAddressLabel, 1, 0);
        status.Controls.Add(_bottomStateLabel, 2, 0);
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
        RefreshToolsDropDown();
        RefreshStatusView();
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
        RefreshToolsDropDown();
        RefreshStatusView();
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
            RefreshStatusView();
            RefreshCommandState();
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
            RefreshStatusView();
            RefreshCommandState();
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
        _startStopButton.Enabled = !busy;
        _trayStartStopItem.Enabled = !busy;
        _saveButton.Enabled = !busy;
        _browseButton.Enabled = !busy;
        _folderTextBox.Enabled = !busy;
        _toolsComboBox.Enabled = !busy;
        _toolbarSaveButton.Enabled = !busy;
        _toolbarStartButton.Enabled = !busy && _server is not { IsRunning: true };
        _toolbarStopButton.Enabled = !busy && _server is { IsRunning: true };
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
        _themeButton.IsDarkTheme = _palette.IsDark;
        _toolTip.SetToolTip(_themeButton, _palette.IsDark ? "Cambiar a modo claro" : "Cambiar a modo oscuro");
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
        else if (control is ComboBox comboBox)
        {
            comboBox.BackColor = _palette.Elevated;
            comboBox.ForeColor = _palette.Text;
            comboBox.FlatStyle = FlatStyle.Flat;
        }
        else if (control is DataGridView grid)
        {
            ApplyGridPalette(grid);
        }
        else if (control is MenuStrip menu)
        {
            menu.BackColor = _palette.Window;
            menu.ForeColor = _palette.Text;
            foreach (ToolStripItem item in menu.Items)
            {
                item.BackColor = _palette.Window;
                item.ForeColor = _palette.Text;
            }
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

    private void RefreshStatusView()
    {
        var running = _server is { IsRunning: true };
        var folderExists = Directory.Exists(_settings.MediaFolder);
        _sidebarServerLabel.Text = running ? "Servidor activo (1)" : "Servidor detenido (0)";
        _sidebarFolderLabel.Text = folderExists ? "Biblioteca lista (1)" : "Biblioteca pendiente (0)";
        _sidebarWindowsLabel.Text = _settings.StartWithWindows ? "Inicio Win: si" : "Inicio Win: no";
        RefreshToolsDropDown();

        _bottomStateLabel.Text = running ? "DLNA activo" : "DLNA detenido";
        _bottomAddressLabel.Text = running && _server is not null ? _server.DescriptionUrl : "Sin direccion activa";
        _serverLink.Text = _bottomAddressLabel.Text;

        if (_statusGrid.Columns.Count == 0)
        {
            return;
        }

        _statusGrid.Rows.Clear();
        _statusGrid.Rows.Add("Servidor DLNA", running ? "Activo" : "Detenido", running ? "Anunciando por UPnP en la red local" : "Listo para iniciar");
        _statusGrid.Rows.Add("Biblioteca", folderExists ? "Lista" : "Pendiente", string.IsNullOrWhiteSpace(_settings.MediaFolder) ? "Sin carpeta seleccionada" : _settings.MediaFolder);
        _statusGrid.Rows.Add("Nombre visible", "Configurado", _settings.DeviceName);
        _statusGrid.Rows.Add("Direccion", running ? "Disponible" : "No disponible", running && _server is not null ? _server.DescriptionUrl : "Sin direccion activa");
        _statusGrid.Rows.Add("Inicio con Windows", _settings.StartWithWindows ? "Activado" : "Desactivado", _settings.StartMinimized ? "Abrira minimizada" : "Abrira visible");
        _statusGrid.Rows.Add("Bandeja", _settings.MinimizeToTray ? "Activada" : "Desactivada", _settings.MinimizeToTray ? "Cerrar envia al area de notificacion" : "Cerrar sale de la app");
        RefreshCommandState();
    }

    private void RefreshCommandState()
    {
        var running = _server is { IsRunning: true };
        _toolbarStartButton.Enabled = !running;
        _toolbarStopButton.Enabled = running;
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

    private void ConfigureToolsDropDown()
    {
        _toolsComboBox.Dock = DockStyle.Fill;
        _toolsComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _toolsComboBox.FlatStyle = FlatStyle.Flat;
        _toolsComboBox.Font = new Font("Segoe UI", 9.5f);
        _toolsComboBox.Margin = new Padding(0, 4, 0, 4);
        _toolsComboBox.SelectedIndexChanged += (_, _) => ApplySelectedToolOption();
        _toolTip.SetToolTip(_toolsComboBox, "Herramientas y comportamiento de inicio");
        RefreshToolsDropDown();
    }

    private void RefreshToolsDropDown()
    {
        if (_toolsComboBox.IsDisposed)
        {
            return;
        }

        _isRefreshingTools = true;
        _toolsComboBox.BeginUpdate();
        _toolsComboBox.Items.Clear();
        _toolsComboBox.Items.Add("Herramientas");
        _toolsComboBox.Items.Add($"Auto inicio: {YesNo(_settings.AutoStartServer)}");
        _toolsComboBox.Items.Add($"Inicio Windows: {YesNo(_settings.StartWithWindows)}");
        _toolsComboBox.Items.Add($"Abrir minimizada: {YesNo(_settings.StartMinimized)}");
        _toolsComboBox.Items.Add($"Cerrar a bandeja: {YesNo(_settings.MinimizeToTray)}");
        _toolsComboBox.EndUpdate();
        _toolsComboBox.SelectedIndex = 0;
        _isRefreshingTools = false;
    }

    private void ApplySelectedToolOption()
    {
        if (_isRefreshingTools || _toolsComboBox.SelectedIndex <= 0)
        {
            return;
        }

        var changed = _toolsComboBox.SelectedIndex switch
        {
            1 => ToggleOption(value => _settings.AutoStartServer = value, _settings.AutoStartServer, "Auto inicio"),
            2 => ToggleOption(value => _settings.StartWithWindows = value, _settings.StartWithWindows, "Inicio Windows"),
            3 => ToggleOption(value => _settings.StartMinimized = value, _settings.StartMinimized, "Abrir minimizada"),
            4 => ToggleOption(value => _settings.MinimizeToTray = value, _settings.MinimizeToTray, "Cerrar a bandeja"),
            _ => string.Empty
        };

        if (string.IsNullOrWhiteSpace(changed))
        {
            RefreshToolsDropDown();
            return;
        }

        SyncOptionSwitches();
        SaveSettingsFromUi(showConfirmation: false);
        Log($"Herramientas: {changed}");
    }

    private string ToggleOption(Action<bool> assign, bool currentValue, string label)
    {
        var nextValue = !currentValue;
        assign(nextValue);
        return $"{label}: {YesNo(nextValue)}";
    }

    private void SyncOptionSwitches()
    {
        _autoStartServerSwitch.Checked = _settings.AutoStartServer;
        _startWithWindowsSwitch.Checked = _settings.StartWithWindows;
        _startMinimizedSwitch.Checked = _settings.StartMinimized;
        _startMinimizedSwitch.Enabled = _settings.StartWithWindows;
        _minimizeToTraySwitch.Checked = _settings.MinimizeToTray;
    }

    private static string YesNo(bool value) => value ? "si" : "no";

    private void ConfigureToolbarButton(FlatIconButton button, FlatIconKind kind, string tooltip, Action action, bool accent = false)
    {
        button.IconKind = kind;
        button.IsAccent = accent;
        button.Margin = new Padding(0, 0, 8, 0);
        button.Click += (_, _) => action();
        _toolTip.SetToolTip(button, tooltip);
    }

    private void ConfigureToolbarButton(FlatIconButton button, FlatIconKind kind, string tooltip, Func<Task> action, bool accent = false)
    {
        button.IconKind = kind;
        button.IsAccent = accent;
        button.Margin = new Padding(0, 0, 8, 0);
        button.Click += async (_, _) => await action();
        _toolTip.SetToolTip(button, tooltip);
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
