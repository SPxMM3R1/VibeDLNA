using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace FolderDlnaServer;

internal interface IThumbnailProvider
{
    bool TrySave(string sourcePath, string targetPath);
}

/// <summary>
/// Obtains artwork through the Windows Shell thumbnail provider.
/// </summary>
internal sealed class WindowsThumbnailProvider : IThumbnailProvider
{
    private const int ThumbnailWidth = 320;
    private const int ThumbnailHeight = 180;
    private static readonly Guid ShellItemImageFactoryId =
        new("BCC18B79-BA16-442F-80C4-8A59C30C463B");

    public bool TrySave(string sourcePath, string targetPath)
    {
        using var thumbnail = TryGetShellThumbnail(sourcePath);
        return thumbnail is not null && SaveJpeg(thumbnail, targetPath);
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
            var interfaceId = ShellItemImageFactoryId;
            var createResult = SHCreateItemFromParsingName(
                sourcePath,
                IntPtr.Zero,
                ref interfaceId,
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
