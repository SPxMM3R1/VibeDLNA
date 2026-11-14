namespace FolderDlnaServer;

/// <summary>
/// Estado de una carpeta compartida para la ventana (1.1.0): cuántos archivos hay de cada tipo,
/// su tamaño y cuántas miniaturas están listas.
/// </summary>
internal sealed record LibraryFolderStats(
    string Path,
    bool Available,
    bool Counted,
    int Videos,
    int Audio,
    int Images,
    long Bytes,
    int ThumbnailsReady,
    int ThumbnailsTotal,
    bool Scanning)
{
    /// <summary>Archivos compartidos de la carpeta (videos, canciones e imágenes).</summary>
    public int Files => Videos + Audio + Images;

    /// <summary>Porcentaje de miniaturas listas; 100 si no hay nada que preparar.</summary>
    public int Percent => ThumbnailsTotal == 0
        ? 100
        : (int)Math.Floor(ThumbnailsReady * 100.0 / ThumbnailsTotal);
}

/// <summary>Resumen de la biblioteca compartida que muestra la ventana.</summary>
internal sealed record LibraryOverview(
    bool Counted,
    bool ThumbnailsRunning,
    IReadOnlyList<LibraryFolderStats> Folders,
    int Videos,
    int Audio,
    int Images,
    int ThumbnailsReady,
    int ThumbnailsTotal)
{
    /// <summary>Sin conteo todavía: la ventana muestra guiones en vez de ceros.</summary>
    public static LibraryOverview Empty { get; } = new(
        false,
        false,
        Array.Empty<LibraryFolderStats>(),
        0,
        0,
        0,
        0,
        0);
}

/// <summary>
/// Cuenta la biblioteca en segundo plano (solo listados de carpetas y tamaños, nunca abre los
/// archivos) y sigue cuántas miniaturas están listas, consultando la caché ya indexada. Solo
/// cuenta lo que el servidor comparte según los tipos activos, igual que el calentamiento.
/// </summary>
internal sealed class LibraryTracker
{
    private static readonly TimeSpan BusyInterval = TimeSpan.FromSeconds(1.5);
    private static readonly TimeSpan IdleInterval = TimeSpan.FromSeconds(10);

    private readonly ThumbnailCache _thumbnails;
    private readonly Func<bool> _thumbnailsRunning;
    private readonly object _sync = new();
    private IReadOnlyList<string> _roots = Array.Empty<string>();
    private List<Entry> _entries = new();
    private bool _counted;
    private int _generation;
    private CancellationTokenSource? _cancellation;

    public LibraryTracker(ThumbnailCache thumbnails, Func<bool> thumbnailsRunning)
    {
        _thumbnails = thumbnails;
        _thumbnailsRunning = thumbnailsRunning;
    }

    /// <summary>Empieza (o reinicia) el conteo de las carpetas indicadas.</summary>
    public void Begin(
        IReadOnlyList<string> roots,
        bool shareVideos,
        bool shareAudio,
        bool shareImages,
        CancellationToken serverCancellation)
    {
        CancellationToken token;
        int generation;
        lock (_sync)
        {
            _cancellation?.Cancel();
            _cancellation = CancellationTokenSource.CreateLinkedTokenSource(serverCancellation);
            token = _cancellation.Token;
            generation = ++_generation;
            _roots = roots.ToList();
            _entries = new List<Entry>();
            _counted = false;
        }

        _ = Task.Run(() => CountAndTrackAsync(roots.ToList(), shareVideos, shareAudio, shareImages, generation, token));
    }

    /// <summary>Detiene el seguimiento al apagar el servidor.</summary>
    public void Stop()
    {
        lock (_sync)
        {
            _cancellation?.Cancel();
            _generation++;
            _entries = new List<Entry>();
            _counted = false;
        }
    }

    /// <summary>Estado actual. Barato: copia la última cuenta sin recorrer el disco.</summary>
    public LibraryOverview Snapshot()
    {
        List<Entry> entries;
        IReadOnlyList<string> roots;
        bool counted;
        lock (_sync)
        {
            entries = _entries;
            roots = _roots;
            counted = _counted;
        }

        var running = _thumbnailsRunning();
        var folders = new List<LibraryFolderStats>();
        for (var index = 0; index < roots.Count; index++)
        {
            var root = roots[index];
            var available = Directory.Exists(root);
            var mine = entries.Where(entry => entry.RootIndex == index).ToList();
            var ready = mine.Count(entry => entry.Ready);
            var total = mine.Count;
            folders.Add(new LibraryFolderStats(
                root,
                available,
                counted,
                mine.Count(entry => entry.Kind == MediaKind.Video),
                mine.Count(entry => entry.Kind == MediaKind.Audio),
                mine.Count(entry => entry.Kind == MediaKind.Image),
                mine.Sum(entry => entry.Length),
                ready,
                total,
                available && (!counted || (running && ready < total))));
        }

        return new LibraryOverview(
            counted,
            running,
            folders,
            entries.Count(entry => entry.Kind == MediaKind.Video),
            entries.Count(entry => entry.Kind == MediaKind.Audio),
            entries.Count(entry => entry.Kind == MediaKind.Image),
            entries.Count(entry => entry.Ready),
            entries.Count);
    }

    private async Task CountAndTrackAsync(
        IReadOnlyList<string> roots,
        bool shareVideos,
        bool shareAudio,
        bool shareImages,
        int generation,
        CancellationToken token)
    {
        try
        {
            var entries = new List<Entry>();
            for (var index = 0; index < roots.Count && !token.IsCancellationRequested; index++)
            {
                foreach (var file in ThumbnailCache.EnumerateMediaFiles(new[] { roots[index] }, token))
                {
                    if (!MediaTypes.TryGet(file, out var mediaType)
                        || !MediaTypes.IsAllowed(mediaType, shareVideos, shareAudio, shareImages))
                    {
                        continue;
                    }

                    entries.Add(new Entry(index, mediaType.Kind, FileLengthOf(file), file));
                }
            }

            lock (_sync)
            {
                if (generation != _generation || token.IsCancellationRequested)
                {
                    return;
                }

                _entries = entries;
                _counted = true;
            }

            // Mientras el calentamiento trabaja, se revisa cada pocos segundos cuántas miniaturas
            // ya están listas; cuando termina, la revisión se espacia.
            while (!token.IsCancellationRequested && generation == Volatile.Read(ref _generation))
            {
                RefreshReady(entries, generation);
                try
                {
                    await Task.Delay(_thumbnailsRunning() ? BusyInterval : IdleInterval, token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
        catch
        {
            // El conteo es informativo: si algo falla, la ventana muestra lo que haya.
        }
    }

    private void RefreshReady(List<Entry> entries, int generation)
    {
        foreach (var entry in entries)
        {
            if (generation != Volatile.Read(ref _generation))
            {
                return;
            }

            entry.Ready = _thumbnails.TryGetCached(entry.Path) is not null;
        }
    }

    private static long FileLengthOf(string file)
    {
        try
        {
            return new FileInfo(file).Length;
        }
        catch
        {
            return 0;
        }
    }

    private sealed class Entry
    {
        public Entry(int rootIndex, MediaKind kind, long length, string path)
        {
            RootIndex = rootIndex;
            Kind = kind;
            Length = length;
            Path = path;
        }

        public int RootIndex { get; }

        public MediaKind Kind { get; }

        public long Length { get; }

        public string Path { get; }

        public bool Ready { get; set; }
    }
}
