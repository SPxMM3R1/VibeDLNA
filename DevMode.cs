namespace FolderDlnaServer;

/// <summary>
/// Modo de desarrollo para probar sin tocar la instalación real. Solo existe en compilaciones
/// Debug y solo si la variable de entorno <c>VIBEDLNA_DEV=1</c> está puesta. Cambia el mutex,
/// la carpeta de datos (%APPDATA%\VibeDLNA-dev), la caché de miniaturas, el puerto (8211) y el
/// nombre que ve la TV. En Release siempre es falso y no se puede activar.
/// </summary>
internal static class DevMode
{
    public const int DevPort = 8211;
    public const string ProductionFolderName = "VibeDLNA";
    public const string DevFolderName = "VibeDLNA-dev";
    public const string ProductionSingleInstanceName = "VibeDLNA.SingleInstance";
    public const string DevSingleInstanceName = "VibeDLNA.SingleInstance.Dev";
    public const string DevDeviceName = "VibeDLNA DEV";

    /// <summary>true solo en Debug con VIBEDLNA_DEV=1.</summary>
    public static bool IsEnabled { get; } = Detect();

    public static string AppDataFolderName => IsEnabled ? DevFolderName : ProductionFolderName;

    public static string SingleInstanceName => IsEnabled ? DevSingleInstanceName : ProductionSingleInstanceName;

    private static bool Detect()
    {
#if DEBUG
        return Environment.GetEnvironmentVariable("VIBEDLNA_DEV") == "1";
#else
        return false;
#endif
    }
}
