using System.Text;
using Xunit;

namespace FolderDlnaServer.Tests;

public sealed class CoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "VibeDLNA.Tests", Guid.NewGuid().ToString("N"));

    public CoreTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void NormalizeMigratesLegacyFolderAndKeepsAtLeastOneMediaType()
    {
        var settings = new AppSettings
        {
            MediaFolder = _root,
            MediaFolders = new List<string>(),
            ShareVideos = false,
            ShareAudio = false,
            ShareImages = false,
            Uuid = "invalid",
            Port = -1
        };

        SettingsService.Normalize(settings);

        Assert.Equal(_root, settings.MediaFolders.Single());
        Assert.True(settings.ShareVideos);
        Assert.True(Guid.TryParse(settings.Uuid, out _));
        Assert.Equal(0, settings.Port);
    }

    [Fact]
    public void LibraryFiltersMediaAndEnumeratesFolders()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Series"));
        File.WriteAllBytes(Path.Combine(_root, "video.mp4"), new byte[] { 1, 2, 3 });
        File.WriteAllText(Path.Combine(_root, "notes.txt"), "ignored");
        File.WriteAllBytes(Path.Combine(_root, "cover.jpg"), new byte[] { 4, 5 });
        var library = new DlnaContentLibrary(new[] { _root }, shareVideos: true, shareAudio: false, shareImages: false);

        var entries = library.GetChildren("R:0");

        Assert.Contains(entries, entry => entry.IsDirectory && entry.Title == "Series");
        Assert.Contains(entries, entry => !entry.IsDirectory && entry.Title == "video");
        Assert.DoesNotContain(entries, entry => entry.Title is "notes" or "cover");
    }

    [Fact]
    public void LibraryRejectsObjectIdsOutsideSharedRoot()
    {
        var library = new DlnaContentLibrary(new[] { _root }, true, true, true);
        var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes("0|..\\outside.mp4"))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        Assert.Throws<UnauthorizedAccessException>(() => library.GetMetadata($"F:{payload}"));
    }

    [Theory]
    [InlineData("movie.mkv", "Video")]
    [InlineData("song.flac", "Audio")]
    [InlineData("photo.webp", "Image")]
    public void MediaTypesRecognizeCommonExtensions(string fileName, string expectedKind)
    {
        Assert.True(MediaTypes.TryGet(fileName, out var mediaType));
        Assert.Equal(expectedKind, mediaType.Kind.ToString());
    }

    [Fact]
    public void DeviceDescriptionEscapesFriendlyName()
    {
        var xml = DlnaXml.DeviceDescription("TV & Sala <1>", Guid.NewGuid().ToString("D"));

        Assert.Contains("TV &amp; Sala &lt;1&gt;", xml);
        Assert.Contains("urn:schemas-upnp-org:device:MediaServer:1", xml);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // Windows may briefly retain a file handle after a failed test.
        }
    }
}
