using System.Text;

namespace FolderDlnaServer;

internal sealed class DlnaContentLibrary
{
    private readonly string _rootFolder;
    private readonly string _rootFolderWithSeparator;

    public DlnaContentLibrary(string rootFolder)
    {
        _rootFolder = Path.GetFullPath(rootFolder);
        _rootFolderWithSeparator = _rootFolder.EndsWith(Path.DirectorySeparatorChar)
            ? _rootFolder
            : _rootFolder + Path.DirectorySeparatorChar;
    }

    public DlnaEntry GetMetadata(string objectId)
    {
        if (objectId == "0")
        {
            var directoryInfo = new DirectoryInfo(_rootFolder);
            return new DlnaEntry
            {
                Id = "0",
                ParentId = "-1",
                Title = directoryInfo.Name,
                FullPath = _rootFolder,
                RelativePath = string.Empty,
                IsDirectory = true,
                ChildCount = CountChildren(_rootFolder)
            };
        }

        var isDirectory = objectId.StartsWith("D:", StringComparison.Ordinal);
        var isFile = objectId.StartsWith("F:", StringComparison.Ordinal);
        if (!isDirectory && !isFile)
        {
            throw new FileNotFoundException("ObjectID no valido.");
        }

        var relativePath = DecodeRelativePath(objectId[2..]);
        var fullPath = ResolvePath(relativePath);
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
                ParentId = GetParentId(relativePath),
                Title = directoryInfo.Name,
                FullPath = fullPath,
                RelativePath = relativePath,
                IsDirectory = true,
                ChildCount = CountChildren(fullPath)
            };
        }

        var fileInfo = new FileInfo(fullPath);
        if (!fileInfo.Exists || !MediaTypes.TryGet(fileInfo.FullName, out var mediaType))
        {
            throw new FileNotFoundException(fullPath);
        }

        return new DlnaEntry
        {
            Id = objectId,
            ParentId = GetParentId(relativePath),
            Title = Path.GetFileNameWithoutExtension(fileInfo.Name),
            FullPath = fileInfo.FullName,
            RelativePath = relativePath,
            IsDirectory = false,
            Size = fileInfo.Length,
            MimeType = mediaType.MimeType,
            UpnpClass = mediaType.UpnpClass
        };
    }

    public IReadOnlyList<DlnaEntry> GetChildren(string objectId)
    {
        var parent = GetMetadata(objectId);
        if (!parent.IsDirectory)
        {
            return Array.Empty<DlnaEntry>();
        }

        var entries = new List<DlnaEntry>();

        try
        {
            foreach (var directory in Directory.EnumerateDirectories(parent.FullPath).OrderBy(Path.GetFileName))
            {
                var relativePath = GetRelativePath(directory);
                entries.Add(new DlnaEntry
                {
                    Id = EncodeId(isDirectory: true, relativePath),
                    ParentId = parent.Id,
                    Title = Path.GetFileName(directory),
                    FullPath = directory,
                    RelativePath = relativePath,
                    IsDirectory = true,
                    ChildCount = CountChildren(directory)
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
                if (!MediaTypes.TryGet(file, out var mediaType))
                {
                    continue;
                }

                var fileInfo = new FileInfo(file);
                var relativePath = GetRelativePath(file);
                entries.Add(new DlnaEntry
                {
                    Id = EncodeId(isDirectory: false, relativePath),
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
        mediaType = new MediaTypeInfo("application/octet-stream", "object.item");

        try
        {
            var entry = GetMetadata(objectId);
            if (entry.IsDirectory || !File.Exists(entry.FullPath) || !MediaTypes.TryGet(entry.FullPath, out mediaType!))
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

    private int CountChildren(string directory)
    {
        try
        {
            var directoryCount = Directory.EnumerateDirectories(directory).Count();
            var fileCount = Directory.EnumerateFiles(directory).Count(file => MediaTypes.TryGet(file, out _));
            return directoryCount + fileCount;
        }
        catch
        {
            return 0;
        }
    }

    private string ResolvePath(string relativePath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_rootFolder, relativePath));
        if (!fullPath.Equals(_rootFolder, StringComparison.OrdinalIgnoreCase)
            && !fullPath.StartsWith(_rootFolderWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("La ruta queda fuera de la carpeta compartida.");
        }

        return fullPath;
    }

    private string GetRelativePath(string fullPath) =>
        Path.GetRelativePath(_rootFolder, fullPath);

    private static string GetParentId(string relativePath)
    {
        var parentPath = Path.GetDirectoryName(relativePath);
        return string.IsNullOrEmpty(parentPath) ? "0" : EncodeId(isDirectory: true, parentPath);
    }

    private static string EncodeId(bool isDirectory, string relativePath)
    {
        var bytes = Encoding.UTF8.GetBytes(relativePath);
        var encoded = Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        return $"{(isDirectory ? "D" : "F")}:{encoded}";
    }

    private static string DecodeRelativePath(string encoded)
    {
        var padded = encoded.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + ((4 - padded.Length % 4) % 4), '=');
        return Encoding.UTF8.GetString(Convert.FromBase64String(padded));
    }
}
