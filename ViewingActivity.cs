using System.Text.Encodings.Web;
using System.Text.Json;

namespace FolderDlnaServer;

/// <summary>
/// Entrada de «seguir viendo» de un reproductor de VibeDLNA para un archivo (contrato §D).
/// Se guarda en %APPDATA%\VibeDLNA\seguir-viendo.json.
/// </summary>
internal sealed class ContinueWatchingEntry
{
    public string PlayerId { get; set; } = string.Empty;

    public string PlayerName { get; set; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    /// <summary>Último porcentaje aproximado (0 a 100).</summary>
    public double Percent { get; set; }

    public long? PositionMs { get; set; }

    public long? DurationMs { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// Reproductor visto en la red. Se guarda en %APPDATA%\VibeDLNA\reproductores.json.
/// </summary>
internal sealed class KnownPlayer
{
    public string Key { get; set; } = string.Empty;

    public string Kind { get; set; } = string.Empty;

    public string? PlayerId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Model { get; set; }

    public string? DeviceName { get; set; }

    public string Ip { get; set; } = string.Empty;

    public DateTimeOffset LastSeen { get; set; }
}

/// <summary>
/// Video que un reproductor está pidiendo ahora mismo («Viendo ahora»).
/// </summary>
internal sealed record WatchingNow(
    string ClientName,
    string FilePath,
    string Title,
    TimeSpan? Position,
    TimeSpan? Duration,
    double Percent,
    DateTimeOffset LastSeen);

/// <summary>
/// API interna de actividad de reproducción (contrato §D). Deduce «viendo ahora», «seguir
/// viendo» y reproductores vistos a partir de los pedidos HTTP, y avisa por <see cref="Activity"/>
/// con las frases del registro. La ventana consultará los métodos <c>Get*</c>.
/// Todo es aproximado: el servidor no recibe la posición real del Player.
/// </summary>
internal sealed class ViewingActivity
{
    private const string ContinueWatchingFileName = "seguir-viendo.json";
    private const string KnownPlayersFileName = "reproductores.json";
    private static readonly TimeSpan PlayersSaveInterval = TimeSpan.FromSeconds(30);

    // Los archivos se leen a mano: se conservan los acentos y el «·» sin escapar.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly string _dataDirectory;
    private readonly Func<DateTimeOffset> _clock;
    private readonly object _sync = new();
    private readonly Dictionary<string, RangeState> _recent = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, KnownPlayer> _players = new(StringComparer.Ordinal);
    private List<ContinueWatchingEntry> _continueWatching;
    private DateTimeOffset _lastPlayersSave = DateTimeOffset.MinValue;
    private bool _playersDirty;

    /// <summary>Línea legible para el registro de actividad.</summary>
    public event EventHandler<string>? Activity;

    public ViewingActivity(string dataDirectory, Func<DateTimeOffset>? clock = null)
    {
        _dataDirectory = dataDirectory;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);

        // La carga solo poda en memoria: el constructor nunca escribe en disco.
        var now = _clock();
        _continueWatching = ViewingRules.PruneContinueWatching(
            ReadFile<ContinueWatchingEntry>(ContinueWatchingFileName), now);
        foreach (var player in ViewingRules.PrunePlayers(ReadFile<KnownPlayer>(KnownPlayersFileName), now))
        {
            _players[player.Key] = player;
        }
    }

    /// <summary>Registra que un cliente hizo un pedido (reproductores vistos).</summary>
    public void NoteClient(ClientIdentity client)
    {
        lock (_sync)
        {
            NoteClientLocked(client, _clock());
        }
    }

    /// <summary>
    /// Registra que un cliente abrió una carpeta. Se llama solo con la primera página
    /// (StartingIndex 0), para no registrar cada página.
    /// </summary>
    public void RecordFolderOpened(ClientIdentity client, string folderPath, int returned, int total)
    {
        lock (_sync)
        {
            NoteClientLocked(client, _clock());
        }

        Raise(ViewingRules.FolderOpened(client.Name, folderPath, returned, total));
    }

    /// <summary>
    /// Registra un pedido de un rango de un video. Marca «viendo» durante 15 s, actualiza
    /// «seguir viendo» si el cliente tiene id de Player y avisa una vez por cada grupo de
    /// pedidos del mismo archivo y reproductor (60 s).
    /// </summary>
    public void RecordVideoRange(
        ClientIdentity client,
        string filePath,
        long rangeStart,
        long fileSize,
        TimeSpan? duration,
        MediaTechnicalInfo? technical)
    {
        string? message = null;
        lock (_sync)
        {
            var now = _clock();
            NoteClientLocked(client, now);
            PruneRecentLocked(now);

            var title = Path.GetFileNameWithoutExtension(filePath);
            var progress = ViewingRules.Measure(rangeStart, fileSize, duration);
            var quality = technical is null ? null : MediaDescription.QualityLabel(technical);
            var key = $"{client.Key}|{filePath}";

            var startsGroup = !_recent.ContainsKey(key);
            _recent[key] = new RangeState(
                client.Name,
                filePath,
                title,
                duration,
                progress,
                now);

            if (startsGroup)
            {
                message = ViewingRules.VideoStarted(client.Name, title, quality);
            }

            if (client.PlayerId is not null)
            {
                UpdateContinueWatchingLocked(client, filePath, title, progress, duration, now);
            }
        }

        if (message is not null)
        {
            Raise(message);
        }
    }

    /// <summary>Videos pedidos en los últimos 15 segundos.</summary>
    public IReadOnlyList<WatchingNow> GetWatchingNow()
    {
        lock (_sync)
        {
            var now = _clock();
            PruneRecentLocked(now);
            return _recent.Values
                .Where(state => now - state.LastRequest <= ViewingRules.WatchingWindow)
                .OrderByDescending(state => state.LastRequest)
                .Select(state => new WatchingNow(
                    state.ClientName,
                    state.FilePath,
                    state.Title,
                    state.Progress.Position,
                    state.Duration,
                    state.Progress.Percent,
                    state.LastRequest))
                .ToList();
        }
    }

    /// <summary>«Seguir viendo» vigente (7 días, máximo 50, sin lo terminado).</summary>
    public IReadOnlyList<ContinueWatchingEntry> GetContinueWatching()
    {
        lock (_sync)
        {
            _continueWatching = ViewingRules.PruneContinueWatching(_continueWatching, _clock());
            return _continueWatching.ToList();
        }
    }

    /// <summary>Reproductores vistos en los últimos 30 días, del más reciente al más antiguo.</summary>
    public IReadOnlyList<KnownPlayer> GetKnownPlayers()
    {
        lock (_sync)
        {
            return ViewingRules.PrunePlayers(_players.Values, _clock());
        }
    }

    /// <summary>Guarda los reproductores pendientes de escribir. El servidor lo llama al detenerse.</summary>
    public void Flush()
    {
        lock (_sync)
        {
            if (_playersDirty)
            {
                SavePlayersLocked(_clock());
            }
        }
    }

    private void NoteClientLocked(ClientIdentity client, DateTimeOffset now)
    {
        var isNew = !_players.ContainsKey(client.Key);
        _players[client.Key] = new KnownPlayer
        {
            Key = client.Key,
            Kind = client.Kind.ToString(),
            PlayerId = client.PlayerId,
            Name = client.DisplayName,
            Model = client.Model,
            DeviceName = client.DeviceName,
            Ip = client.RemoteAddress,
            LastSeen = now
        };
        _playersDirty = true;

        // Los pedidos son frecuentes (cada página de Browse): se escribe al aparecer un
        // reproductor nuevo y, después, como mucho cada 30 s; Flush cubre el resto.
        if (isNew || now - _lastPlayersSave >= PlayersSaveInterval)
        {
            SavePlayersLocked(now);
        }
    }

    private void UpdateContinueWatchingLocked(
        ClientIdentity client,
        string filePath,
        string title,
        PlaybackProgress progress,
        TimeSpan? duration,
        DateTimeOffset now)
    {
        var playerId = client.PlayerId!;
        _continueWatching.RemoveAll(entry =>
            entry.PlayerId == playerId
            && entry.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));

        if (progress.Percent <= ViewingRules.FinishedPercent)
        {
            _continueWatching.Add(new ContinueWatchingEntry
            {
                PlayerId = playerId,
                PlayerName = client.Name,
                FilePath = filePath,
                Title = title,
                Percent = Math.Round(progress.Percent, 1),
                PositionMs = progress.Position is { } position ? (long)position.TotalMilliseconds : null,
                DurationMs = duration is { } length ? (long)length.TotalMilliseconds : null,
                UpdatedAt = now
            });
        }

        _continueWatching = ViewingRules.PruneContinueWatching(_continueWatching, now);
        WriteFile(ContinueWatchingFileName, _continueWatching);
    }

    private void PruneRecentLocked(DateTimeOffset now)
    {
        var expired = _recent
            .Where(pair => now - pair.Value.LastRequest > ViewingRules.LogGroupWindow)
            .Select(pair => pair.Key)
            .ToList();
        foreach (var key in expired)
        {
            _recent.Remove(key);
        }
    }

    private void SavePlayersLocked(DateTimeOffset now)
    {
        var kept = ViewingRules.PrunePlayers(_players.Values, now);
        _players.Clear();
        foreach (var player in kept)
        {
            _players[player.Key] = player;
        }

        WriteFile(KnownPlayersFileName, kept);
        _playersDirty = false;
        _lastPlayersSave = now;
    }

    private void Raise(string message)
    {
        try
        {
            Activity?.Invoke(this, message);
        }
        catch
        {
            // Un suscriptor con error no debe detener el servidor.
        }
    }

    private List<T> ReadFile<T>(string fileName)
    {
        try
        {
            var path = Path.Combine(_dataDirectory, fileName);
            if (!File.Exists(path))
            {
                return new List<T>();
            }

            return JsonSerializer.Deserialize<List<T>>(File.ReadAllText(path), JsonOptions) ?? new List<T>();
        }
        catch
        {
            // Un archivo dañado no impide arrancar: se empieza de cero.
            return new List<T>();
        }
    }

    private void WriteFile<T>(string fileName, IEnumerable<T> items)
    {
        try
        {
            Directory.CreateDirectory(_dataDirectory);
            var path = Path.Combine(_dataDirectory, fileName);
            var temporaryPath = path + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(items.ToList(), JsonOptions));
            File.Move(temporaryPath, path, overwrite: true);
        }
        catch
        {
            // La actividad es informativa: si no se puede guardar, el servidor sigue funcionando.
        }
    }

    private sealed record RangeState(
        string ClientName,
        string FilePath,
        string Title,
        TimeSpan? Duration,
        PlaybackProgress Progress,
        DateTimeOffset LastRequest);
}
