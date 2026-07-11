namespace FolderDlnaServer;

internal static class AppLog
{
    private const long MaximumLogBytes = 1024 * 1024;
    private static readonly object SyncRoot = new();

    public static string LogPath => Path.Combine(SettingsService.AppDataFolder, "VibeDLNA.log");

    public static void Write(string message)
    {
        try
        {
            lock (SyncRoot)
            {
                Directory.CreateDirectory(SettingsService.AppDataFolder);
                RotateIfNeeded();
                File.AppendAllText(LogPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // Logging must never interrupt the media server.
        }
    }

    private static void RotateIfNeeded()
    {
        if (!File.Exists(LogPath) || new FileInfo(LogPath).Length < MaximumLogBytes)
        {
            return;
        }

        var previousLog = Path.Combine(SettingsService.AppDataFolder, "VibeDLNA.previous.log");
        File.Move(LogPath, previousLog, overwrite: true);
    }
}
