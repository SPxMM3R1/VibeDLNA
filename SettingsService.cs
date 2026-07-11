using System.Text.Json;
using Microsoft.Win32;

namespace FolderDlnaServer;

internal static class SettingsService
{
    private const string RunValueName = "VibeDLNA";
    private const string LegacyRunValueName = "FolderDlnaServer";
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    public static string AppDataFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VibeDLNA");

    private static string LegacyAppDataFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FolderDlnaServer");

    public static string SettingsPath => Path.Combine(AppDataFolder, "settings.json");

    public static string? LastLoadWarning { get; private set; }

    private static string LegacySettingsPath => Path.Combine(LegacyAppDataFolder, "settings.json");

    public static AppSettings Load()
    {
        LastLoadWarning = null;
        try
        {
            var path = File.Exists(SettingsPath) ? SettingsPath : LegacySettingsPath;
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions) ?? new AppSettings();
                Normalize(settings);
                settings.StartWithWindows = IsStartupEnabled();
                return settings;
            }
        }
        catch (Exception ex)
        {
            TryBackupInvalidSettings();
            LastLoadWarning = $"La configuracion no se pudo leer y se restauraron valores seguros: {ex.Message}";
        }

        var defaults = new AppSettings
        {
            StartWithWindows = IsStartupEnabled()
        };
        Normalize(defaults);
        return defaults;
    }

    public static void Save(AppSettings settings)
    {
        Normalize(settings);
        Directory.CreateDirectory(AppDataFolder);
        var json = JsonSerializer.Serialize(settings, SerializerOptions);
        var temporaryPath = SettingsPath + ".tmp";
        File.WriteAllText(temporaryPath, json);
        File.Move(temporaryPath, SettingsPath, overwrite: true);
        SyncStartup(settings);
    }

    public static void SyncStartup(AppSettings settings)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);

        if (settings.StartWithWindows)
        {
            var executablePath = Application.ExecutablePath;
            var command = settings.StartMinimized
                ? $"\"{executablePath}\" --minimized"
                : $"\"{executablePath}\"";
            key.SetValue(RunValueName, command, RegistryValueKind.String);
            key.DeleteValue(LegacyRunValueName, throwOnMissingValue: false);
        }
        else
        {
            key.DeleteValue(RunValueName, throwOnMissingValue: false);
            key.DeleteValue(LegacyRunValueName, throwOnMissingValue: false);
        }
    }

    public static bool IsStartupEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        if (key?.GetValue(RunValueName) is string value)
        {
            return value.Contains("VibeDLNA", StringComparison.OrdinalIgnoreCase)
                || value.Contains(Application.ExecutablePath, StringComparison.OrdinalIgnoreCase);
        }

        return key?.GetValue(LegacyRunValueName) is string;
    }

    public static void RefreshStartupRegistration(AppSettings settings)
    {
        if (settings.StartWithWindows)
        {
            SyncStartup(settings);
        }
    }

    internal static string? GetStartupCommand()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(RunValueName) as string;
    }

    private static void TryBackupInvalidSettings()
    {
        try
        {
            var source = File.Exists(SettingsPath) ? SettingsPath : LegacySettingsPath;
            if (!File.Exists(source))
            {
                return;
            }

            Directory.CreateDirectory(AppDataFolder);
            var backup = Path.Combine(AppDataFolder, $"settings.invalid-{DateTime.Now:yyyyMMdd-HHmmss}.json");
            File.Copy(source, backup, overwrite: false);
        }
        catch
        {
            // A backup is best effort; loading defaults must still succeed.
        }
    }

    internal static void Normalize(AppSettings settings)
    {
        settings.MediaFolders ??= new List<string>();

        if (!string.IsNullOrWhiteSpace(settings.MediaFolder)
            && !settings.MediaFolders.Any(folder => string.Equals(folder, settings.MediaFolder, StringComparison.OrdinalIgnoreCase)))
        {
            settings.MediaFolders.Insert(0, settings.MediaFolder);
        }

        settings.MediaFolders = settings.MediaFolders
            .Where(folder => !string.IsNullOrWhiteSpace(folder))
            .Select(folder => folder.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        settings.MediaFolder = settings.MediaFolders.FirstOrDefault() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(settings.DeviceName))
        {
            settings.DeviceName = $"VibeDLNA - {Environment.MachineName}";
        }
        else if (settings.DeviceName.StartsWith("Carpeta DLNA - ", StringComparison.OrdinalIgnoreCase))
        {
            settings.DeviceName = "VibeDLNA - " + settings.DeviceName["Carpeta DLNA - ".Length..];
        }
        else if (settings.DeviceName.StartsWith("Folder DLNA - ", StringComparison.OrdinalIgnoreCase))
        {
            settings.DeviceName = "VibeDLNA - " + settings.DeviceName["Folder DLNA - ".Length..];
        }

        if (!settings.ShareVideos && !settings.ShareAudio && !settings.ShareImages)
        {
            settings.ShareVideos = true;
        }

        if (!Guid.TryParse(settings.Uuid, out _))
        {
            settings.Uuid = Guid.NewGuid().ToString("D");
        }

        if (settings.Port is < 0 or > 65535)
        {
            settings.Port = 0;
        }

        if (!Enum.TryParse<AppThemeMode>(settings.ThemeMode, ignoreCase: true, out _))
        {
            settings.ThemeMode = AppThemeMode.System.ToString();
        }
    }
}
