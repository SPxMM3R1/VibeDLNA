using System.Globalization;
using System.Text.RegularExpressions;

namespace FolderDlnaServer;

/// <summary>
/// Convierte los datos técnicos de Windows en el texto y los atributos del contrato §A/§B.
/// Funciones puras (sin disco ni COM) para poder probarlas. Un dato desconocido devuelve
/// null y el atributo o la parte del texto se omite; nunca se inventa.
/// </summary>
internal static class MediaDescription
{
    /// <summary>Subtipo KS de Dolby Digital Plus (E-AC3), que Windows reporta como GUID.</summary>
    private const string DolbyDigitalPlusSubtype = "{A7FB87AF-2D02-42FB-A4D4-05CD93843BDD}";

    /// <summary>Subtipo de formato WAVE: {tag hexadecimal de 8 dígitos}-0000-0010-8000-00AA00389B71.</summary>
    private static readonly Regex WaveSubtype = new(
        @"^\{(?<tag>[0-9A-F]{8})-0000-0010-8000-00AA00389B71\}$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>Cualquier GUID con llaves. Un GUID desconocido no debe leerse como texto de códec.</summary>
    private static readonly Regex GuidShape = new(
        @"^\{[0-9A-F]{8}-[0-9A-F]{4}-[0-9A-F]{4}-[0-9A-F]{4}-[0-9A-F]{12}\}$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>
    /// Texto de «res@resolution»: «3840x2160». Null si falta alguna dimensión.
    /// </summary>
    public static string? Resolution(MediaTechnicalInfo info)
    {
        if (info.Width is > 0 && info.Height is > 0)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{info.Width.Value}x{info.Height.Value}");
        }

        return null;
    }

    /// <summary>
    /// «res@bitrate» en bytes por segundo (UPnP): los bits de Windows divididos entre 8.
    /// </summary>
    public static long? BitrateBytesPerSecond(MediaTechnicalInfo info)
    {
        if (info.TotalBitrateBits is not > 0)
        {
            return null;
        }

        var bytes = info.TotalBitrateBits.Value / 8;
        return bytes > 0 ? bytes : null;
    }

    /// <summary>«res@nrAudioChannels»: número de canales, o null si Windows no lo sabe.</summary>
    public static int? AudioChannelCount(MediaTechnicalInfo info) =>
        info.AudioChannels is > 0 ? info.AudioChannels : null;

    /// <summary>
    /// Texto de <c>dc:description</c>: ««códec video» · «códec audio» «canales»», por ejemplo
    /// «HEVC · E-AC3 5.1» o «H.264 · AAC 2.0». Cada parte aparece solo si se conoce.
    /// Null si no hay ninguna parte conocida.
    /// </summary>
    public static string? Describe(MediaTechnicalInfo info)
    {
        var video = VideoCodecName(info);
        var audio = AudioCodecName(info);
        string? audioPart = null;
        if (audio is not null)
        {
            var layout = ChannelLayout(info.AudioChannels ?? 0);
            audioPart = layout is null ? audio : $"{audio} {layout}";
        }

        var parts = new[] { video, audioPart }.OfType<string>().ToArray();
        return parts.Length == 0 ? null : string.Join(" · ", parts);
    }

    /// <summary>
    /// Etiqueta corta de calidad para el registro: «2160p HEVC», «720p», «HEVC». Null si no hay datos.
    /// </summary>
    public static string? QualityLabel(MediaTechnicalInfo info)
    {
        var parts = new List<string>();
        if (info.Height is > 0)
        {
            parts.Add($"{info.Height.Value}p");
        }

        if (VideoCodecName(info) is { } video)
        {
            parts.Add(video);
        }

        return parts.Count == 0 ? null : string.Join(' ', parts);
    }

    /// <summary>Nombre del códec de video (H.264, HEVC, VP9, AV1, MPEG-2), o null.</summary>
    public static string? VideoCodecName(MediaTechnicalInfo info)
    {
        var fromFourCc = VideoCodecFromText(info.VideoFourCc);
        if (fromFourCc is not null)
        {
            return fromFourCc;
        }

        var compression = FourCcFromSubtype(info.VideoCompression)
            ?? (IsGuid(info.VideoCompression) ? null : info.VideoCompression);
        return VideoCodecFromText(compression);
    }

    /// <summary>Nombre del códec de audio (AAC, AC3, E-AC3, DTS, TrueHD, FLAC, Opus, MP3), o null.</summary>
    public static string? AudioCodecName(MediaTechnicalInfo info)
    {
        var raw = info.AudioFormat?.Trim();
        if (string.IsNullOrEmpty(raw))
        {
            return null;
        }

        if (raw.Equals(DolbyDigitalPlusSubtype, StringComparison.OrdinalIgnoreCase))
        {
            return "E-AC3";
        }

        var wave = WaveSubtype.Match(raw);
        if (wave.Success)
        {
            return WaveFormatName(Convert.ToUInt32(wave.Groups["tag"].Value, 16));
        }

        return IsGuid(raw) ? null : AudioCodecFromText(raw);
    }

    /// <summary>Disposición de canales legible: 1.0, 2.0, 5.1, 7.1. Null si no es una disposición común.</summary>
    public static string? ChannelLayout(int channels) => channels switch
    {
        1 => "1.0",
        2 => "2.0",
        6 => "5.1",
        8 => "7.1",
        _ => null
    };

    /// <summary>
    /// Pie de carpeta de la raíz (§B): «3 carpetas · 1.284 videos». Si el número de videos
    /// aún no se conoce, se omite esa parte.
    /// </summary>
    public static string FolderSummary(int folders, int? videos)
    {
        var parts = new List<string>
        {
            $"{Thousands(folders)} {(folders == 1 ? "carpeta" : "carpetas")}"
        };

        if (videos is { } count)
        {
            parts.Add($"{Thousands(count)} {(count == 1 ? "video" : "videos")}");
        }

        return string.Join(" · ", parts);
    }

    /// <summary>Convierte un valor de 32 bits de FourCC (orden little-endian) en texto, o null si no es ASCII imprimible.</summary>
    public static string? DecodeFourCc(uint value)
    {
        var chars = new char[4];
        for (var index = 0; index < 4; index++)
        {
            var character = (byte)(value >> (8 * index));
            if (character is < 0x20 or > 0x7E)
            {
                return null;
            }

            chars[index] = (char)character;
        }

        return new string(chars);
    }

    private static bool IsGuid(string? raw) => raw is not null && GuidShape.IsMatch(raw.Trim());

    private static string? FourCcFromSubtype(string? raw)
    {
        if (raw is null)
        {
            return null;
        }

        var match = WaveSubtype.Match(raw.Trim());
        return match.Success
            ? DecodeFourCc(Convert.ToUInt32(match.Groups["tag"].Value, 16))
            : null;
    }

    private static string? VideoCodecFromText(string? text)
    {
        var token = Token(text);
        if (token.Length == 0)
        {
            return null;
        }

        if (token.Contains("HEVC") || token.Contains("H265") || token.Contains("HEV1")
            || token.Contains("HVC1") || token.Contains("X265"))
        {
            return "HEVC";
        }

        if (token.Contains("H264") || token.Contains("AVC") || token.Contains("X264"))
        {
            return "H.264";
        }

        if (token.Contains("VP9"))
        {
            return "VP9";
        }

        if (token.Contains("AV1") || token.Contains("AV01"))
        {
            return "AV1";
        }

        if (token.Contains("MPEG2") || token.Contains("MP2V") || token.Contains("MPG2"))
        {
            return "MPEG-2";
        }

        return null;
    }

    private static string? AudioCodecFromText(string text)
    {
        var token = Token(text);
        if (token.Contains("EAC3") || token.Contains("DDPLUS") || token.Contains("DIGITALPLUS")
            || token.Contains("EC3"))
        {
            return "E-AC3";
        }

        if (token.Contains("AC3") || token.Contains("DOLBYDIGITAL"))
        {
            return "AC3";
        }

        if (token.Contains("TRUEHD") || token.Contains("MLP"))
        {
            return "TrueHD";
        }

        if (token.Contains("DTS"))
        {
            return "DTS";
        }

        if (token.Contains("FLAC"))
        {
            return "FLAC";
        }

        if (token.Contains("OPUS"))
        {
            return "Opus";
        }

        if (token.Contains("AAC") || token.StartsWith("MP4A", StringComparison.Ordinal))
        {
            return "AAC";
        }

        if (token.Contains("MP3") || token.Contains("MPEGLAYER3"))
        {
            return "MP3";
        }

        return null;
    }

    /// <summary>Tags WAVE conocidos (wFormatTag) a nombre de códec.</summary>
    private static string? WaveFormatName(uint tag) => tag switch
    {
        0x00FF or 0x1600 or 0x1601 or 0x1610 => "AAC",
        0x0055 => "MP3",
        0x2000 or 0x0092 => "AC3",
        0x2001 => "DTS",
        0xF1AC => "FLAC",
        0x704F => "Opus",
        _ => null
    };

    /// <summary>Texto en mayúsculas sin signos: «H.264» → «H264», «E-AC3» → «EAC3».</summary>
    private static string Token(string? text) =>
        text is null
            ? string.Empty
            : new string(text.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

    private static string Thousands(long value) =>
        value.ToString("N0", CultureInfo.InvariantCulture).Replace(',', '.');
}
