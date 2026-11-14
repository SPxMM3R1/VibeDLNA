using System.Security.Cryptography;
using System.Text;

namespace FolderDlnaServer;

internal sealed class DlnaContentLibrary
{
    private readonly List<SharedRoot> _roots;
    private readonly bool _shareVideos;
    private readonly bool _shareAudio;
    private readonly bool _shareImages;
    private readonly object _countSync = new();
    private int _countGeneration;
    private int _videoCount = -1;

    public DlnaContentLibrary(IEnumerable<string> rootFolders, bool shareVideos, bool shareAudio, bool shareImages)
    {
        _roots = rootFolders
            .Where(Directory.Exists)
            .Select((folder, index) => CreateRoot(index, Path.GetFullPath(folder)))
            .ToList();
        _shareVideos = shareVideos;
        _shareAudio = shareAudio;
        _shareImages = shareImages;
    }

    /// <summary>
    /// Videos compartidos contados por la última pasada de indexado, o null mientras no haya
    /// terminado (contrato §B: lo que no se sepa, se omite).
    /// </summary>
    public int? VideoCount
    {
        get
        {
            var count = Volatile.Read(ref _videoCount);
            return count >= 0 ? count : null;
        }
    }

    /// <summary>
    /// Cuenta en segundo plano los videos compartidos recorriendo solo los listados de
    /// carpetas; nunca abre archivos. Una pasada nueva invalida la anterior.
    /// </summary>
    public Task StartCountingVideos(CancellationToken serverCancellation)
    {
        var generation = Interlocked.Increment(ref _countGeneration);
        var roots = _roots.Select(root => root.Path).ToArray();
        var countVideos = _shareVideos;

        bool IsCurrent() =>
            !serverCancellation.IsCancellationRequested
            && Volatile.Read(ref _countGeneration) == generation;

        return Task.Run(() =>
        {
            var count = 0;
            try
            {
                if (countVideos)
                {
                    foreach (var file in EnumerateFilesSafely(roots, IsCurrent))
                    {
                        if (MediaTypes.TryGet(file, out var mediaType) && mediaType.Kind == MediaKind.Video)
                        {
                            count++;
                        }
                    }
                }

                lock (_countSync)
                {
                    if (IsCurrent())
                    {
                        Volatile.Write(ref _videoCount, count);
                    }
                }
            }
            catch
            {
                // El conteo es informativo: si falla, la raíz simplemente no muestra videos.
            }
        }, CancellationToken.None);
    }

    public DlnaEntry GetMetadata(string objectId)
    {
        if (objectId == "0")
        {
            return new DlnaEntry
            {
                Id = "0",
                ParentId = "-1",
                Title = "Bibliotecas",
                FullPath = string.Empty,
                RelativePath = string.Empty,
                IsDirectory = true,
                ChildCount = _roots.Count,
                Description = MediaDescription.FolderSummary(_roots.Count, VideoCount)
            };
        }

        if (objectId.StartsWith("R:", StringComparison.Ordinal))
        {
            var sharedRoot = GetRoot(objectId[2..]);
            var directoryInfo = new DirectoryInfo(sharedRoot.Path);
            return new DlnaEntry
            {
                Id = objectId,
                ParentId = "0",
                Title = directoryInfo.Name,
                FullPath = sharedRoot.Path,
                RelativePath = string.Empty,
                IsDirectory = true,
                ChildCount = CountChildren(sharedRoot, sharedRoot.Path)
            };
        }

        var isDirectory = objectId.StartsWith("D:", StringComparison.Ordinal);
        var isFile = objectId.StartsWith("F:", StringComparison.Ordinal);
        if (!isDirectory && !isFile)
        {
            throw new FileNotFoundException("ObjectID no valido.");
        }

        var decoded = DecodeEntryPath(objectId[2..]);
        var root = GetRoot(decoded.RootKey);
        var fullPath = ResolvePath(root, decoded.RelativePath);
        if (isDirectory)
        {
            var directoryInfo = new DirectoryInfo(fullPath);
            if (!directoryInfo.Exists)
            {
                throw new DirectoryNotFoundException(fullPath);
            }

            return new DlnaEntry
            {
                Id = objectId,
                ParentId = GetParentId(root, decoded.RelativePath),
                Title = directoryInfo.Name,
                FullPath = fullPath,
                RelativePath = decoded.RelativePath,
                IsDirectory = true,
                ChildCount = CountChildren(root, fullPath)
            };
        }

        var fileInfo = new FileInfo(fullPath);
        if (!fileInfo.Exists || !TryGetAllowedMedia(fileInfo.FullName, out var mediaType))
        {
            throw new FileNotFoundException(fullPath);
        }

        return new DlnaEntry
        {
            Id = objectId,
            ParentId = GetParentId(root, decoded.RelativePath),
            Title = Path.GetFileNameWithoutExtension(fileInfo.Name),
            FullPath = fileInfo.FullName,
            RelativePath = decoded.RelativePath,
            IsDirectory = false,
            Size = fileInfo.Length,
            MimeType = mediaType.MimeType,
            UpnpClass = mediaType.UpnpClass
        };
    }

