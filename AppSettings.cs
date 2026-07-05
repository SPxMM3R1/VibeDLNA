namespace FolderDlnaServer;

internal sealed class AppSettings
{
    public string MediaFolder { get; set; } = string.Empty;

    public List<string> MediaFolders { get; set; } = new();

    public string DeviceName { get; set; } = $"Carpeta DLNA - {Environment.MachineName}";

    public bool AutoStartServer { get; set; } = true;

    public bool AutoRescanLibrary { get; set; } = true;

    public bool ShareVideos { get; set; } = true;

    public bool ShareAudio { get; set; } = true;

    public bool ShareImages { get; set; } = true;

    public bool KeepAwake { get; set; }

    public bool StartWithWindows { get; set; }

    public bool MinimizeToTray { get; set; } = true;

    public bool StartMinimized { get; set; } = true;

    public string ThemeMode { get; set; } = "System";

    public int Port { get; set; }

    public string Uuid { get; set; } = Guid.NewGuid().ToString("D");
}
