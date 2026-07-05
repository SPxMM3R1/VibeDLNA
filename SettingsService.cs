using System.Text.Json;
using Microsoft.Win32;

namespace FolderDlnaServer;

internal static class SettingsService
{
    private const string RunValueName = "FolderDlnaServer";
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    public static string AppDataFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FolderDlnaServer");

    public static string SettingsPath => Path.Combine(AppDataFolder, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions) ?? new AppSettings();
                Normalize(settings);
                settings.StartWithWindows = IsStartupEnabled();
                return settings;
            }
        }
        catch
        {
            // If the settings file is damaged, fall back to defaults and let the user save again.
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
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, SerializerOptions));
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
        }
        else
        {
            key.DeleteValue(RunValueName, throwOnMissingValue: false);
        }
    }

    public static bool IsStartupEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(RunValueName) is string value && value.Contains(Application.ExecutablePath, StringComparison.OrdinalIgnoreCase);
    }

    private static void Normalize(AppSettings settings)
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
            settings.DeviceName = $"Carpeta DLNA - {Environment.MachineName}";
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