    public IReadOnlyList<DlnaEntry> GetChildren(string objectId)
    {
        if (objectId == "0")
        {
            return _roots
                .Select(root =>
                {
                    var directoryInfo = new DirectoryInfo(root.Path);
                    return new DlnaEntry
                    {
                        Id = $"R:{root.Key}",
                        ParentId = "0",
                        Title = directoryInfo.Name,
                        FullPath = root.Path,
                        RelativePath = string.Empty,
                        IsDirectory = true,
                        ChildCount = CountChildren(root, root.Path)
                    };
                })
                .ToArray();
        }

        var parent = GetMetadata(objectId);
        if (!parent.IsDirectory)
        {
            return Array.Empty<DlnaEntry>();
        }

        var root = objectId.StartsWith("R:", StringComparison.Ordinal)
            ? GetRoot(objectId[2..])
            : GetRoot(DecodeEntryPath(objectId[2..]).RootKey);
        var entries = new List<DlnaEntry>();

        try
        {
            foreach (var directory in Directory.EnumerateDirectories(parent.FullPath).OrderBy(Path.GetFileName))
            {
                var relativePath = GetRelativePath(root, directory);
                entries.Add(new DlnaEntry
                {
                    Id = EncodeId(isDirectory: true, root, relativePath),
                    ParentId = parent.Id,
                    Title = Path.GetFileName(directory),
                    FullPath = directory,
                    RelativePath = relativePath,
                    IsDirectory = true,
                    ChildCount = CountChildren(root, directory)
                });
            }
        }
        catch
        {
            // Skip folders that the current Windows user cannot enumerate.
        }

        try
        {
            foreach (var file in Directory.EnumerateFiles(parent.FullPath).OrderBy(Path.GetFileName))
            {
                if (!TryGetAllowedMedia(file, out var mediaType))
                {
                    continue;
                }

                var fileInfo = new FileInfo(file);
                var relativePath = GetRelativePath(root, file);
                entries.Add(new DlnaEntry
                {
                    Id = EncodeId(isDirectory: false, root, relativePath),
                    ParentId = parent.Id,
                    Title = Path.GetFileNameWithoutExtension(fileInfo.Name),
                    FullPath = fileInfo.FullName,
                    RelativePath = relativePath,
                    IsDirectory = false,
                    Size = fileInfo.Length,
                    MimeType = mediaType.MimeType,
                    UpnpClass = mediaType.UpnpClass
                });
            }
        }
        catch
        {
            // Skip files that the current Windows user cannot enumerate.
        }

        return entries;
    }

