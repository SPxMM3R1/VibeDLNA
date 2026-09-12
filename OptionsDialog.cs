namespace FolderDlnaServer;

internal sealed class OptionsDialog : Form
{
    private readonly AppSettings _settings;
    private readonly AppPalette _palette;
    private readonly Action _rescanAction;
    private readonly GitHubUpdateService _updateService;
    private readonly ListBox _foldersList = new();
    private readonly TextBox _deviceNameTextBox = new();
    private readonly List<OptionLine> _optionLines = new();
    private readonly Label _updateStatusLabel = new();
    private readonly Label _updateActionLabel = new();
    private readonly CancellationTokenSource _updateLifetime = new();

    private bool _shareVideos;
    private bool _shareAudio;
    private bool _shareImages;
    private bool _autoRescan;
    private bool _keepAwake;
    private bool _autoStartServer;
    private bool _startWithWindows;
    private bool _startMinimized;
    private bool _minimizeToTray;
    private GitHubReleaseInfo? _availableUpdate;
    private bool _updateBusy;

    private Color DialogBackground => _palette.Window;
    private Color FieldBackground => _palette.Elevated;

    public OptionsDialog(
        AppSettings settings,
        AppPalette palette,
        Action rescanAction,
        GitHubUpdateService updateService)
    {
        _settings = Clone(settings);
        _palette = palette;
        _rescanAction = rescanAction;
        _updateService = updateService;

        _shareVideos = _settings.ShareVideos;
        _shareAudio = _settings.ShareAudio;
        _shareImages = _settings.ShareImages;
        _autoRescan = _settings.AutoRescanLibrary;
        _keepAwake = _settings.KeepAwake;
        _autoStartServer = _settings.AutoStartServer;
        _startWithWindows = _settings.StartWithWindows;
        _startMinimized = _settings.StartMinimized;
        _minimizeToTray = _settings.MinimizeToTray;

        Text = "Opciones";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(760, 620);
        MinimumSize = new Size(720, 560);
        Font = new Font("Segoe UI", 9.5f);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        HandleCreated += (_, _) => NativeTheme.ApplyFlatWindow(this, _palette.IsDark);

        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BuildUi();
        ApplyPalette(this);
        RefreshOptionLines();
        Shown += async (_, _) => await CheckForUpdatesAsync();
        FormClosed += (_, _) => _updateLifetime.Cancel();
    }

