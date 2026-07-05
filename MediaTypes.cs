namespace FolderDlnaServer;

internal sealed record MediaTypeInfo(string MimeType, string UpnpClass);

internal static class MediaTypes
{
    private static readonly Dictionary<string, MediaTypeInfo> Types = new(StringComparer.OrdinalIgnoreCase)
    {
        [".3gp"] = new("video/3gpp", "object.item.videoItem"),
        [".avi"] = new("video/x-msvideo", "object.item.videoItem"),
        [".m2ts"] = new("video/mp2t", "object.item.videoItem"),
        [".m4v"] = new("video/mp4", "object.item.videoItem"),
        [".mkv"] = new("video/x-matroska", "object.item.videoItem"),
        [".mov"] = new("video/quicktime", "object.item.videoItem"),
        [".mp4"] = new("video/mp4", "object.item.videoItem"),
        [".mpeg"] = new("video/mpeg", "object.item.videoItem"),
        [".mpg"] = new("video/mpeg", "object.item.videoItem"),
        [".ts"] = new("video/mp2t", "object.item.videoItem"),
        [".webm"] = new("video/webm", "object.item.videoItem"),
        [".wmv"] = new("video/x-ms-wmv", "object.item.videoItem"),

        [".aac"] = new("audio/aac", "object.item.audioItem.musicTrack"),
        [".aiff"] = new("audio/aiff", "object.item.audioItem.musicTrack"),
        [".alac"] = new("audio/mp4", "object.item.audioItem.musicTrack"),
        [".flac"] = new("audio/flac", "object.item.audioItem.musicTrack"),
        [".m4a"] = new("audio/mp4", "object.item.audioItem.musicTrack"),
        [".mp3"] = new("audio/mpeg", "object.item.audioItem.musicTrack"),
        [".ogg"] = new("audio/ogg", "object.item.audioItem.musicTrack"),
        [".wav"] = new("audio/wav", "object.item.audioItem.musicTrack"),
        [".wma"] = new("audio/x-ms-wma", "object.item.audioItem.musicTrack"),

        [".bmp"] = new("image/bmp", "object.item.imageItem.photo"),
        [".gif"] = new("image/gif", "object.item.imageItem.photo"),
        [".jpeg"] = new("image/jpeg", "object.item.imageItem.photo"),
        [".jpg"] = new("image/jpeg", "object.item.imageItem.photo"),
        [".png"] = new("image/png", "object.item.imageItem.photo"),
        [".tif"] = new("image/tiff", "object.item.imageItem.photo"),
        [".tiff"] = new("image/tiff", "object.item.imageItem.photo"),
        [".webp"] = new("image/webp", "object.item.imageItem.photo")
    };

    public static bool TryGet(string path, out MediaTypeInfo typeInfo) =>
        Types.TryGetValue(Path.GetExtension(path), out typeInfo!);

    public static string GetProtocolInfo(string mimeType) =>
        $"http-get:*:{mimeType}:DLNA.ORG_OP=01;DLNA.ORG_FLAGS=01700000000000000000000000000000";

    public static string GetSourceProtocolInfo() =>
        string.Join(',', Types.Values.Select(type => GetProtocolInfo(type.MimeType)).Distinct(StringComparer.OrdinalIgnoreCase));
}
