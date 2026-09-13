using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;

namespace FolderDlnaServer;

internal sealed class ThumbnailCache
{
    private readonly string _directory;
    private readonly string _indexPath;
    private readonly IThumbnailProvider _thumbnailProvider;
    private readonly object _indexSync = new();
    private readonly ConcurrentDictionary<string, Lazy<string?>> _inFlight = new(
        StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, CachedChecksum> _knownChecksums = new(
        StringComparer.OrdinalIgnoreCase);

    public ThumbnailCache(string? directory = null, IThumbnailProvider? thumbnailProvider = null)
    {
        _directory = Path.GetFullPath(directory ?? DefaultDirectory);
        _indexPath = Path.Combine(_directory, "index.json");
        _thumbnailProvider = thumbnailProvider ?? new WindowsThumbnailProvider();
        Directory.CreateDirectory(_directory);
        LoadIndex();
    }

    public static string DefaultDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VibeDLNA",
            "ThumbnailCache");

    public string DirectoryPath => _directory;

    public string? GetOrCreate(string filePath) =>
        GetOrCreate(filePath, CancellationToken.None);

    internal string? GetOrCreate(string filePath, CancellationToken cancellationToken)
    {
        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(filePath);
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch
        {
            return null;
        }

        if (!IsSupportedMedia(fullPath) || !File.Exists(fullPath))
        {
            _knownChecksums.TryRemove(fullPath, out _);
            return null;
        }

        string checksum;
        try
        {
            var fileInfo = new FileInfo(fullPath);
            cancellationToken.ThrowIfCancellationRequested();
            if (_knownChecksums.TryGetValue(fullPath, out var knownChecksum)
                && knownChecksum.Length == fileInfo.Length
                && knownChecksum.LastWriteUtcTicks == fileInfo.LastWriteTimeUtc.Ticks)
            {
                checksum = knownChecksum.Checksum;
            }
            else
            {
                checksum = ComputeChecksum(fullPath, cancellationToken);
                var currentInfo = new FileInfo(fullPath);
                _knownChecksums[fullPath] = new CachedChecksum(
                    currentInfo.Length,
                    currentInfo.LastWriteTimeUtc.Ticks,
                    checksum);
                PersistIndex();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch
        {
            _knownChecksums.TryRemove(fullPath, out _);
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var cachedPath = GetCachePath(checksum);
        if (IsUsableFile(cachedPath))
        {
            return checksum;
        }

        var pending = _inFlight.GetOrAdd(
            checksum,
            _ => new Lazy<string?>(
                () => CreateThumbnail(checksum, fullPath),
                LazyThreadSafetyMode.ExecutionAndPublication));

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return pending.Value;
        }
        finally
        {
            if (_inFlight.TryGetValue(checksum, out var current)
                && ReferenceEquals(current, pending))
            {
                _inFlight.TryRemove(checksum, out _);
            }
        }
    }

    public void Invalidate()
    {
        var changed = false;
        foreach (var pair in _knownChecksums)
        {
            if (IsCurrentSource(pair.Key, pair.Value))
            {
                continue;
            }

            if (_knownChecksums.TryRemove(pair.Key, out _))
            {
                changed = true;
            }
        }

        if (changed)
        {
            PersistIndex();
        }
    }

    public bool TryGetPath(string checksum, out string path)
    {
        path = string.Empty;
        if (!IsChecksum(checksum))
        {
            return false;
        }

        var candidate = GetCachePath(checksum);
        if (!IsUsableFile(candidate))
        {
            return false;
        }

        path = candidate;
        return true;
    }

    /// <summary>
    /// Returns artwork that is already indexed and ready without hashing or
    /// rendering the source media on the request thread.
    /// </summary>
    public string? TryGetCached(string filePath)
    {
        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(filePath);
            if (!IsSupportedMedia(fullPath) || !File.Exists(fullPath))
            {
                return null;
            }

            var fileInfo = new FileInfo(fullPath);
            if (!_knownChecksums.TryGetValue(fullPath, out var knownChecksum)
                || knownChecksum.Length != fileInfo.Length
                || knownChecksum.LastWriteUtcTicks != fileInfo.LastWriteTimeUtc.Ticks)
            {
                return null;
            }

            return IsUsableFile(GetCachePath(knownChecksum.Checksum))
                ? knownChecksum.Checksum
                : null;
        }
        catch
        {
            return null;
        }
    }

    internal string GetCachePathForTesting(string checksum) => GetCachePath(checksum);

    internal string IndexPathForTesting => _indexPath;

    internal static string ComputeChecksum(string filePath) =>
        ComputeChecksum(filePath, CancellationToken.None);

    private static string ComputeChecksum(string filePath, CancellationToken cancellationToken)
    {
        using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 1024 * 128,
            options: FileOptions.SequentialScan);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[1024 * 128];
        int read;
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            read = stream.Read(buffer, 0, buffer.Length);
            if (read > 0)
            {
                hash.AppendData(buffer, 0, read);
            }
        }
        while (read > 0);

