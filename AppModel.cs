namespace FolderDlnaServer;

/// <summary>Las cinco secciones de la ventana, en el orden del riel.</summary>
internal enum AppSection
{
    Inicio,
    Carpetas,
    Reproductores,
    Actividad,
    Opciones
}

/// <summary>
/// Foto del estado que la ventana pinta en cada actualización (una vez por segundo y tras cada acción).
/// </summary>
internal sealed record AppView(
    bool Running,
    AppSettings Settings,
    string HostAddress,
    string DescriptionUrl,
    LibraryOverview Library,
    IReadOnlyList<WatchingNow> Watching,
    IReadOnlyList<KnownPlayer> Players,
    IReadOnlyList<ContinueWatchingEntry> Resume,
    IReadOnlyList<ActivityEntry> Log,
    DateTimeOffset Now,
    string LogPath,
    string Version,
    string UpdateStatus,
    bool UpdateAvailable,
    bool UpdateBusy);

/// <summary>Acciones que las páginas piden a la ventana principal.</summary>
internal interface IAppActions
{
    void Navigate(AppSection section);

    void ToggleServer();

    void CopyAddress();

    void Rescan();

    void AddFolder();

    void RemoveFolder(string folder);

    /// <summary>Cambia una opción, la guarda al instante y reinicia el servidor si hace falta.</summary>
    void ChangeSettings(Action<AppSettings> change, bool restartServer);

    void CheckForUpdates();

    void InstallUpdate();

    /// <summary>Muestra un aviso breve abajo de la ventana.</summary>
    void Notify(string text);
}