    public AppSettings Settings => _settings;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _updateLifetime.Cancel();
            _updateLifetime.Dispose();
        }

        base.Dispose(disposing);
    }

    private void BuildUi()
    {
        var shell = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18),
            BackColor = DialogBackground
        };
        Controls.Add(shell);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(0),
            Margin = new Padding(0),
            BackColor = DialogBackground
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        shell.Controls.Add(root);

        root.Controls.Add(CreateTitle("Opciones"), 0, 0);

        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = DialogBackground
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48));
        root.Controls.Add(body, 0, 1);

        body.Controls.Add(BuildLibraryPanel(), 0, 0);
        body.Controls.Add(BuildBehaviorPanel(), 1, 0);
        root.Controls.Add(BuildFooter(), 0, 2);
    }

    private Control BuildLibraryPanel()
    {
        var panel = CreatePanel(9);
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));

        panel.Controls.Add(CreateHeader("Biblioteca"), 0, 0);
        panel.Controls.Add(CreateCaption("Carpetas compartidas"), 0, 1);

        _foldersList.Dock = DockStyle.Fill;
        _foldersList.BorderStyle = BorderStyle.FixedSingle;
        foreach (var folder in _settings.MediaFolders)
        {
            _foldersList.Items.Add(folder);
        }

        panel.Controls.Add(_foldersList, 0, 2);

        var folderActions = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = DialogBackground,
            Margin = new Padding(0)
        };
        folderActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
        folderActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 27));
        folderActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        folderActions.Controls.Add(CreateTextCommand("Agregar", _palette.AccentAlt, AddFolder), 0, 0);
        folderActions.Controls.Add(CreateTextCommand("Quitar", _palette.MutedText, RemoveSelectedFolder), 1, 0);
        folderActions.Controls.Add(CreateTextCommand("Reescanear", _palette.Accent, _rescanAction), 2, 0);
        panel.Controls.Add(folderActions, 0, 3);

        _deviceNameTextBox.Dock = DockStyle.Fill;
        _deviceNameTextBox.BorderStyle = BorderStyle.FixedSingle;
        _deviceNameTextBox.Text = _settings.DeviceName;
        panel.Controls.Add(WrapWithCaption("Nombre visible en la TV", _deviceNameTextBox), 0, 4);

        panel.Controls.Add(CreateOption("Compartir videos", () => _shareVideos, value => _shareVideos = value), 0, 5);
        panel.Controls.Add(CreateOption("Compartir audio", () => _shareAudio, value => _shareAudio = value), 0, 6);
        panel.Controls.Add(CreateOption("Compartir fotos", () => _shareImages, value => _shareImages = value), 0, 7);
        panel.Controls.Add(CreateOption("Reescanear automaticamente", () => _autoRescan, value => _autoRescan = value), 0, 8);
        return panel;
    }

    private Control BuildBehaviorPanel()
    {
        var panel = CreatePanel(9);
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        panel.Controls.Add(CreateHeader("Comportamiento"), 0, 0);
        panel.Controls.Add(CreateOption("Mantener PC despierto con DLNA activo", () => _keepAwake, value => _keepAwake = value), 0, 1);
        panel.Controls.Add(CreateOption("Iniciar servidor al abrir", () => _autoStartServer, value => _autoStartServer = value), 0, 2);
        panel.Controls.Add(CreateOption("Iniciar con Windows", () => _startWithWindows, value =>
        {
            _startWithWindows = value;
            RefreshOptionLines();
        }), 0, 3);
        panel.Controls.Add(CreateOption("Abrir minimizada", () => _startMinimized, value => _startMinimized = value, () => _startWithWindows), 0, 4);
        panel.Controls.Add(CreateOption("Cerrar a bandeja", () => _minimizeToTray, value => _minimizeToTray = value), 0, 5);
        panel.Controls.Add(CreateHeader("Actualizaciones"), 0, 6);
        panel.Controls.Add(BuildUpdatePanel(), 0, 7);
        return panel;
    }

    private Control BuildUpdatePanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0),
            BackColor = DialogBackground
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 72));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28));

        _updateStatusLabel.Text = $"Versión instalada: v{_updateService.CurrentVersion}";
        _updateStatusLabel.Dock = DockStyle.Fill;
        _updateStatusLabel.AutoEllipsis = true;
        _updateStatusLabel.TextAlign = ContentAlignment.MiddleLeft;
        _updateStatusLabel.Font = new Font("Segoe UI", 8.5f);
        _updateStatusLabel.Margin = new Padding(0);

        _updateActionLabel.Text = "Buscar";
        _updateActionLabel.Dock = DockStyle.Fill;
        _updateActionLabel.AutoSize = false;
        _updateActionLabel.BackColor = Color.Transparent;
        _updateActionLabel.ForeColor = _palette.AccentAlt;
        _updateActionLabel.Font = new Font("Segoe UI", 9.5f);
        _updateActionLabel.TextAlign = ContentAlignment.MiddleCenter;
        _updateActionLabel.Cursor = Cursors.Hand;
        _updateActionLabel.Tag = _palette.AccentAlt;
        _updateActionLabel.Margin = new Padding(0);
        _updateActionLabel.Click += async (_, _) => await HandleUpdateActionAsync();
        _updateActionLabel.MouseDown += (_, _) => _updateActionLabel.ForeColor = Blend(_palette.AccentAlt, _palette.Text, 0.35f);
        _updateActionLabel.MouseUp += (_, _) => _updateActionLabel.ForeColor = _palette.AccentAlt;
        _updateActionLabel.MouseLeave += (_, _) => _updateActionLabel.ForeColor = _palette.AccentAlt;

        panel.Controls.Add(_updateStatusLabel, 0, 0);
        panel.Controls.Add(_updateActionLabel, 1, 0);
        return panel;
    }

    private Control BuildFooter()
    {
        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 12, 0, 0),
            BackColor = DialogBackground
        };
        footer.Controls.Add(CreateTextCommand("Guardar", _palette.AccentAlt, SaveAndClose, fillParent: false));
        footer.Controls.Add(CreateTextCommand("Cancelar", _palette.MutedText, () => DialogResult = DialogResult.Cancel, fillParent: false));
        return footer;
    }

    private OptionLine CreateOption(string text, Func<bool> getValue, Action<bool> setValue, Func<bool>? canUse = null)
    {
        var line = new OptionLine(_palette)
        {
            Text = text,
            Dock = DockStyle.Fill,
            Checked = getValue(),
            Enabled = canUse?.Invoke() ?? true
        };
        line.Click += (_, _) =>
        {
            if (!(canUse?.Invoke() ?? true))
            {
                return;
            }

            setValue(!getValue());
            RefreshOptionLines();
        };
        line.GetValue = getValue;
        line.CanUse = canUse;
        _optionLines.Add(line);
        return line;
    }

    private Label CreateTextCommand(string text, Color color, Action action, bool fillParent = true)
    {
        var label = new Label
        {
            Text = text,
            Dock = fillParent ? DockStyle.Fill : DockStyle.None,
            AutoSize = false,
            Size = fillParent ? Size.Empty : new Size(126, 28),
            BackColor = Color.Transparent,
            ForeColor = color,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
            TextAlign = ContentAlignment.MiddleCenter,
            Cursor = Cursors.Hand,
            Tag = color,
            Margin = new Padding(0)
        };
        label.Click += (_, _) => action();
        label.MouseDown += (_, _) => label.ForeColor = Blend(color, _palette.Text, 0.35f);
        label.MouseUp += (_, _) => label.ForeColor = color;
        label.MouseLeave += (_, _) => label.ForeColor = color;
        return label;
    }

    private void RefreshOptionLines()
    {
        foreach (var line in _optionLines)
        {
            line.Checked = line.GetValue();
            line.Enabled = line.CanUse?.Invoke() ?? true;
            line.ApplyPalette(_palette);
        }
    }

    private void SaveAndClose()
    {
        var folders = _foldersList.Items.Cast<string>()
            .Where(folder => !string.IsNullOrWhiteSpace(folder))
            .Select(folder => folder.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (folders.Count == 0)
        {
            MessageBox.Show(this, "Agrega al menos una carpeta.", "VibeDLNA", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!_shareVideos && !_shareAudio && !_shareImages)
        {
            MessageBox.Show(this, "Deja al menos un tipo de archivo activo.", "VibeDLNA", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _settings.MediaFolders = folders;
        _settings.MediaFolder = folders[0];
        _settings.DeviceName = _deviceNameTextBox.Text.Trim();
        _settings.ShareVideos = _shareVideos;
        _settings.ShareAudio = _shareAudio;
        _settings.ShareImages = _shareImages;
        _settings.AutoRescanLibrary = _autoRescan;
        _settings.KeepAwake = _keepAwake;
        _settings.AutoStartServer = _autoStartServer;
        _settings.StartWithWindows = _startWithWindows;
        _settings.StartMinimized = _startMinimized;
        _settings.MinimizeToTray = _minimizeToTray;
        DialogResult = DialogResult.OK;
    }

    private void AddFolder()
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

        if (!_foldersList.Items.Cast<string>().Any(folder => string.Equals(folder, dialog.SelectedPath, StringComparison.OrdinalIgnoreCase)))
        {
            _foldersList.Items.Add(dialog.SelectedPath);
        }
    }

    private void RemoveSelectedFolder()
    {
        if (_foldersList.SelectedIndex >= 0)
        {
            _foldersList.Items.RemoveAt(_foldersList.SelectedIndex);
        }
    }

    private async Task HandleUpdateActionAsync()
    {
        if (_updateBusy)
        {
            return;
        }

        if (_availableUpdate is not null)
        {
            await InstallUpdateAsync(_availableUpdate);
            return;
        }

        await CheckForUpdatesAsync();
    }

    private async Task CheckForUpdatesAsync()
    {
        if (_updateBusy)
        {
            return;
        }

        _updateBusy = true;
        _availableUpdate = null;
        _updateActionLabel.Enabled = false;
        _updateActionLabel.Text = "Buscando...";
        _updateStatusLabel.Text = "Comprobando actualizaciones en GitHub...";

        try
        {
            var result = await _updateService.CheckAsync(_updateLifetime.Token);
            if (IsDisposed || Disposing)
            {
                return;
            }

            _availableUpdate = result.State == UpdateCheckState.Available
                ? result.Release
                : null;
            _updateStatusLabel.Text = result.Message;
            _updateActionLabel.Text = result.State == UpdateCheckState.Available
                ? "Actualizar"
                : "Buscar de nuevo";
        }
        catch (OperationCanceledException)
        {
            // Closing the dialog cancels an in-flight request.
        }
        catch
        {
            if (!IsDisposed && !Disposing)
            {
                _updateStatusLabel.Text = "No se pudo consultar GitHub en este momento.";
                _updateActionLabel.Text = "Buscar de nuevo";
            }
        }
        finally
        {
            if (!IsDisposed && !Disposing)
            {
                _updateBusy = false;
                _updateActionLabel.Enabled = true;
            }
        }
    }

    private async Task InstallUpdateAsync(GitHubReleaseInfo release)
    {
        if (_updateBusy)
        {
            return;
        }

        _updateBusy = true;
        _updateActionLabel.Enabled = false;
        _updateActionLabel.Text = "Descargando...";
        _updateStatusLabel.Text = $"Descargando v{release.Version} desde GitHub...";

        try
        {
            var result = await _updateService.DownloadAndStartAsync(release, _updateLifetime.Token);
            if (result.Started)
            {
                _updateStatusLabel.Text = result.Message;
                DialogResult = DialogResult.Abort;
                return;
            }

            _updateStatusLabel.Text = result.Message;
            _updateActionLabel.Text = "Reintentar";
        }
        catch (OperationCanceledException)
        {
            _updateStatusLabel.Text = "Actualización cancelada.";
            _updateActionLabel.Text = "Reintentar";
        }
        catch
        {
            _updateStatusLabel.Text = "No se pudo preparar la actualización.";
            _updateActionLabel.Text = "Reintentar";
        }
        finally
        {
            if (!IsDisposed && !Disposing)
            {
                _updateBusy = false;
                _updateActionLabel.Enabled = true;
            }
        }
    }

    private TableLayoutPanel CreatePanel(int rows) =>
        new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = rows,
            Padding = new Padding(0, 0, 18, 0),
            BackColor = DialogBackground
        };

    private Label CreateTitle(string text) =>
        new()
        {
            Text = text,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 18f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = DialogBackground,
            ForeColor = _palette.Text
        };

    private Label CreateHeader(string text) =>
        new()
        {
            Text = text,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = DialogBackground,
            ForeColor = _palette.Text
        };

    private Label CreateCaption(string text) =>
        new()
        {
            Text = text,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9f),
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = DialogBackground,
            ForeColor = _palette.Text
        };

    private Control WrapWithCaption(string caption, Control control)
    {
        var wrapper = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0),
            BackColor = DialogBackground
        };
        wrapper.RowStyles.Add(new RowStyle(SizeType.Absolute, 18));
        wrapper.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        wrapper.Controls.Add(CreateCaption(caption), 0, 0);
        wrapper.Controls.Add(control, 0, 1);
        return wrapper;
    }

    private void ApplyPalette(Control control)
    {
        control.BackColor = DialogBackground;
        control.ForeColor = _palette.Text;
        if (control is TextBox textBox)
        {
            textBox.BackColor = FieldBackground;
            textBox.ForeColor = _palette.Text;
        }
        else if (control is ListBox listBox)
        {
            listBox.BackColor = FieldBackground;
            listBox.ForeColor = _palette.Text;
        }
        else if (control is OptionLine optionLine)
        {
            optionLine.ApplyPalette(_palette);
        }
        else if (control is Label label && label.Tag is Color labelColor)
        {
            label.BackColor = Color.Transparent;
            label.ForeColor = labelColor;
        }

        foreach (Control child in control.Controls)
        {
            ApplyPalette(child);
        }
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        using var brush = new SolidBrush(DialogBackground);
        e.Graphics.FillRectangle(brush, ClientRectangle);
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

    private static AppSettings Clone(AppSettings source) =>
        new()
        {
            MediaFolder = source.MediaFolder,
            MediaFolders = source.MediaFolders.ToList(),
            DeviceName = source.DeviceName,
            AutoStartServer = source.AutoStartServer,
            AutoRescanLibrary = source.AutoRescanLibrary,
            ShareVideos = source.ShareVideos,
            ShareAudio = source.ShareAudio,
            ShareImages = source.ShareImages,
            KeepAwake = source.KeepAwake,
            StartWithWindows = source.StartWithWindows,
            MinimizeToTray = source.MinimizeToTray,
            StartMinimized = source.StartMinimized,
            ThemeMode = source.ThemeMode,
            Port = source.Port,
            Uuid = source.Uuid
        };

    private sealed class OptionLine : Control
    {
        private AppPalette _palette;

        public OptionLine(AppPalette palette)
        {
            _palette = palette;
            SetStyle(
                ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw
                | ControlStyles.UserPaint
                | ControlStyles.SupportsTransparentBackColor,
                true);
            Height = 34;
            Cursor = Cursors.Hand;
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            Margin = new Padding(0);
            TabStop = true;
        }

        public Func<bool> GetValue { get; set; } = () => false;
        public Func<bool>? CanUse { get; set; }
        public bool Checked { get; set; }

        public void ApplyPalette(AppPalette palette)
        {
            _palette = palette;
            BackColor = Color.Transparent;
            ForeColor = palette.Text;
            Invalidate();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            base.OnPaintBackground(e);
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
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var enabled = Enabled;
            var accent = Checked ? _palette.AccentAlt : _palette.MutedText;
            using var pen = new Pen(enabled ? accent : _palette.MutedText, Checked ? 2.4f : 1.6f)
            {
                StartCap = System.Drawing.Drawing2D.LineCap.Round,
                EndCap = System.Drawing.Drawing2D.LineCap.Round
            };
            if (Checked)
            {
                e.Graphics.DrawLines(pen, new[]
                {
                    new Point(13, Height / 2),
                    new Point(18, Height / 2 + 5),
                    new Point(28, Height / 2 - 6)
                });
            }
            else
            {
                e.Graphics.DrawLine(pen, 14, Height / 2, 27, Height / 2);
            }

            var textBounds = new Rectangle(44, 0, Width - 44, Height);
            TextRenderer.DrawText(
                e.Graphics,
                Text,
                Font,
                textBounds,
                enabled ? _palette.Text : _palette.MutedText,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        }
    }
}
