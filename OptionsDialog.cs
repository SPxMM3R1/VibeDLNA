namespace FolderDlnaServer;

internal sealed class OptionsDialog : Form
{
    private readonly AppSettings _settings;
    private readonly AppPalette _palette;
    private readonly Action _rescanAction;
    private readonly ListBox _foldersList = new();
    private readonly TextBox _deviceNameTextBox = new();
    private readonly FloatingSwitchRow _autoRescanSwitch = new();
    private readonly FloatingSwitchRow _shareVideosSwitch = new();
    private readonly FloatingSwitchRow _shareAudioSwitch = new();
    private readonly FloatingSwitchRow _shareImagesSwitch = new();
    private readonly FloatingSwitchRow _keepAwakeSwitch = new();
    private readonly FloatingSwitchRow _autoStartServerSwitch = new();
    private readonly FloatingSwitchRow _startWithWindowsSwitch = new();
    private readonly FloatingSwitchRow _startMinimizedSwitch = new();
    private readonly FloatingSwitchRow _minimizeToTraySwitch = new();

    private Color DialogBackground => _palette.IsDark
        ? Color.FromArgb(94, 107, 136)
        : _palette.Window;

    private Color FieldBackground => _palette.IsDark
        ? Color.FromArgb(70, 82, 108)
        : _palette.Elevated;

    public OptionsDialog(AppSettings settings, AppPalette palette, Action rescanAction)
    {
        _settings = Clone(settings);
        _palette = palette;
        _rescanAction = rescanAction;

        Text = "Opciones";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(760, 620);
        MinimumSize = new Size(720, 560);
        Font = new Font("Segoe UI", 9.5f);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        BuildUi();
        ApplyPalette(this);
    }

