namespace FolderDlnaServer;

internal enum MediaKind
{
    Video,
    Audio,
    Image
}

internal sealed record MediaTypeInfo(string MimeType, string UpnpClass, MediaKind Kind);

internal static class MediaTypes
{
    private static readonly Dictionary<string, MediaTypeInfo> Types = new(StringComparer.OrdinalIgnoreCase)
    {
        [".3gp"] = new("video/3gpp", "object.item.videoItem", MediaKind.Video),
        [".avi"] = new("video/x-msvideo", "object.item.videoItem", MediaKind.Video),
        [".m2ts"] = new("video/mp2t", "object.item.videoItem", MediaKind.Video),
        [".m4v"] = new("video/mp4", "object.item.videoItem", MediaKind.Video),
        [".mkv"] = new("video/x-matroska", "object.item.videoItem", MediaKind.Video),
        [".mov"] = new("video/quicktime", "object.item.videoItem", MediaKind.Video),
        [".mp4"] = new("video/mp4", "object.item.videoItem", MediaKind.Video),
        [".mpeg"] = new("video/mpeg", "object.item.videoItem", MediaKind.Video),
        [".mpg"] = new("video/mpeg", "object.item.videoItem", MediaKind.Video),
        [".ts"] = new("video/mp2t", "object.item.videoItem", MediaKind.Video),
        [".webm"] = new("video/webm", "object.item.videoItem", MediaKind.Video),
        [".wmv"] = new("video/x-ms-wmv", "object.item.videoItem", MediaKind.Video),

        [".aac"] = new("audio/aac", "object.item.audioItem.musicTrack", MediaKind.Audio),
        [".aiff"] = new("audio/aiff", "object.item.audioItem.musicTrack", MediaKind.Audio),
        [".alac"] = new("audio/mp4", "object.item.audioItem.musicTrack", MediaKind.Audio),
        [".flac"] = new("audio/flac", "object.item.audioItem.musicTrack", MediaKind.Audio),
        [".m4a"] = new("audio/mp4", "object.item.audioItem.musicTrack", MediaKind.Audio),
        [".mp3"] = new("audio/mpeg", "object.item.audioItem.musicTrack", MediaKind.Audio),
        [".ogg"] = new("audio/ogg", "object.item.audioItem.musicTrack", MediaKind.Audio),
        [".wav"] = new("audio/wav", "object.item.audioItem.musicTrack", MediaKind.Audio),
        [".wma"] = new("audio/x-ms-wma", "object.item.audioItem.musicTrack", MediaKind.Audio),

        [".bmp"] = new("image/bmp", "object.item.imageItem.photo", MediaKind.Image),
        [".gif"] = new("image/gif", "object.item.imageItem.photo", MediaKind.Image),
        [".jpeg"] = new("image/jpeg", "object.item.imageItem.photo", MediaKind.Image),
        [".jpg"] = new("image/jpeg", "object.item.imageItem.photo", MediaKind.Image),
        [".png"] = new("image/png", "object.item.imageItem.photo", MediaKind.Image),
        [".tif"] = new("image/tiff", "object.item.imageItem.photo", MediaKind.Image),
        [".tiff"] = new("image/tiff", "object.item.imageItem.photo", MediaKind.Image),
        [".webp"] = new("image/webp", "object.item.imageItem.photo", MediaKind.Image)
    };

    public static bool TryGet(string path, out MediaTypeInfo typeInfo) =>
        Types.TryGetValue(Path.GetExtension(path), out typeInfo!);

    public static bool IsAllowed(MediaTypeInfo typeInfo, bool videos, bool audio, bool images) =>
        typeInfo.Kind switch
        {
            MediaKind.Video => videos,
            MediaKind.Audio => audio,
            MediaKind.Image => images,
            _ => false
        };

    public static string GetProtocolInfo(string mimeType) =>
        $"http-get:*:{mimeType}:DLNA.ORG_OP=01;DLNA.ORG_FLAGS=01700000000000000000000000000000";

    public static string GetSourceProtocolInfo(bool videos = true, bool audio = true, bool images = true) =>
        string.Join(',', Types.Values
            .Where(type => IsAllowed(type, videos, audio, images))
            .Select(type => GetProtocolInfo(type.MimeType))
            .Distinct(StringComparer.OrdinalIgnoreCase));
}
