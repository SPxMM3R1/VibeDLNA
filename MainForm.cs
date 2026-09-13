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
    private readonly Label _brandLabel = new();
    private readonly Label _pageTitleLabel = new();
    private readonly Label _pageSubtitleLabel = new();
    private readonly WinUiCard _statusCard = new();
    private readonly WinUiStatusIndicator _statusIndicator = new();
    private readonly Label _statusHeadlineLabel = new();
    private readonly Label _statusEndpointLabel = new();
    private readonly Label _nameCaptionLabel = new();
    private readonly Label _sharedFoldersHeaderLabel = new();
    private readonly Label _sharedContentHeaderLabel = new();
    private readonly Label _sharedContentDescriptionLabel = new();
    private readonly Label _activityHeaderLabel = new();
    private readonly WinUiNavItem _serverNavItem = new();
    private readonly WinUiNavItem _libraryNavItem = new();
    private readonly WinUiNavItem _optionsNavItem = new();
    private readonly WinUiButton _startButton = new();
    private readonly WinUiButton _stopButton = new();
    private readonly WinUiButton _rescanButton = new();
    private readonly WinUiButton _addFolderButton = new();
    private readonly WinUiButton _clearActivityButton = new();
    private readonly WinUiToggle _videosToggle = new();
    private readonly WinUiToggle _audioToggle = new();
    private readonly WinUiToggle _photosToggle = new();
    private readonly GitHubUpdateService _updateService = new();

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
    private bool _mediaToggleUpdateInProgress;

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
        _root.RowCount = 3;
        _root.Padding = new Padding(0);
        _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        _root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        Controls.Add(_root);

        _root.Controls.Add(BuildAppHeader(), 0, 0);
        _root.Controls.Add(BuildWorkspace(), 0, 1);
        _root.Controls.Add(BuildStatusFooter(), 0, 2);
    }

    private Control BuildAppHeader()
    {
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Padding = new Padding(16, 7, 14, 5),
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 224));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 54));

        ConfigureLabel(_brandLabel, "◈  VibeDLNA", 11f, FontStyle.Bold);
        _brandLabel.Dock = DockStyle.Fill;
        _brandLabel.TextAlign = ContentAlignment.MiddleLeft;
        header.Controls.Add(_brandLabel, 0, 0);

        ConfigureThemeGlyph();
        header.Controls.Add(_themeGlyphLabel, 2, 0);
        return header;
    }

    private Control BuildWorkspace()
    {
        var workspace = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(0),
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        };
        workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 224));
        workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        workspace.Controls.Add(BuildNavigationPane(), 0, 0);
        workspace.Controls.Add(BuildMainPage(), 1, 0);
        return workspace;
    }

    private Control BuildNavigationPane()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12, 8, 10, 10),
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 7,
            Padding = new Padding(0),
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));

        var menuGlyph = new Label
        {
            Text = "☰",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI Symbol", 16f),
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = Color.Transparent,
            ForeColor = _palette.Text,
            Padding = new Padding(10, 0, 0, 0)
        };
        layout.Controls.Add(menuGlyph, 0, 0);

        ConfigureNavItem(_serverNavItem, "⌂", "Servidor", selected: true, () => SelectNavigation(_serverNavItem));
        ConfigureNavItem(_libraryNavItem, "♫", "Biblioteca", selected: false, FocusSharedFolders);
        ConfigureNavItem(_optionsNavItem, "⚙", "Opciones", selected: false, () =>
        {
            SelectNavigation(_optionsNavItem);
            OpenOptionsDialog();
        });
        layout.Controls.Add(_serverNavItem, 0, 1);
        layout.Controls.Add(_libraryNavItem, 0, 2);
        layout.Controls.Add(_optionsNavItem, 0, 3);

        ConfigureLabel(_footerLabel, "VibeDLNA", 9.5f, FontStyle.Bold);
        _footerLabel.Dock = DockStyle.Fill;
        _footerLabel.TextAlign = ContentAlignment.BottomLeft;
        _footerLabel.Padding = new Padding(10, 0, 0, 4);
        layout.Controls.Add(_footerLabel, 0, 5);

        var state = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(10, 0, 0, 0),
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        };
        state.RowStyles.Add(new RowStyle(SizeType.Percent, 34));
        state.RowStyles.Add(new RowStyle(SizeType.Percent, 33));
        state.RowStyles.Add(new RowStyle(SizeType.Percent, 33));
        ConfigureLabel(_sidebarServerLabel, "Servidor detenido", 8.5f, FontStyle.Regular, muted: true);
        ConfigureLabel(_sidebarFolderLabel, "Biblioteca pendiente", 8.5f, FontStyle.Regular, muted: true);
        ConfigureLabel(_sidebarWindowsLabel, "Inicio con Windows: no", 8.5f, FontStyle.Regular, muted: true);
        state.Controls.Add(_sidebarServerLabel, 0, 0);
        state.Controls.Add(_sidebarFolderLabel, 0, 1);
        state.Controls.Add(_sidebarWindowsLabel, 0, 2);
        layout.Controls.Add(state, 0, 6);

        panel.Controls.Add(layout);
        return panel;
    }

    private Control BuildMainPage()
    {
        var host = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(22, 12, 22, 8),
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        };
        var page = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(0),
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        };
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 250));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 168));

        var heading = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        };
        heading.RowStyles.Add(new RowStyle(SizeType.Percent, 68));
        heading.RowStyles.Add(new RowStyle(SizeType.Percent, 32));
        ConfigureLabel(_pageTitleLabel, "Servidor DLNA", 20f, FontStyle.Bold);
        ConfigureLabel(_pageSubtitleLabel, "Comparte tu colección multimedia en la red local mediante DLNA/UPnP.", 9.5f, FontStyle.Regular, muted: true);
        heading.Controls.Add(_pageTitleLabel, 0, 0);
        heading.Controls.Add(_pageSubtitleLabel, 0, 1);
        page.Controls.Add(heading, 0, 0);

        page.Controls.Add(BuildCommandBar(), 0, 1);
        page.Controls.Add(BuildServerStatusCard(), 0, 2);
        page.Controls.Add(BuildLibraryAndContentArea(), 0, 3);
        page.Controls.Add(BuildActivityCard(), 0, 4);

        host.Controls.Add(page);
        return host;
    }

    private Control BuildCommandBar()
    {
        var bar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 2, 0, 4),
            Margin = new Padding(0)
        };
        ConfigureActionButton(_startButton, "Iniciar servidor", "▶", primary: true, async () => await StartServerAsync());
        ConfigureActionButton(_stopButton, "Detener", "■", primary: false, async () => await StopServerAsync());
        ConfigureActionButton(_rescanButton, "Reescanear", "↻", primary: false, RescanLibrary);
        bar.Controls.Add(_startButton);
        bar.Controls.Add(_stopButton);
        bar.Controls.Add(_rescanButton);
        return bar;
    }

    private Control BuildServerStatusCard()
    {
        _statusCard.Dock = DockStyle.Fill;
        _statusCard.Margin = new Padding(0, 2, 0, 6);
        _statusCard.Padding = new Padding(14, 8, 14, 8);
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 34));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 248));

        _statusIndicator.Dock = DockStyle.Fill;
        _statusIndicator.AccessibleName = "Estado del servidor";
        layout.Controls.Add(_statusIndicator, 0, 0);

        var statusText = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        };
        statusText.RowStyles.Add(new RowStyle(SizeType.Percent, 58));
        statusText.RowStyles.Add(new RowStyle(SizeType.Percent, 42));
        ConfigureLabel(_statusHeadlineLabel, "Servidor detenido", 11f, FontStyle.Bold);
        ConfigureLabel(_statusEndpointLabel, "Listo para iniciar en la red local", 9f, FontStyle.Regular, muted: true);
        statusText.Controls.Add(_statusHeadlineLabel, 0, 0);
        statusText.Controls.Add(_statusEndpointLabel, 0, 1);
        layout.Controls.Add(statusText, 1, 0);

        var identity = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        };
        identity.RowStyles.Add(new RowStyle(SizeType.Absolute, 17));
        identity.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        ConfigureLabel(_nameCaptionLabel, "Nombre visible en la TV", 8.5f, FontStyle.Regular, muted: true);
        ConfigureTextBox(_deviceNameTextBox);
        _deviceNameTextBox.Margin = new Padding(0, 0, 0, 0);
        identity.Controls.Add(_nameCaptionLabel, 0, 0);
        identity.Controls.Add(_deviceNameTextBox, 0, 1);
        layout.Controls.Add(identity, 2, 0);

        _statusCard.Controls.Add(layout);
        return _statusCard;
    }

    private Control BuildLibraryAndContentArea()
    {
        var area = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        };
        area.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 64));
        area.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36));
        area.Controls.Add(BuildLibraryCard(), 0, 0);
        area.Controls.Add(BuildSharedContentCard(), 1, 0);
        return area;
    }

    private Control BuildLibraryCard()
    {
        var card = new WinUiCard
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 8, 0),
            Padding = new Padding(14, 10, 14, 10)
        };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 154));
        ConfigureLabel(_sharedFoldersHeaderLabel, "Carpetas compartidas", 11f, FontStyle.Bold);
        ConfigureActionButton(_addFolderButton, "Agregar carpeta", "+", primary: false, AddFolderFromMain);
        header.Controls.Add(_sharedFoldersHeaderLabel, 0, 0);
        header.Controls.Add(_addFolderButton, 1, 0);
        layout.Controls.Add(header, 0, 0);

        ConfigureGrid(_statusGrid);
        _statusGrid.Columns.Add("folder", "Carpeta");
        _statusGrid.Columns.Add("state", "Estado");
        _statusGrid.Columns.Add("actions", "");
        _statusGrid.Columns[0].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        _statusGrid.Columns[0].FillWeight = 170;
        _statusGrid.Columns[1].Width = 112;
        _statusGrid.Columns[2].Width = 38;
        _statusGrid.Columns[2].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        _statusGrid.CellClick += (_, e) =>
        {
            if (e.RowIndex >= 0 && e.ColumnIndex == 2)
            {
                OpenOptionsDialog();
            }
        };
        _statusGrid.CellFormatting += (_, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex != 1)
            {
                return;
            }

            var state = e.Value?.ToString() ?? string.Empty;
            var color = state.Contains("No disponible", StringComparison.Ordinal)
                ? _palette.Warning
                : state.Contains("Disponible", StringComparison.Ordinal)
                ? _palette.Success
                : _palette.MutedText;
            if (e.CellStyle is { } cellStyle)
            {
                cellStyle.ForeColor = color;
            }
        };
        layout.Controls.Add(_statusGrid, 0, 1);
        card.Controls.Add(layout);
        return card;
    }

    private Control BuildSharedContentCard()
    {
        var card = new WinUiCard
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(8, 0, 0, 0),
            Padding = new Padding(14, 10, 14, 10)
        };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        ConfigureLabel(_sharedContentHeaderLabel, "Contenido compartido", 11f, FontStyle.Bold);
        ConfigureLabel(
            _sharedContentDescriptionLabel,
            "Selecciona qué tipos de contenido estarán disponibles\nen tu servidor DLNA.",
            8.5f,
            FontStyle.Regular,
            muted: true);
        _sharedContentDescriptionLabel.TextAlign = ContentAlignment.MiddleLeft;
        layout.Controls.Add(_sharedContentHeaderLabel, 0, 0);
        layout.Controls.Add(_sharedContentDescriptionLabel, 0, 1);
        ConfigureToggle(_videosToggle, "Videos", () => HandleMediaToggleChanged(_videosToggle, value => _settings.ShareVideos = value));
        ConfigureToggle(_audioToggle, "Audio", () => HandleMediaToggleChanged(_audioToggle, value => _settings.ShareAudio = value));
        ConfigureToggle(_photosToggle, "Fotos", () => HandleMediaToggleChanged(_photosToggle, value => _settings.ShareImages = value));
        layout.Controls.Add(_videosToggle, 0, 2);
        layout.Controls.Add(_audioToggle, 0, 3);
        layout.Controls.Add(_photosToggle, 0, 4);
        card.Controls.Add(layout);
        return card;
    }

    private Control BuildActivityCard()
    {
        var card = new WinUiCard
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 8, 0, 0),
            Padding = new Padding(14, 10, 14, 10)
        };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
        ConfigureLabel(_activityHeaderLabel, "Actividad reciente", 11f, FontStyle.Bold);
        ConfigureActionButton(_clearActivityButton, "Limpiar", "⌫", primary: false, ClearActivity);
        _clearActivityButton.Width = 104;
        header.Controls.Add(_activityHeaderLabel, 0, 0);
        header.Controls.Add(_clearActivityButton, 1, 0);
        layout.Controls.Add(header, 0, 0);
        ConfigureGrid(_logGrid);
        _logGrid.Columns.Add("time", "Hora");
        _logGrid.Columns.Add("message", "Evento");
        _logGrid.Columns[0].Width = 82;
        _logGrid.Columns[1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        layout.Controls.Add(_logGrid, 0, 1);
        card.Controls.Add(layout);
        return card;
    }

    private Control BuildStatusFooter()
    {
        var status = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            Padding = new Padding(14, 3, 14, 3),
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        };
        status.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        status.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 360));
        status.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        ConfigureLabel(_bottomAddressLabel, "Sin direccion activa", 8.5f, FontStyle.Regular, muted: true);
        ConfigureLabel(_bottomStateLabel, "DLNA detenido", 8.5f, FontStyle.Bold);
        _bottomAddressLabel.TextAlign = ContentAlignment.MiddleRight;
        _bottomStateLabel.TextAlign = ContentAlignment.MiddleRight;
        status.Controls.Add(new Label { Dock = DockStyle.Fill, BackColor = Color.Transparent }, 0, 0);
        status.Controls.Add(_bottomAddressLabel, 1, 0);
        status.Controls.Add(_bottomStateLabel, 2, 0);
        return status;
    }

    private void ConfigureNavItem(WinUiNavItem item, string glyph, string text, bool selected, Action action)
    {
        item.Glyph = glyph;
        item.Text = text;
        item.Selected = selected;
        item.Dock = DockStyle.Fill;
        item.AccessibleName = text;
        item.Click += (_, _) => action();
    }

    private void SelectNavigation(WinUiNavItem selected)
    {
        _serverNavItem.Selected = ReferenceEquals(selected, _serverNavItem);
        _libraryNavItem.Selected = ReferenceEquals(selected, _libraryNavItem);
        _optionsNavItem.Selected = ReferenceEquals(selected, _optionsNavItem);
        _serverNavItem.Invalidate();
        _libraryNavItem.Invalidate();
        _optionsNavItem.Invalidate();
    }

    private void FocusSharedFolders()
    {
        SelectNavigation(_libraryNavItem);
        _statusGrid.Focus();
        if (_statusGrid.Rows.Count > 0)
        {
            _statusGrid.CurrentCell = _statusGrid.Rows[0].Cells[0];
        }
    }

    private void ConfigureActionButton(WinUiButton button, string text, string glyph, bool primary, Action action)
    {
        button.Text = text;
        button.Glyph = glyph;
        button.Primary = primary;
        button.Width = primary ? 170 : text == "Agregar carpeta" ? 138 : 122;
        button.Height = 36;
        button.Margin = new Padding(0, 0, 8, 0);
        button.AccessibleName = text;
        button.Click += (_, _) => action();
    }

    private void ConfigureActionButton(WinUiButton button, string text, string glyph, bool primary, Func<Task> action)
    {
        ConfigureActionButton(button, text, glyph, primary, (Action)(() => _ = action()));
    }

    private void ConfigureToggle(WinUiToggle toggle, string text, Action action)
    {
        toggle.Text = text;
        toggle.StateTextOn = "Activado";
        toggle.StateTextOff = "Desactivado";
        toggle.Dock = DockStyle.Fill;
        toggle.AccessibleName = text;
        toggle.CheckedChanged += (_, _) => action();
    }

    private void AddFolderFromMain()
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

        if (!_settings.MediaFolders.Any(folder => string.Equals(folder, dialog.SelectedPath, StringComparison.OrdinalIgnoreCase)))
        {
            _settings.MediaFolders.Add(dialog.SelectedPath);
            _settings.MediaFolder = _settings.MediaFolders[0];
            SaveSettingsFromUi(showConfirmation: false);
            Log($"Carpeta agregada: {dialog.SelectedPath}");
        }
    }

    private void HandleMediaToggleChanged(WinUiToggle toggle, Action<bool> setter)
    {
        if (_mediaToggleUpdateInProgress || _isApplyingSettings)
        {
            return;
        }

        setter(toggle.Checked);
        SaveSettingsFromUi(showConfirmation: false);
        RefreshStatusView();
        if (_server is { IsRunning: true })
        {
            BeginInvoke(async () =>
            {
                await StopServerAsync();
                await StartServerAsync();
            });
        }
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
        _mediaToggleUpdateInProgress = true;
        _videosToggle.Checked = _settings.ShareVideos;
        _audioToggle.Checked = _settings.ShareAudio;
        _photosToggle.Checked = _settings.ShareImages;
        _mediaToggleUpdateInProgress = false;
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
        _optionsNavItem.Enabled = !busy;
        _libraryNavItem.Enabled = !busy;
        _videosToggle.Enabled = !busy;
        _audioToggle.Enabled = !busy;
        _photosToggle.Enabled = !busy;
        _addFolderButton.Enabled = !busy;
        _clearActivityButton.Enabled = !busy;
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
        using var dialog = new OptionsDialog(_settings, _palette, RescanLibrary, _updateService);
        var result = dialog.ShowDialog(this);
        if (result == DialogResult.Abort)
        {
            BeginInvoke(async () => await ExitApplicationAsync());
            return;
        }

        if (result != DialogResult.OK)
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

    private void ClearActivity()
    {
        _logGrid.Rows.Clear();
        AppLog.Write("Actividad limpiada.");
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
        else if (control.Parent is WinUiCard)
        {
            control.BackColor = Color.Transparent;
            control.ForeColor = _palette.Text;
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
            control.BackColor = control == _root || HasWinUiCardAncestor(control)
                ? control == _root ? _palette.Window : Color.Transparent
                : ThemePaint.ResolveBackColor(control, _palette);
            control.ForeColor = _palette.Text;
        }
        else
        {
            control.BackColor = control == this || control == _root
                ? _palette.Window
                : HasWinUiCardAncestor(control) ? Color.Transparent : ThemePaint.ResolveBackColor(control, _palette);
            control.ForeColor = _palette.Text;
        }

        foreach (Control child in control.Controls)
        {
            ApplyPaletteRecursive(child);
        }
    }

    private static bool HasWinUiCardAncestor(Control control)
    {
        for (var parent = control.Parent; parent is not null; parent = parent.Parent)
        {
            if (parent is WinUiCard)
            {
                return true;
            }
        }

        return false;
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
        var configuredFolders = _settings.MediaFolders
            .Where(folder => !string.IsNullOrWhiteSpace(folder))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var validFolders = configuredFolders.Where(Directory.Exists).ToList();
        var folderExists = validFolders.Count > 0;

        _statusIndicator.IsActive = running;
        _statusHeadlineLabel.Text = running ? "Servidor activo" : "Servidor detenido";
        _statusHeadlineLabel.ForeColor = running ? _palette.Success : _palette.Text;
        _statusEndpointLabel.Text = running && _server is not null
            ? _server.DescriptionUrl
            : "Listo para iniciar en la red local";
        _pageSubtitleLabel.Text = "Comparte tu colección multimedia en la red local mediante DLNA/UPnP.";

        _sidebarServerLabel.Text = running ? "Servidor activo" : "Servidor detenido";
        _sidebarServerLabel.ForeColor = running ? _palette.Success : _palette.MutedText;
        _sidebarFolderLabel.Text = folderExists ? $"Biblioteca lista ({validFolders.Count})" : "Biblioteca pendiente";
        _sidebarWindowsLabel.Text = _settings.StartWithWindows ? "Inicio Win: si" : "Inicio Win: no";

        _bottomStateLabel.Text = running ? "DLNA activo" : "DLNA detenido";
        _bottomAddressLabel.Text = running && _server is not null ? _server.DescriptionUrl : "Sin direccion activa";
        if (_statusGrid.Columns.Count == 0)
        {
            return;
        }

        _statusCard.IsActive = running;
        _statusGrid.Rows.Clear();
        foreach (var folder in configuredFolders)
        {
            _statusGrid.Rows.Add(
                $"📁  {folder}",
                Directory.Exists(folder) ? "●  Disponible" : "▲  No disponible",
                "⋯");
        }

        if (configuredFolders.Count == 0)
        {
            _statusGrid.Rows.Add("Sin carpetas configuradas", "Pendiente", "");
        }

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
        _startButton.Enabled = _startCommandAvailable;
        _stopButton.Enabled = _stopCommandAvailable;
        _rescanButton.Enabled = _saveCommandAvailable;
        _addFolderButton.Enabled = _saveCommandAvailable;
        _clearActivityButton.Enabled = _saveCommandAvailable;
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
        grid.ColumnHeadersHeight = 30;
        grid.RowTemplate.Height = 36;
        grid.DefaultCellStyle.Font = new Font("Segoe UI", 9.5f);
        grid.DefaultCellStyle.Padding = new Padding(4, 0, 4, 0);
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
        grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(4, 0, 4, 0);
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
        grid.AlternatingRowsDefaultCellStyle.BackColor = _palette.IsDark ? Color.FromArgb(25, 32, 45) : Color.FromArgb(248, 251, 254);
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