    public bool TryGetFile(string objectId, out string filePath, out MediaTypeInfo mediaType)
    {
        filePath = string.Empty;
        mediaType = new MediaTypeInfo("application/octet-stream", "object.item", MediaKind.Video);

        try
        {
            var entry = GetMetadata(objectId);
            if (entry.IsDirectory || !File.Exists(entry.FullPath) || !TryGetAllowedMedia(entry.FullPath, out mediaType!))
            {
                return false;
            }

            filePath = entry.FullPath;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private bool TryGetAllowedMedia(string path, out MediaTypeInfo mediaType) =>
        MediaTypes.TryGet(path, out mediaType!)
        && MediaTypes.IsAllowed(mediaType, _shareVideos, _shareAudio, _shareImages);

    private int CountChildren(SharedRoot root, string directory)
    {
        try
        {
            var directoryCount = Directory.EnumerateDirectories(directory).Count();
            var fileCount = Directory.EnumerateFiles(directory).Count(file => TryGetAllowedMedia(file, out _));
            return directoryCount + fileCount;
        }
        catch
        {
            return 0;
        }
    }

    private string ResolvePath(SharedRoot root, string relativePath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(root.Path, relativePath));
        if (!fullPath.Equals(root.Path, StringComparison.OrdinalIgnoreCase)
            && !fullPath.StartsWith(root.PathWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("La ruta queda fuera de la carpeta compartida.");
        }

        return fullPath;
    }

    /// <summary>
    /// Resolves a root by its stable key. Numeric keys are the legacy
    /// position-based ids of earlier versions and keep resolving while the
    /// folder list is unchanged.
    /// </summary>
    private SharedRoot GetRoot(string key)
    {
        var root = int.TryParse(key, out var legacyIndex)
            ? _roots.FirstOrDefault(item => item.Index == legacyIndex)
            : _roots.FirstOrDefault(item => item.Key.Equals(key, StringComparison.Ordinal));
        return root ?? throw new DirectoryNotFoundException("Carpeta compartida no encontrada.");
    }

    /// <summary>
    /// Root keys derive from the folder path, not from its position, so adding,
    /// removing or reordering shared folders keeps every other object id stable.
    /// </summary>
    internal static string GetRootKey(string fullPath)
    {
        var normalized = Path.TrimEndingDirectorySeparator(Path.GetFullPath(fullPath))
            .ToUpperInvariant();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(hash, 0, 8).ToLowerInvariant();
    }

    private static SharedRoot CreateRoot(int index, string fullPath) =>
        new(index, GetRootKey(fullPath), fullPath);

    /// <summary>
    /// Recorre las carpetas compartidas en profundidad y devuelve sus archivos. Ignora las
    /// carpetas que no se pueden listar y los enlaces de carpeta (para no entrar en bucle).
    /// </summary>
    private static IEnumerable<string> EnumerateFilesSafely(IEnumerable<string> roots, Func<bool> keepGoing)
    {
        var pending = new Stack<string>(roots);
        while (pending.Count > 0 && keepGoing())
        {
            var directory = pending.Pop();
            var files = Array.Empty<string>();
            var directories = Array.Empty<string>();
            try
            {
                files = Directory.GetFiles(directory);
                directories = Directory.GetDirectories(directory);
            }
            catch
            {
                // Carpeta que desapareció o que el usuario de Windows no puede listar.
            }

            foreach (var file in files)
            {
                yield return file;
            }

            foreach (var child in directories)
            {
                try
                {
                    if ((new DirectoryInfo(child).Attributes & FileAttributes.ReparsePoint) == 0)
                    {
                        pending.Push(child);
                    }
                }
                catch
                {
                    // Subcarpeta inaccesible: se omite.
                }
            }
        }
    }

    private static string GetRelativePath(SharedRoot root, string fullPath) =>
        Path.GetRelativePath(root.Path, fullPath);

    private static string GetParentId(SharedRoot root, string relativePath)
    {
        var parentPath = Path.GetDirectoryName(relativePath);
        return string.IsNullOrEmpty(parentPath) ? $"R:{root.Key}" : EncodeId(isDirectory: true, root, parentPath);
    }

    private static string EncodeId(bool isDirectory, SharedRoot root, string relativePath)
    {
        var bytes = Encoding.UTF8.GetBytes($"{root.Key}|{relativePath}");
        var encoded = Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        return $"{(isDirectory ? "D" : "F")}:{encoded}";
    }

    private static DecodedEntryPath DecodeEntryPath(string encoded)
    {
        var padded = encoded.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + ((4 - padded.Length % 4) % 4), '=');
        var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(padded));
        var separator = decoded.IndexOf('|');
        if (separator <= 0)
        {
            throw new FileNotFoundException("ObjectID no valido.");
        }

        return new DecodedEntryPath(decoded[..separator], decoded[(separator + 1)..]);
    }

    private sealed record SharedRoot(int Index, string Key, string Path)
    {
        public string PathWithSeparator { get; } = Path.EndsWith(System.IO.Path.DirectorySeparatorChar)
            ? Path
            : Path + System.IO.Path.DirectorySeparatorChar;
    }

    private sealed record DecodedEntryPath(string RootKey, string RelativePath);
}
