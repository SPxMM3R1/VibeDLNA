namespace FolderDlnaServer;

internal sealed class AppSettings
{
    public string MediaFolder { get; set; } = string.Empty;

    public string DeviceName { get; set; } = $"Carpeta DLNA - {Environment.MachineName}";

    public bool AutoStartServer { get; set; } = true;

    public bool StartWithWindows { get; set; }

    public bool MinimizeToTray { get; set; } = true;

    public bool StartMinimized { get; set; } = true;

    public string ThemeMode { get; set; } = "System";

    public int Port { get; set; }

    public string Uuid { get; set; } = Guid.NewGuid().ToString("D");
}
