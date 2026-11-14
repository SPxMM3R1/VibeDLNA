namespace FolderDlnaServer;

/// <summary>
/// Datos técnicos que Windows conoce de un video o audio (contrato §A). Se guardan tal
/// como los entrega el Shell; el texto legible se calcula al emitir el XML con
/// <see cref="MediaDescription"/>. Un campo nulo significa «Windows no lo sabe» y se omite.
/// </summary>
internal sealed record MediaTechnicalInfo
{
    /// <summary>System.Video.FrameWidth, en píxeles.</summary>
    public int? Width { get; init; }

    /// <summary>System.Video.FrameHeight, en píxeles.</summary>
    public int? Height { get; init; }

    /// <summary>System.Video.TotalBitrate, en bits por segundo.</summary>
    public long? TotalBitrateBits { get; init; }

    /// <summary>System.Video.FrameRate, en milésimas de fps (30 fps = 30000).</summary>
    public int? FrameRateMilli { get; init; }

    /// <summary>System.Video.FourCC decodificado (por ejemplo «H264» o «HEVC»).</summary>
    public string? VideoFourCc { get; init; }

    /// <summary>System.Video.Compression tal cual; suele ser un subtipo GUID con el FourCC dentro.</summary>
    public string? VideoCompression { get; init; }

    /// <summary>System.Audio.Format tal cual; suele ser un subtipo GUID (tag WAVE o KS).</summary>
    public string? AudioFormat { get; init; }

    /// <summary>System.Audio.ChannelCount.</summary>
    public int? AudioChannels { get; init; }
}
