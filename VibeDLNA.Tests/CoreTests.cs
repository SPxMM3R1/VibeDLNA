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
    public void WinUiStatusIndicatorAllowsTransparentBackground()
    {
        using var indicator = new WinUiStatusIndicator();

        Assert.Equal(System.Drawing.Color.Transparent, indicator.BackColor);
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
    public void ThumbnailCacheReusesContentChecksumAcrossDifferentPaths()
    {
        var firstDirectory = Path.Combine(_root, "first");
        var secondDirectory = Path.Combine(_root, "second");
        Directory.CreateDirectory(firstDirectory);
        Directory.CreateDirectory(secondDirectory);
        var firstVideo = Path.Combine(firstDirectory, "video.mp4");
        var secondVideo = Path.Combine(secondDirectory, "renamed.mp4");
        var content = new byte[] { 1, 2, 3, 4, 5, 6 };
        File.WriteAllBytes(firstVideo, content);
        File.WriteAllBytes(secondVideo, content);

        var firstChecksum = ThumbnailCache.ComputeChecksum(firstVideo);
        var secondChecksum = ThumbnailCache.ComputeChecksum(secondVideo);
        var cache = new ThumbnailCache(Path.Combine(_root, "thumbnails"));
        File.WriteAllBytes(cache.GetCachePathForTesting(firstChecksum), new byte[] { 9, 8, 7 });

        Assert.Equal(firstChecksum, secondChecksum);
        Assert.Equal(firstChecksum, cache.GetOrCreate(secondVideo));
        Assert.True(cache.TryGetPath(secondChecksum, out _));
    }

    [Fact]
    public void DidlAdvertisesCachedVideoThumbnail()
    {
        var videoPath = Path.Combine(_root, "clip.mp4");
        File.WriteAllBytes(videoPath, new byte[] { 10, 20, 30 });
        var library = new DlnaContentLibrary(new[] { _root }, true, false, false);
        var entry = Assert.Single(library.GetChildren("R:0"), item => !item.IsDirectory);
        var cache = new ThumbnailCache(Path.Combine(_root, "thumbnails"));
        var checksum = ThumbnailCache.ComputeChecksum(videoPath);
        File.WriteAllBytes(cache.GetCachePathForTesting(checksum), new byte[] { 1, 2, 3 });
        Assert.Equal(checksum, cache.GetOrCreate(videoPath));

        var didl = DlnaXml.BuildDidl(new[] { entry }, "http://127.0.0.1:1234", cache);

        Assert.Contains("albumArtURI", didl);
        Assert.Contains($"/thumbnail/{checksum}.jpg", didl);
    }

    [Fact]
    public void DidlDoesNotBuildThumbnailDuringBrowse()
    {
        var videoPath = Path.Combine(_root, "uncached.mp4");
        File.WriteAllBytes(videoPath, new byte[] { 10, 20, 30 });
        var library = new DlnaContentLibrary(new[] { _root }, true, false, false);
        var entry = Assert.Single(library.GetChildren("R:0"), item => !item.IsDirectory);
        var cachePath = Path.Combine(_root, "thumbnails");
        var cache = new ThumbnailCache(cachePath);

        var didl = DlnaXml.BuildDidl(new[] { entry }, "http://127.0.0.1:1234", cache);

        Assert.DoesNotContain("albumArtURI", didl);
        Assert.Empty(Directory.EnumerateFiles(cachePath));
    }

    [Fact]
    public async Task ThumbnailResponseServesCachedResource()
    {
        var videoPath = Path.Combine(_root, "clip.mp4");
        File.WriteAllBytes(videoPath, new byte[] { 10, 20, 30 });
        var cache = new ThumbnailCache(Path.Combine(_root, "thumbnails"));
        var checksum = ThumbnailCache.ComputeChecksum(videoPath);
        File.WriteAllBytes(cache.GetCachePathForTesting(checksum), new byte[] { 1, 2, 3 });
        using var server = new DlnaServer(
            new[] { _root },
            "VibeDLNA Test",
            Guid.NewGuid().ToString("D"),
            0,
            true,
            false,
            false,
            false,
            false,
            cache);
        await server.StartAsync();

        using var client = new HttpClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Head,
            $"{server.BaseUrl}/thumbnail/{checksum}.jpg");
        using var response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        Assert.Equal("image/jpeg", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(3, response.Content.Headers.ContentLength);
    }

   [Theory]
    [InlineData("v1.2.3", "1.2.3")]
    [InlineData("1.4.0-beta", "1.4.0")]
    public void UpdateServiceParsesReleaseVersions(string tagName, string expected)
    {
        Assert.True(GitHubUpdateService.TryParseVersion(tagName, out var version));
        Assert.Equal(expected, version.ToString());
    }

    [Fact]
    public void DeviceDescriptionEscapesFriendlyName()
    {
        var xml = DlnaXml.DeviceDescription("TV & Sala <1>", Guid.NewGuid().ToString("D"));

        Assert.Contains("TV &amp; Sala &lt;1&gt;", xml);
        Assert.Contains("urn:schemas-upnp-org:device:MediaServer:1", xml);
    }

    [Fact]
    public void ContentDispositionKeepsReadableSpacesAndProvidesUtf8Name()
    {
        var header = DlnaServer.BuildContentDisposition("video loco \u00f1.mp4");

        Assert.Equal(
            "inline; filename=\"video loco _.mp4\"; filename*=UTF-8''video%20loco%20%C3%B1.mp4",
            header);
    }

    [Fact]
    public async Task MediaResponseAnnouncesReadableFileName()
    {
        const string fileName = "video loco.mp4";
        await File.WriteAllBytesAsync(Path.Combine(_root, fileName), new byte[] { 1, 2, 3 });
        var library = new DlnaContentLibrary(new[] { _root }, true, false, false);
        var entry = Assert.Single(library.GetChildren("R:0"), item => !item.IsDirectory);
        using var server = new DlnaServer(
            new[] { _root },
            "VibeDLNA Test",
            Guid.NewGuid().ToString("D"),
            0,
            true,
            false,
            false,
            false,
            false);
        await server.StartAsync();

        using var client = new HttpClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Head,
            $"{server.BaseUrl}/media/{Uri.EscapeDataString(entry.Id)}/{Uri.EscapeDataString(fileName)}");
        using var response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        Assert.Equal("\"video loco.mp4\"", response.Content.Headers.ContentDisposition?.FileName);
        Assert.Equal("video loco.mp4", response.Content.Headers.ContentDisposition?.FileNameStar);
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