    public AppSettings Settings => _settings;

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(18)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 1));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        Controls.Add(root);

        var title = new Label
        {
            Text = "Opciones",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 18f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };
        root.Controls.Add(title, 0, 0);

        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48));
        root.Controls.Add(body, 0, 1);

        body.Controls.Add(BuildLibraryPanel(), 0, 0);
        body.Controls.Add(BuildBehaviorPanel(), 1, 0);

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 10, 0, 0)
        };
        root.Controls.Add(footer, 0, 3);

        var saveButton = CreateActionLabel("✓", "Guardar", _palette.AccentAlt);
        saveButton.Click += (_, _) => SaveAndClose();
        var cancelButton = CreateActionLabel("×", "Cancelar", _palette.MutedText);
        cancelButton.Click += (_, _) => DialogResult = DialogResult.Cancel;
        footer.Controls.Add(saveButton);
        footer.Controls.Add(cancelButton);
    }

    private Control BuildLibraryPanel()
    {
        var panel = CreatePanel(9);
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

        panel.Controls.Add(CreateHeader("Biblioteca"), 0, 0);
        panel.Controls.Add(CreateCaption("Carpetas compartidas"), 0, 1);

        _foldersList.Dock = DockStyle.Fill;
        _foldersList.BorderStyle = BorderStyle.FixedSingle;
        foreach (var folder in _settings.MediaFolders)
        {
            _foldersList.Items.Add(folder);
        }

        panel.Controls.Add(_foldersList, 0, 2);

        var folderButtons = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Margin = new Padding(0),
            Padding = new Padding(0, 4, 0, 0)
        };
        folderButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
        folderButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28));
        folderButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
        var addButton = CreateActionLabel("+", "Agregar", _palette.AccentAlt);
        addButton.Click += (_, _) => AddFolder();
        var removeButton = CreateActionLabel("−", "Quitar", _palette.MutedText);
        removeButton.Click += (_, _) => RemoveSelectedFolder();
        var rescanButton = CreateActionLabel("↻", "Reescanear", _palette.Accent);
        rescanButton.Click += (_, _) => _rescanAction();
        PrepareInlineAction(addButton);
        PrepareInlineAction(removeButton);
        PrepareInlineAction(rescanButton);
        folderButtons.Controls.Add(addButton, 0, 0);
        folderButtons.Controls.Add(removeButton, 1, 0);
        folderButtons.Controls.Add(rescanButton, 2, 0);
        panel.Controls.Add(folderButtons, 0, 3);

        _deviceNameTextBox.Dock = DockStyle.Fill;
        _deviceNameTextBox.BorderStyle = BorderStyle.FixedSingle;
        _deviceNameTextBox.Text = _settings.DeviceName;
        panel.Controls.Add(WrapWithCaption("Nombre visible en la TV", _deviceNameTextBox), 0, 4);

        panel.Controls.Add(CreateSwitchRow(_shareVideosSwitch, "Compartir videos", _settings.ShareVideos), 0, 5);
        panel.Controls.Add(CreateSwitchRow(_shareAudioSwitch, "Compartir audio", _settings.ShareAudio), 0, 6);
        panel.Controls.Add(CreateSwitchRow(_shareImagesSwitch, "Compartir fotos", _settings.ShareImages), 0, 7);
        panel.Controls.Add(CreateSwitchRow(_autoRescanSwitch, "Reescanear automaticamente", _settings.AutoRescanLibrary), 0, 8);
        return panel;
    }

    private Control BuildBehaviorPanel()
    {
        var panel = CreatePanel(7);
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        panel.Controls.Add(CreateHeader("Comportamiento"), 0, 0);
        panel.Controls.Add(CreateSwitchRow(_keepAwakeSwitch, "Mantener PC despierto con DLNA activo", _settings.KeepAwake), 0, 1);
        panel.Controls.Add(CreateSwitchRow(_autoStartServerSwitch, "Iniciar servidor al abrir", _settings.AutoStartServer), 0, 2);
        panel.Controls.Add(CreateSwitchRow(_startWithWindowsSwitch, "Iniciar con Windows", _settings.StartWithWindows), 0, 3);
        panel.Controls.Add(CreateSwitchRow(_startMinimizedSwitch, "Abrir minimizada", _settings.StartMinimized), 0, 4);
        panel.Controls.Add(CreateSwitchRow(_minimizeToTraySwitch, "Cerrar a bandeja", _settings.MinimizeToTray), 0, 5);
        _startWithWindowsSwitch.CheckedChanged += (_, _) => _startMinimizedSwitch.Enabled = _startWithWindowsSwitch.Checked;
        _startMinimizedSwitch.Enabled = _startWithWindowsSwitch.Checked;
        return panel;
    }

    private void SaveAndClose()
    {
        var folders = _foldersList.Items.Cast<string>()
            .Where(Directory.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (folders.Count == 0)
        {
            MessageBox.Show(this, "Agrega al menos una carpeta valida.", "VibeDLNA", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!_shareVideosSwitch.Checked && !_shareAudioSwitch.Checked && !_shareImagesSwitch.Checked)
        {
            MessageBox.Show(this, "Deja al menos un tipo de archivo activo.", "VibeDLNA", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _settings.MediaFolders = folders;
        _settings.MediaFolder = folders[0];
        _settings.DeviceName = _deviceNameTextBox.Text.Trim();
        _settings.ShareVideos = _shareVideosSwitch.Checked;
        _settings.ShareAudio = _shareAudioSwitch.Checked;
        _settings.ShareImages = _shareImagesSwitch.Checked;
        _settings.AutoRescanLibrary = _autoRescanSwitch.Checked;
        _settings.KeepAwake = _keepAwakeSwitch.Checked;
        _settings.AutoStartServer = _autoStartServerSwitch.Checked;
        _settings.StartWithWindows = _startWithWindowsSwitch.Checked;
        _settings.StartMinimized = _startMinimizedSwitch.Checked;
        _settings.MinimizeToTray = _minimizeToTraySwitch.Checked;
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

    private TableLayoutPanel CreatePanel(int rows) =>
        new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = rows,
            Padding = new Padding(0, 0, 18, 0)
        };

    private Label CreateHeader(string text) =>
        new()
        {
            Text = text,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };

    private Label CreateCaption(string text) =>
        new()
        {
            Text = text,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9f),
            TextAlign = ContentAlignment.MiddleLeft
        };

    private Control WrapWithCaption(string caption, Control control)
    {
        var wrapper = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0)
        };
        wrapper.RowStyles.Add(new RowStyle(SizeType.Absolute, 18));
        wrapper.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        wrapper.Controls.Add(CreateCaption(caption), 0, 0);
        wrapper.Controls.Add(control, 0, 1);
        return wrapper;
    }

    private Label CreateActionLabel(string icon, string text, Color accent)
    {
        var label = new Label
        {
            Text = $"{icon}  {text}",
            AutoSize = false,
            Width = Math.Max(96, TextRenderer.MeasureText(text, Font).Width + 42),
            Height = 34,
            Margin = new Padding(0, 0, 14, 0),
            BackColor = Color.Transparent,
            ForeColor = accent,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
            Cursor = Cursors.Hand,
            Tag = accent
        };
        label.MouseDown += (_, _) =>
        {
            label.BackColor = Color.FromArgb(_palette.IsDark ? 42 : 24, accent);
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
        return label;
    }

    private static void PrepareInlineAction(Label label)
    {
        label.Dock = DockStyle.Fill;
        label.Width = 0;
        label.Margin = new Padding(0, 0, 8, 0);
    }

    private Control CreateSwitchRow(FloatingSwitchRow switchRow, string text, bool isChecked)
    {
        switchRow.Text = text;
        switchRow.Checked = isChecked;
        switchRow.Dock = DockStyle.Fill;
        switchRow.HostBackColor = DialogBackground;
        switchRow.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
        switchRow.ApplyPalette(_palette);
        return switchRow;
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
        else if (control is FloatingSwitchRow switchRow)
        {
            switchRow.HostBackColor = DialogBackground;
            switchRow.ApplyPalette(_palette);
        }
        else if (control is Label label && label.Tag is Color labelColor)
        {
            label.ForeColor = labelColor;
        }

        foreach (Control child in control.Controls)
        {
            ApplyPalette(child);
        }
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
}
