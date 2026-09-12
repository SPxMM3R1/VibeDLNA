using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace FolderDlnaServer;

internal sealed class ThumbnailCache
{
    private const int ThumbnailWidth = 320;
    private const int ThumbnailHeight = 180;
    private static readonly Guid ShellItemImageFactoryId =
        new("BCC18B79-BA16-442F-80C4-8A59C30C463B");

    private readonly string _directory;
    private readonly ConcurrentDictionary<string, Lazy<string?>> _inFlight = new(
        StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, CachedChecksum> _knownChecksums = new(
        StringComparer.OrdinalIgnoreCase);

    public ThumbnailCache(string? directory = null)
    {
        _directory = Path.GetFullPath(directory ?? DefaultDirectory);
        Directory.CreateDirectory(_directory);
    }

    public static string DefaultDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VibeDLNA",
            "ThumbnailCache");

    public string DirectoryPath => _directory;

    public string? GetOrCreate(string filePath)
    {
        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(filePath);
        }
        catch
        {
            return null;
        }

        if (!IsVideo(fullPath) || !File.Exists(fullPath))
        {
            _knownChecksums.TryRemove(fullPath, out _);
            return null;
        }

        string checksum;
        try
        {
            var fileInfo = new FileInfo(fullPath);
            if (_knownChecksums.TryGetValue(fullPath, out var knownChecksum)
                && knownChecksum.Length == fileInfo.Length
                && knownChecksum.LastWriteUtcTicks == fileInfo.LastWriteTimeUtc.Ticks)
            {
                checksum = knownChecksum.Checksum;
            }
            else
            {
                checksum = ComputeChecksum(fullPath);
                var currentInfo = new FileInfo(fullPath);
                _knownChecksums[fullPath] = new CachedChecksum(
                    currentInfo.Length,
                    currentInfo.LastWriteTimeUtc.Ticks,
                    checksum);
            }
        }
        catch
        {
            _knownChecksums.TryRemove(fullPath, out _);
            return null;
        }

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
        _knownChecksums.Clear();
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

    internal string GetCachePathForTesting(string checksum) => GetCachePath(checksum);

