namespace FolderDlnaServer;

internal interface IMediaTechnicalProvider
{
    /// <summary>
    /// Devuelve lo que Windows sabe del archivo. Nunca es null: si no sabe nada, devuelve
    /// un registro vacío (todos los campos nulos).
    /// </summary>
    MediaTechnicalInfo TryGetInfo(string sourcePath);
}

/// <summary>
/// Lee las propiedades técnicas de video y audio del Shell de Windows (contrato §A). Solo se
/// llama desde el calentamiento en segundo plano, nunca desde Browse.
/// </summary>
internal sealed class WindowsMediaTechnicalProvider : IMediaTechnicalProvider
{
    private static readonly Guid VideoFormatId = new("64440491-4C8B-11D1-8B70-080036B11A03");
    private static readonly Guid AudioFormatId = new("64440490-4C8B-11D1-8B70-080036B11A03");

    // Claves comprobadas con PSGetPropertyKeyFromName: System.Video.* y System.Audio.*.
    private static readonly ShellPropertyKey FrameWidth = new(VideoFormatId, 3);
    private static readonly ShellPropertyKey FrameHeight = new(VideoFormatId, 4);
    private static readonly ShellPropertyKey FrameRate = new(VideoFormatId, 6);
    private static readonly ShellPropertyKey VideoCompression = new(VideoFormatId, 10);
    private static readonly ShellPropertyKey TotalBitrate = new(VideoFormatId, 43);
    private static readonly ShellPropertyKey VideoFourCc = new(VideoFormatId, 44);
    private static readonly ShellPropertyKey AudioFormat = new(AudioFormatId, 2);
    private static readonly ShellPropertyKey AudioChannelCount = new(AudioFormatId, 7);

    public MediaTechnicalInfo TryGetInfo(string sourcePath)
    {
        try
        {
            using var reader = ShellPropertyReader.TryOpen(sourcePath);
            if (reader is null)
            {
                return new MediaTechnicalInfo();
            }

            var fourCc = ReadUInt32(reader, VideoFourCc);
            return new MediaTechnicalInfo
            {
                Width = ToPositiveInt(ReadUInt32(reader, FrameWidth)),
                Height = ToPositiveInt(ReadUInt32(reader, FrameHeight)),
                TotalBitrateBits = ReadUInt32(reader, TotalBitrate) is { } bits and > 0 ? (long?)bits : null,
                FrameRateMilli = ToPositiveInt(ReadUInt32(reader, FrameRate)),
                VideoFourCc = fourCc is { } code ? MediaDescription.DecodeFourCc(code) : null,
                VideoCompression = reader.TryGetString(VideoCompression),
                AudioFormat = reader.TryGetString(AudioFormat),
                AudioChannels = ToPositiveInt(ReadUInt32(reader, AudioChannelCount))
            };
        }
        catch
        {
            return new MediaTechnicalInfo();
        }
    }

    private static uint? ReadUInt32(ShellPropertyReader reader, ShellPropertyKey key) =>
        reader.TryGetUInt32(key, out var value) ? value : null;

    private static int? ToPositiveInt(uint? value) =>
        value is { } number && number > 0 && number <= int.MaxValue
            ? (int)number
            : null;
}
