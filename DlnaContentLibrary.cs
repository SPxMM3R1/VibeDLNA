using System.Text;

namespace FolderDlnaServer;

internal sealed class DlnaContentLibrary
{
    private readonly List<SharedRoot> _roots;
    private readonly bool _shareVideos;
    private readonly bool _shareAudio;
    private readonly bool _shareImages;

    public DlnaContentLibrary(IEnumerable<string> rootFolders, bool shareVideos, bool shareAudio, bool shareImages)
    {
        _roots = rootFolders
            .Where(Directory.Exists)
            .Select((folder, index) => new SharedRoot(index, Path.GetFullPath(folder)))
            .ToList();
        _shareVideos = shareVideos;
        _shareAudio = shareAudio;
        _shareImages = shareImages;
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
                ChildCount = _roots.Count
            };
        }

        if (objectId.StartsWith("R:", StringComparison.Ordinal))
        {
            var sharedRoot = GetRoot(ParseRootIndex(objectId[2..]));
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
        var root = GetRoot(decoded.RootIndex);
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
                ParentId = GetParentId(root.Index, decoded.RelativePath),
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
            ParentId = GetParentId(root.Index, decoded.RelativePath),
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
                        Id = $"R:{root.Index}",
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
            ? GetRoot(ParseRootIndex(objectId[2..]))
            : GetRoot(DecodeEntryPath(objectId[2..]).RootIndex);
        var entries = new List<DlnaEntry>();

        try
        {
            foreach (var directory in Directory.EnumerateDirectories(parent.FullPath).OrderBy(Path.GetFileName))
            {
                var relativePath = GetRelativePath(root, directory);
                entries.Add(new DlnaEntry
                {
                    Id = EncodeId(isDirectory: true, root.Index, relativePath),
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
                    Id = EncodeId(isDirectory: false, root.Index, relativePath),
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

    private SharedRoot GetRoot(int index) =>
        _roots.FirstOrDefault(root => root.Index == index)
        ?? throw new DirectoryNotFoundException("Carpeta compartida no encontrada.");

    private static int ParseRootIndex(string value) =>
        int.TryParse(value, out var index) ? index : throw new FileNotFoundException("Raiz no valida.");

    private static string GetRelativePath(SharedRoot root, string fullPath) =>
        Path.GetRelativePath(root.Path, fullPath);

    private static string GetParentId(int rootIndex, string relativePath)
    {
        var parentPath = Path.GetDirectoryName(relativePath);
        return string.IsNullOrEmpty(parentPath) ? $"R:{rootIndex}" : EncodeId(isDirectory: true, rootIndex, parentPath);
    }

    private static string EncodeId(bool isDirectory, int rootIndex, string relativePath)
    {
        var bytes = Encoding.UTF8.GetBytes($"{rootIndex}|{relativePath}");
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
        if (separator <= 0 || !int.TryParse(decoded[..separator], out var rootIndex))
        {
            throw new FileNotFoundException("ObjectID no valido.");
        }

        return new DecodedEntryPath(rootIndex, decoded[(separator + 1)..]);
    }

    private sealed record SharedRoot(int Index, string Path)
    {
        public string PathWithSeparator { get; } = Path.EndsWith(System.IO.Path.DirectorySeparatorChar)
            ? Path
            : Path + System.IO.Path.DirectorySeparatorChar;
    }

    private sealed record DecodedEntryPath(int RootIndex, string RelativePath);
}