    internal static string ComputeChecksum(string filePath)
    {
        using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 1024 * 128,
            options: FileOptions.SequentialScan);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    public Task WarmAsync(IEnumerable<string> folders, CancellationToken cancellationToken)
    {
        var roots = folders
            .Where(folder => !string.IsNullOrWhiteSpace(folder))
            .Select(Path.GetFullPath)
            .Where(Directory.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return Task.Run(
            () =>
            {
                foreach (var filePath in EnumerateVideoFiles(roots, cancellationToken))
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    try
                    {
                        GetOrCreate(filePath);
                    }
                    catch
                    {
                        // One inaccessible or changing file must not stop the cache warmup.
                    }
                }
            },
            CancellationToken.None);
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
            if (!VideoThumbnailRenderer.TrySave(sourcePath, temporaryPath))
            {
                if (!VideoThumbnailRenderer.TrySaveFallback(temporaryPath))
                {
                    return null;
                }
            }

            if (!IsUsableFile(temporaryPath))
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

    private static bool IsVideo(string filePath) =>
        MediaTypes.TryGet(filePath, out var mediaType)
        && mediaType.Kind == MediaKind.Video;

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

    private static IEnumerable<string> EnumerateVideoFiles(
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
                if (IsVideo(file))
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

    private static class VideoThumbnailRenderer
    {
        public static bool TrySave(string sourcePath, string targetPath)
        {
            using var thumbnail = TryGetShellThumbnail(sourcePath);
            return thumbnail is not null && SaveJpeg(thumbnail, targetPath);
        }

        public static bool TrySaveFallback(string targetPath)
        {
            try
            {
                using var bitmap = new Bitmap(
                    ThumbnailWidth,
                    ThumbnailHeight,
                    PixelFormat.Format24bppRgb);
                using var graphics = Graphics.FromImage(bitmap);
                graphics.Clear(Color.FromArgb(29, 36, 48));
                graphics.SmoothingMode = SmoothingMode.AntiAlias;

                using var borderPen = new Pen(Color.FromArgb(93, 111, 137), 3);
                using var accentBrush = new SolidBrush(Color.FromArgb(62, 174, 238));
                graphics.DrawRectangle(
                    borderPen,
                    22,
                    22,
                    ThumbnailWidth - 44,
                    ThumbnailHeight - 44);
                graphics.FillPolygon(
                    accentBrush,
                    new[]
                    {
                        new Point(139, 58),
                        new Point(139, 122),
                        new Point(204, 90)
                    });

                using var font = new Font("Segoe UI", 12f, FontStyle.Regular);
                using var textBrush = new SolidBrush(Color.FromArgb(215, 225, 239));
                graphics.DrawString(
                    "VibeDLNA",
                    font,
                    textBrush,
                    new PointF(ThumbnailWidth - 94, ThumbnailHeight - 28));

                return SaveJpeg(bitmap, targetPath);
            }
            catch
            {
                return false;
            }
        }

        private static Bitmap? TryGetShellThumbnail(string sourcePath)
        {
            if (!OperatingSystem.IsWindows())
            {
                return null;
            }

            IShellItemImageFactory? factory = null;
            var bitmapHandle = IntPtr.Zero;
            try
            {
                var iid = ShellItemImageFactoryId;
                var createResult = SHCreateItemFromParsingName(
                    sourcePath,
                    IntPtr.Zero,
                    ref iid,
                    out factory);
                if (createResult < 0 || factory is null)
                {
                    return null;
                }

                var imageResult = factory.GetImage(
                    new ShellSize(640, 360),
                    ShellImageFlags.ThumbnailOnly | ShellImageFlags.BiggerSizeOk,
                    out bitmapHandle);
                if (imageResult < 0 || bitmapHandle == IntPtr.Zero)
                {
                    return null;
                }

                using var source = Image.FromHbitmap(bitmapHandle);
                return CropToThumbnail(source);
            }
            catch
            {
                return null;
            }
            finally
            {
                if (bitmapHandle != IntPtr.Zero)
                {
                    DeleteObject(bitmapHandle);
                }

                if (factory is not null)
                {
                    Marshal.ReleaseComObject(factory);
                }
            }
        }

        private static Bitmap CropToThumbnail(Image source)
        {
            var sourceRatio = (double)source.Width / source.Height;
            var targetRatio = (double)ThumbnailWidth / ThumbnailHeight;
            var sourceWidth = source.Width;
            var sourceHeight = source.Height;
            var sourceX = 0;
            var sourceY = 0;

            if (sourceRatio > targetRatio)
            {
                sourceWidth = (int)Math.Round(source.Height * targetRatio);
                sourceX = (source.Width - sourceWidth) / 2;
            }
            else if (sourceRatio < targetRatio)
            {
                sourceHeight = (int)Math.Round(source.Width / targetRatio);
                sourceY = (source.Height - sourceHeight) / 2;
            }

            var result = new Bitmap(
                ThumbnailWidth,
                ThumbnailHeight,
                PixelFormat.Format24bppRgb);
            using var graphics = Graphics.FromImage(result);
            graphics.CompositingMode = CompositingMode.SourceCopy;
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.SmoothingMode = SmoothingMode.HighQuality;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.DrawImage(
                source,
                new Rectangle(0, 0, ThumbnailWidth, ThumbnailHeight),
                new Rectangle(sourceX, sourceY, sourceWidth, sourceHeight),
                GraphicsUnit.Pixel);
            return result;
        }

        private static bool SaveJpeg(Image image, string targetPath)
        {
            try
            {
                var codec = ImageCodecInfo.GetImageEncoders()
                    .FirstOrDefault(item => item.FormatID == ImageFormat.Jpeg.Guid);
                if (codec is null)
                {
                    image.Save(targetPath, ImageFormat.Jpeg);
                    return true;
                }

                using var parameters = new EncoderParameters(1);
                parameters.Param[0] = new EncoderParameter(
                    System.Drawing.Imaging.Encoder.Quality,
                    86L);
                image.Save(targetPath, codec, parameters);
                return true;
            }
            catch
            {
                return false;
            }
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHCreateItemFromParsingName(
            string path,
            IntPtr bindContext,
            ref Guid interfaceId,
            [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory factory);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr handle);

        [ComImport]
        [Guid("BCC18B79-BA16-442F-80C4-8A59C30C463B")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItemImageFactory
        {
            [PreserveSig]
            int GetImage(
                ShellSize size,
                ShellImageFlags flags,
                out IntPtr bitmapHandle);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ShellSize
        {
            public ShellSize(int width, int height)
            {
                Width = width;
                Height = height;
            }

            public int Width;

            public int Height;
        }

        [Flags]
        private enum ShellImageFlags : uint
        {
            BiggerSizeOk = 0x00000001,
            ThumbnailOnly = 0x00000008
        }
    }
}
