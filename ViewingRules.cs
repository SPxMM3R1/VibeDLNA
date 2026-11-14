namespace FolderDlnaServer;

/// <summary>
/// Progreso aproximado de un video deducido de un pedido de rango (contrato §D).
/// </summary>
internal sealed record PlaybackProgress(double Percent, TimeSpan? Position);

/// <summary>
/// Reglas de §D sin estado ni disco: posición por rango, poda de «seguir viendo» y de
/// reproductores vistos, y las frases del registro de actividad. Funciones puras y testeables.
/// </summary>
internal static class ViewingRules
{
    /// <summary>Un reproductor «está viendo» si pidió un rango de ese archivo en estos segundos.</summary>
    public static readonly TimeSpan WatchingWindow = TimeSpan.FromSeconds(15);

    /// <summary>Pedidos del mismo archivo y reproductor dentro de esta ventana forman un solo registro.</summary>
    public static readonly TimeSpan LogGroupWindow = TimeSpan.FromSeconds(60);

    /// <summary>«Seguir viendo» conserva entradas recientes durante este tiempo.</summary>
    public static readonly TimeSpan ContinueWatchingRetention = TimeSpan.FromDays(7);

    /// <summary>Reproductores vistos se recuerdan durante este tiempo.</summary>
    public static readonly TimeSpan KnownPlayerRetention = TimeSpan.FromDays(30);

    /// <summary>Máximo de entradas en «seguir viendo».</summary>
    public const int ContinueWatchingLimit = 50;

    /// <summary>Por encima de este porcentaje el video se considera terminado y sale de «seguir viendo».</summary>
    public const double FinishedPercent = 95;

    /// <summary>
    /// Posición aproximada = inicio del rango / tamaño del archivo × duración (§D). Sin duración
    /// medida, solo hay porcentaje.
    /// </summary>
    public static PlaybackProgress Measure(long rangeStart, long fileSize, TimeSpan? duration)
    {
        if (fileSize <= 0)
        {
            return new PlaybackProgress(0, null);
        }

        var fraction = Math.Clamp((double)rangeStart / fileSize, 0, 1);
        TimeSpan? position = duration is { } total && total > TimeSpan.Zero
            ? TimeSpan.FromTicks((long)Math.Round(total.Ticks * fraction))
            : null;
        return new PlaybackProgress(fraction * 100, position);
    }

    /// <summary>
    /// Quita de «seguir viendo» lo viejo (más de 7 días) y lo terminado (más del 95 %),
    /// ordena por lo más reciente y deja como máximo 50 entradas.
    /// </summary>
    public static List<ContinueWatchingEntry> PruneContinueWatching(
        IEnumerable<ContinueWatchingEntry> entries,
        DateTimeOffset now) =>
        entries
            .Where(entry => now - entry.UpdatedAt <= ContinueWatchingRetention)
            .Where(entry => entry.Percent <= FinishedPercent)
            .OrderByDescending(entry => entry.UpdatedAt)
            .Take(ContinueWatchingLimit)
            .ToList();

    /// <summary>Quita los reproductores que no se vieron en los últimos 30 días.</summary>
    public static List<KnownPlayer> PrunePlayers(IEnumerable<KnownPlayer> players, DateTimeOffset now) =>
        players
            .Where(player => now - player.LastSeen <= KnownPlayerRetention)
            .OrderByDescending(player => player.LastSeen)
            .ToList();

    /// <summary>Frase del registro: «192.168.1.34 abrió D:\Películas (8 de 642)».</summary>
    public static string FolderOpened(string clientName, string folderPath, int returned, int total) =>
        $"{clientName} abrió {folderPath} ({returned} de {total})";

    /// <summary>Frase del registro: «Pixel 8 pidió un rango fuera del archivo · Amor.mp4 (416)».</summary>
    public static string RangeOutOfBounds(string clientName, string fileName) =>
        $"{clientName} pidió un rango fuera del archivo · {fileName} (416)";

    /// <summary>Frase del registro: «Hisense 65A6 reproduce Dune (2160p HEVC)».</summary>
    public static string VideoStarted(string clientName, string title, string? quality) =>
        quality is null
            ? $"{clientName} reproduce {title}"
            : $"{clientName} reproduce {title} ({quality})";
}
