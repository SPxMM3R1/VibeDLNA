namespace FolderDlnaServer;

internal interface IMediaDurationProvider
{
    /// <summary>
    /// Returns the media duration, or null when Windows does not know it.
    /// </summary>
    TimeSpan? TryGetDuration(string sourcePath);
}

/// <summary>
/// Reads System.Media.Duration from the Windows Shell property system. It is
/// only called by the background warmup, never while answering Browse.
/// </summary>
internal sealed class WindowsMediaDurationProvider : IMediaDurationProvider
{
    private static readonly ShellPropertyKey MediaDuration = new(
        new Guid("64440490-4C8B-11D1-8B70-080036B11A03"),
        3);

    public TimeSpan? TryGetDuration(string sourcePath)
    {
        try
        {
            using var reader = ShellPropertyReader.TryOpen(sourcePath);
            if (reader is null || !reader.TryGetUInt64(MediaDuration, out var ticks))
            {
                return null;
            }

            // System.Media.Duration is expressed in 100 ns units, like TimeSpan ticks.
            return ticks is > 0 and <= long.MaxValue
                ? TimeSpan.FromTicks((long)ticks)
                : null;
        }
        catch
        {
            return null;
        }
    }
}