        cancellationToken.ThrowIfCancellationRequested();
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private string? CreateThumbnail(string checksum, string sourcePath)
    {
        var targetPath = GetCachePath(checksum);
        if (IsUsableFile(targetPath))
        {
            return checksum;
        }

        var temporaryPath = Path.Combine(
            _directory,
            $"{checksum}.{Guid.NewGuid():N}.tmp");

        try
        {
            if (!_thumbnailProvider.TrySave(sourcePath, temporaryPath)
                || !IsUsableFile(temporaryPath))
            {
                return null;
            }

            File.Move(temporaryPath, targetPath, overwrite: true);
            return IsUsableFile(targetPath) ? checksum : null;
        }
        catch
        {
            return null;
        }
        finally
        {
            try
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
            catch
            {
                // Temporary cleanup is best effort.
            }
        }
    }

    private string GetCachePath(string checksum) =>
        Path.Combine(_directory, checksum.ToLowerInvariant() + ".jpg");

    private sealed record CachedChecksum(long Length, long LastWriteUtcTicks, string Checksum);

    private sealed class PersistedChecksum
    {
        public string Path { get; set; } = string.Empty;

        public long Length { get; set; }

        public long LastWriteUtcTicks { get; set; }

        public string Checksum { get; set; } = string.Empty;
    }

    private static readonly JsonSerializerOptions IndexSerializerOptions = new()
    {
        WriteIndented = true
    };

    private void LoadIndex()
    {
        try
        {
            if (!File.Exists(_indexPath))
            {
                return;
            }

            var entries = JsonSerializer.Deserialize<List<PersistedChecksum>>(
                File.ReadAllText(_indexPath),
                IndexSerializerOptions);
            if (entries is null)
            {
                return;
            }

            foreach (var entry in entries)
            {
                if (string.IsNullOrWhiteSpace(entry.Path)
                    || !IsChecksum(entry.Checksum)
                    || entry.Length < 0
                    || entry.LastWriteUtcTicks < 0)
                {
                    continue;
                }

                try
                {
                    _knownChecksums[Path.GetFullPath(entry.Path)] = new CachedChecksum(
                        entry.Length,
                        entry.LastWriteUtcTicks,
                        entry.Checksum.ToLowerInvariant());
                }
                catch
                {
                    // Ignore an invalid path and continue rebuilding the index.
                }
            }
        }
        catch
        {
            // A corrupt or incompatible index must not prevent the server from starting.
            _knownChecksums.Clear();
        }
    }

    private void PersistIndex()
    {
        lock (_indexSync)
        {
            var entries = _knownChecksums
                .Select(pair => new PersistedChecksum
                {
                    Path = pair.Key,
                    Length = pair.Value.Length,
                    LastWriteUtcTicks = pair.Value.LastWriteUtcTicks,
                    Checksum = pair.Value.Checksum
                })
                .OrderBy(entry => entry.Path, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var temporaryPath = _indexPath + ".tmp";

            try
            {
                File.WriteAllText(
                    temporaryPath,
                    JsonSerializer.Serialize(entries, IndexSerializerOptions));
                File.Move(temporaryPath, _indexPath, overwrite: true);
            }
            catch
            {
                // The in-memory index remains authoritative for this process.
            }
            finally
            {
                try
                {
                    if (File.Exists(temporaryPath))
                    {
                        File.Delete(temporaryPath);
                    }
                }
                catch
                {
                    // Temporary cleanup is best effort.
                }
            }
        }
    }

    private static bool IsSupportedMedia(string filePath) =>
        MediaTypes.TryGet(filePath, out _);

    private static bool IsCurrentSource(string filePath, CachedChecksum checksum)
    {
        try
        {
            if (!IsSupportedMedia(filePath) || !File.Exists(filePath))
            {
                return false;
            }

            var fileInfo = new FileInfo(filePath);
            return checksum.Length == fileInfo.Length
                && checksum.LastWriteUtcTicks == fileInfo.LastWriteTimeUtc.Ticks;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsUsableFile(string path)
    {
        try
        {
            return File.Exists(path) && new FileInfo(path).Length > 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsChecksum(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != 64)
        {
            return false;
        }

        foreach (var character in value)
        {
            var isHex = character is >= '0' and <= '9'
                or >= 'a' and <= 'f'
                or >= 'A' and <= 'F';
            if (!isHex)
            {
                return false;
            }
        }

        return true;
    }

    internal static IEnumerable<string> EnumerateMediaFiles(
        IReadOnlyList<string> roots,
        CancellationToken cancellationToken)
    {
        var pending = new Stack<string>(roots.Reverse());
        while (pending.Count > 0 && !cancellationToken.IsCancellationRequested)
        {
            var directory = pending.Pop();
            var files = Array.Empty<string>();
            try
            {
                files = Directory.GetFiles(directory);
            }
            catch
            {
                // Skip folders that disappear or cannot be read.
            }

            foreach (var file in files)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    yield break;
                }

                if (IsSupportedMedia(file))
                {
                    yield return file;
                }
            }

            var childDirectories = Array.Empty<string>();
            try
            {
                childDirectories = Directory.GetDirectories(directory);
            }
            catch
            {
                // Skip folders that disappear or cannot be read.
            }

            foreach (var childDirectory in childDirectories)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    yield break;
                }

                try
                {
                    if ((File.GetAttributes(childDirectory) & FileAttributes.ReparsePoint) != 0)
                    {
                        continue;
                    }
                }
                catch
                {
                    continue;
                }

                pending.Push(childDirectory);
            }
        }
    }
}
