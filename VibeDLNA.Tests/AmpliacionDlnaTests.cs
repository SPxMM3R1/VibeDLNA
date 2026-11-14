using System.Text;
using Xunit;

namespace FolderDlnaServer.Tests;

/// <summary>
/// Pruebas de la ampliación 1.1.0 del servidor (contrato §A a §D): datos técnicos, raíz,
/// identidad del reproductor y actividad de reproducción.
/// </summary>
public sealed class AmpliacionDlnaTests : IDisposable
{
    private const string Aac = "{00001610-0000-0010-8000-00AA00389B71}";
    private const string Ac3 = "{00000092-0000-0010-8000-00AA00389B71}";
    private const string DolbyDigitalPlus = "{A7FB87AF-2D02-42FB-A4D4-05CD93843BDD}";
    private const string UnknownWave = "{12345678-0000-0010-8000-00AA00389B71}";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "VibeDLNA.Tests", Guid.NewGuid().ToString("N"));

    public AmpliacionDlnaTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Theory]
    [InlineData("HEVC", null, DolbyDigitalPlus, 6, "HEVC · E-AC3 5.1")]
    [InlineData("H264", null, Aac, 2, "H.264 · AAC 2.0")]
    [InlineData("H264", null, null, null, "H.264")]
    [InlineData(null, null, Ac3, 6, "AC3 5.1")]
    [InlineData(null, "{34363248-0000-0010-8000-00AA00389B71}", Aac, 8, "H.264 · AAC 7.1")]
    [InlineData(null, null, UnknownWave, null, null)]
    [InlineData("WVC1", null, null, null, null)]
    public void DescripcionSigueElFormatoDelContrato(
        string? fourCc,
        string? compression,
        string? audioFormat,
        int? channels,
        string? expected)
    {
        var info = new MediaTechnicalInfo
        {
            VideoFourCc = fourCc,
            VideoCompression = compression,
            AudioFormat = audioFormat,
            AudioChannels = channels
        };

        Assert.Equal(expected, MediaDescription.Describe(info));
    }

    [Theory]
    [InlineData(8_000_000L, 1_000_000L)]
    [InlineData(12L, 1L)]
    [InlineData(7L, null)]
    [InlineData(0L, null)]
    [InlineData(null, null)]
    public void BitrateDeWindowsSeEnviaEnBytesPorSegundo(long? bits, long? expectedBytes)
    {
        var info = new MediaTechnicalInfo { TotalBitrateBits = bits };

        Assert.Equal(expectedBytes, MediaDescription.BitrateBytesPerSecond(info));
    }

    [Fact]
    public void ResolucionYCanalesSeOmitenCuandoWindowsNoLosSabe()
    {
        Assert.Null(MediaDescription.Resolution(new MediaTechnicalInfo { Width = 1920 }));
        Assert.Equal("1920x1080", MediaDescription.Resolution(new MediaTechnicalInfo { Width = 1920, Height = 1080 }));
        Assert.Null(MediaDescription.AudioChannelCount(new MediaTechnicalInfo()));
        Assert.Equal(2, MediaDescription.AudioChannelCount(new MediaTechnicalInfo { AudioChannels = 2 }));
    }

    [Fact]
    public void GuidDesconocidoNoSeLeeComoTextoDeCodec()
    {
        // Un GUID puede contener letras hexadecimales que parecen códecs («EAC3», «AAC»).
        var info = new MediaTechnicalInfo { AudioFormat = "{0EAC3AAC-1111-2222-3333-444455556666}" };

        Assert.Null(MediaDescription.AudioCodecName(info));
    }

    [Fact]
    public void DidlPublicaResolucionBitrateCanalesYDescripcionMedidosPorElCalentamiento()
    {
        var videoPath = Path.Combine(_root, "pelicula.mp4");
        File.WriteAllBytes(videoPath, new byte[] { 10, 20, 30 });
        var library = new DlnaContentLibrary(new[] { _root }, true, false, false);
        var entry = Assert.Single(library.GetChildren("R:0"), item => !item.IsDirectory);
        var technical = new FixedTechnicalProvider(new MediaTechnicalInfo
        {
            Width = 1920,
            Height = 1080,
            TotalBitrateBits = 8_000_000,
            VideoFourCc = "HEVC",
            AudioFormat = DolbyDigitalPlus,
            AudioChannels = 6
        });
        var cache = CreateCache(Path.Combine(_root, "thumbnails"), technical);

        var beforeWarmup = DlnaXml.BuildDidl(new[] { entry }, "http://127.0.0.1:1234", cache);
        Assert.DoesNotContain("resolution=", beforeWarmup);
        Assert.DoesNotContain("dc:description", beforeWarmup);

        Assert.NotNull(cache.GetOrCreate(videoPath));
        var afterWarmup = DlnaXml.BuildDidl(new[] { entry }, "http://127.0.0.1:1234", cache);

        Assert.Contains("resolution=\"1920x1080\"", afterWarmup);
        Assert.Contains("bitrate=\"1000000\"", afterWarmup);
        Assert.Contains("nrAudioChannels=\"6\"", afterWarmup);
        Assert.Contains("<dc:description>HEVC · E-AC3 5.1</dc:description>", afterWarmup);
    }

    [Fact]
    public void DidlOmiteLosDatosTecnicosQueWindowsNoConoce()
    {
        var videoPath = Path.Combine(_root, "mudo.mp4");
        File.WriteAllBytes(videoPath, new byte[] { 1, 2, 3 });
        var library = new DlnaContentLibrary(new[] { _root }, true, false, false);
        var entry = Assert.Single(library.GetChildren("R:0"), item => !item.IsDirectory);
        var cache = CreateCache(Path.Combine(_root, "thumbnails"), new FixedTechnicalProvider(new MediaTechnicalInfo()));

        Assert.NotNull(cache.GetOrCreate(videoPath));
        var didl = DlnaXml.BuildDidl(new[] { entry }, "http://127.0.0.1:1234", cache);

        Assert.DoesNotContain("resolution=", didl);
        Assert.DoesNotContain("bitrate=", didl);
        Assert.DoesNotContain("nrAudioChannels=", didl);
        Assert.DoesNotContain("dc:description", didl);
    }

    [Fact]
    public void DatosTecnicosSeMidenUnaVezPorContenidoYSobrevivenAlReinicio()
    {
        var first = Path.Combine(_root, "uno.mp4");
        var second = Path.Combine(_root, "copia.mp4");
        var content = new byte[] { 4, 3, 2, 1 };
        File.WriteAllBytes(first, content);
        File.WriteAllBytes(second, content);
        var cachePath = Path.Combine(_root, "thumbnails");
        var technical = new FixedTechnicalProvider(new MediaTechnicalInfo { Width = 640, Height = 360 });
        var cache = CreateCache(cachePath, technical);

        Assert.NotNull(cache.GetOrCreate(first));
        Assert.NotNull(cache.GetOrCreate(second));
        Assert.Equal(1, technical.Calls);
        Assert.Equal(640, cache.TryGetCachedTechnical(second)?.Width);

        var restarted = CreateCache(cachePath, technical);
        Assert.Equal(360, restarted.TryGetCachedTechnical(first)?.Height);
        Assert.Equal(1, technical.Calls);
    }

    [Fact]
    public void IndiceAnteriorSeCompletaConDatosTecnicosAlCalentar()
    {
        var videoPath = Path.Combine(_root, "antiguo.mp4");
        File.WriteAllBytes(videoPath, new byte[] { 5, 6, 7 });
        var cachePath = Path.Combine(_root, "thumbnails");
        Directory.CreateDirectory(cachePath);
        var info = new FileInfo(videoPath);
        var checksum = ThumbnailCache.ComputeChecksum(videoPath);

        // Índice escrito por 1.0.9: sin el campo Technical y con la duración ya medida.
        File.WriteAllText(Path.Combine(cachePath, "index.json"), $$"""
            [
              {
                "Path": "{{videoPath.Replace("\\", "\\\\")}}",
                "Length": {{info.Length}},
                "LastWriteUtcTicks": {{info.LastWriteTimeUtc.Ticks}},
                "Checksum": "{{checksum}}",
                "DurationTicks": 0
              }
            ]
            """);
        var technical = new FixedTechnicalProvider(new MediaTechnicalInfo { Width = 854, Height = 480 });
        var cache = CreateCache(cachePath, technical);

        Assert.Null(cache.TryGetCachedTechnical(videoPath));
        Assert.Equal(checksum, cache.GetOrCreate(videoPath));
        Assert.Equal(480, cache.TryGetCachedTechnical(videoPath)?.Height);
        Assert.Equal(1, technical.Calls);
    }

    [Fact]
    public void RaizAnunciaCarpetasYVideosContadosEnSegundoPlano()
    {
        var first = Path.Combine(_root, "Peliculas");
        var second = Path.Combine(_root, "Series");
        Directory.CreateDirectory(Path.Combine(first, "Clasicos"));
        Directory.CreateDirectory(second);
        File.WriteAllBytes(Path.Combine(first, "a.mp4"), new byte[] { 1 });
        File.WriteAllBytes(Path.Combine(first, "Clasicos", "b.mkv"), new byte[] { 1 });
        File.WriteAllBytes(Path.Combine(second, "c.mp4"), new byte[] { 1 });
        File.WriteAllText(Path.Combine(second, "notas.txt"), "no es video");
        File.WriteAllBytes(Path.Combine(second, "cover.jpg"), new byte[] { 1 });
        var library = new DlnaContentLibrary(new[] { first, second }, shareVideos: true, shareAudio: false, shareImages: false);

        Assert.Equal("2 carpetas", library.GetMetadata("0").Description);

        library.StartCountingVideos(CancellationToken.None).GetAwaiter().GetResult();

        Assert.Equal("2 carpetas · 3 videos", library.GetMetadata("0").Description);
    }

    [Fact]
    public void RaizSeEnviaComoDescripcionDelContenedor()
    {
        var library = new DlnaContentLibrary(new[] { _root }, true, false, false);
        library.StartCountingVideos(CancellationToken.None).GetAwaiter().GetResult();

        var didl = DlnaXml.BuildDidl(new[] { library.GetMetadata("0") }, "http://127.0.0.1:1234");

        Assert.Contains("<dc:description>1 carpeta · 0 videos</dc:description>", didl);
    }

    [Theory]
    [InlineData(3, 1284, "3 carpetas · 1.284 videos")]
    [InlineData(1, 1, "1 carpeta · 1 video")]
    [InlineData(2, null, "2 carpetas")]
    [InlineData(0, 0, "0 carpetas · 0 videos")]
    public void ResumenDeRaizUsaPuntoDeMilesYFormaSingular(int folders, int? videos, string expected)
    {
        Assert.Equal(expected, MediaDescription.FolderSummary(folders, videos));
    }

    [Fact]
    public void UserAgentDelPlayerSeInterpretaConSuIdentidad()
    {
        var client = ClientIdentity.Parse("VibeDLNA-Player/0.4.0 (Hisense 65A6; Living; id=4F1CA92E)", "192.168.1.52");

        Assert.Equal(ClientKind.VibeDlnaPlayer, client.Kind);
        Assert.Equal("4f1ca92e", client.PlayerId);
        Assert.Equal("Hisense 65A6", client.Model);
        Assert.Equal("Living", client.DeviceName);
        Assert.Equal("0.4.0", client.Version);
        Assert.Equal("VibeDLNA Player · Hisense 65A6", client.DisplayName);
        Assert.Equal("4f1c…a92e", client.ShortPlayerId);
        Assert.Equal("Hisense 65A6", client.Name);
        Assert.Equal("player|4f1ca92e", client.Key);
    }

    [Theory]
    [InlineData("VLC/3.0.18 LibVLC/3.0.18", "VLC 3.0.18", "Vlc")]
    [InlineData("Kodi/20.1 (Windows NT 10.0; Win64; x64) App_Bitness/64 Version/20.1-Nexus", "Kodi 20.1", "Kodi")]
    [InlineData("Mozilla/5.0 (Windows NT 10.0)", "Reproductor en 192.168.1.52", "Desconocido")]
    [InlineData("VibeDLNA-Player/0.4.0 (Hisense 65A6)", "Reproductor en 192.168.1.52", "Desconocido")]
    [InlineData(null, "Reproductor en 192.168.1.52", "Desconocido")]
    public void UserAgentAjenoSeNombraPorSuClienteOPorIp(string? userAgent, string expectedName, string expectedKind)
    {
        var client = ClientIdentity.Parse(userAgent, "192.168.1.52");

        Assert.Equal(expectedKind, client.Kind.ToString());
        Assert.Equal(expectedName, client.DisplayName);
        Assert.Null(client.PlayerId);
    }

    [Fact]
    public void PosicionSeCalculaPorInicioDeRangoYDuracion()
    {
        var quarter = ViewingRules.Measure(1_000_000, 4_000_000, TimeSpan.FromMinutes(2));
        Assert.Equal(25, quarter.Percent, precision: 6);
        Assert.Equal(TimeSpan.FromSeconds(30), quarter.Position);

        var threeQuarters = ViewingRules.Measure(3_000_000, 4_000_000, TimeSpan.FromMinutes(2));
        Assert.Equal(75, threeQuarters.Percent, precision: 6);
        Assert.Equal(TimeSpan.FromSeconds(90), threeQuarters.Position);

        var withoutDuration = ViewingRules.Measure(1_000_000, 4_000_000, null);
        Assert.Equal(25, withoutDuration.Percent, precision: 6);
        Assert.Null(withoutDuration.Position);

        var emptyFile = ViewingRules.Measure(0, 0, TimeSpan.FromMinutes(2));
        Assert.Equal(0, emptyFile.Percent);
        Assert.Null(emptyFile.Position);
    }

    [Fact]
    public void SeguirViendoSePodaPorAntiguedadYPorTerminado()
    {
        var now = new DateTimeOffset(2026, 11, 14, 12, 0, 0, TimeSpan.Zero);
        var entries = new List<ContinueWatchingEntry>
        {
            Entry("vieja", percent: 10, updatedAt: now.AddDays(-8)),
            Entry("terminada", percent: 96, updatedAt: now.AddDays(-1)),
            Entry("justo95", percent: 95, updatedAt: now.AddDays(-1)),
            Entry("semana", percent: 10, updatedAt: now.AddDays(-6.9))
        };

        var kept = ViewingRules.PruneContinueWatching(entries, now);

        Assert.Equal(new[] { "justo95", "semana" }, kept.Select(entry => entry.FilePath));
    }

    [Fact]
    public void SeguirViendoConservaComoMaximoCincuentaMasRecientes()
    {
        var now = new DateTimeOffset(2026, 11, 14, 12, 0, 0, TimeSpan.Zero);
        var entries = Enumerable.Range(0, 60)
            .Select(index => Entry($"reciente-{index}", percent: 10, updatedAt: now.AddMinutes(-index)))
            .ToList();

        var kept = ViewingRules.PruneContinueWatching(entries, now);

        Assert.Equal(50, kept.Count);
        Assert.Equal("reciente-0", kept[0].FilePath);
        Assert.Equal("reciente-49", kept[49].FilePath);
        Assert.Equal(now.AddMinutes(-49), kept[49].UpdatedAt);
    }

    [Fact]
    public void SeguirViendoSeGuardaYSeDepuraAlRecargar()
    {
        var clock = new MutableClock(new DateTimeOffset(2026, 11, 14, 12, 0, 0, TimeSpan.Zero));
        var dataDirectory = Path.Combine(_root, "appdata");
        var activity = new ViewingActivity(dataDirectory, () => clock.Now);
        var player = ClientIdentity.Parse("VibeDLNA-Player/0.4.0 (Hisense 65A6; Living; id=4f1ca92e)", "192.168.1.34");
        var file = Path.Combine(_root, "Peliculas", "Dune.mp4");

        activity.RecordVideoRange(player, file, 1_000_000, 4_000_000, TimeSpan.FromMinutes(2), null);

        var saved = Assert.Single(activity.GetContinueWatching());
        Assert.Equal("Dune", saved.Title);
        var savedText = File.ReadAllText(Path.Combine(dataDirectory, "seguir-viendo.json"));
        Assert.Contains("\"Title\": \"Dune\"", savedText);
        Assert.Contains("\"PlayerName\": \"Hisense 65A6\"", savedText);
        Assert.Equal(25, saved.Percent);
        Assert.Equal(30_000, saved.PositionMs);
        Assert.Equal(120_000, saved.DurationMs);
        Assert.Equal("Hisense 65A6", saved.PlayerName);
        Assert.True(File.Exists(Path.Combine(dataDirectory, "seguir-viendo.json")));

        clock.Now = clock.Now.AddDays(8);
        var reloaded = new ViewingActivity(dataDirectory, () => clock.Now);
        Assert.Empty(reloaded.GetContinueWatching());
    }

    [Fact]
    public void SeguirViendoGuardaLosTitulosConAcentosSinEscapar()
    {
        var clock = new MutableClock(new DateTimeOffset(2026, 11, 14, 12, 0, 0, TimeSpan.Zero));
        var dataDirectory = Path.Combine(_root, "appdata");
        var activity = new ViewingActivity(dataDirectory, () => clock.Now);
        var player = ClientIdentity.Parse("VibeDLNA-Player/0.4.0 (Hisense 65A6; Living; id=4f1ca92e)", "192.168.1.34");

        activity.RecordVideoRange(player, Path.Combine(_root, "Películas", "Ñandú Dune.mp4"), 1_000, 4_000, null, null);

        var text = File.ReadAllText(Path.Combine(dataDirectory, "seguir-viendo.json"));
        Assert.Contains("Ñandú Dune", text);
        Assert.Contains("Películas", text);
    }

    [Fact]
    public void VideoTerminadoSaleDeSeguirViendo()
    {
        var clock = new MutableClock(new DateTimeOffset(2026, 11, 14, 12, 0, 0, TimeSpan.Zero));
        var activity = new ViewingActivity(Path.Combine(_root, "appdata"), () => clock.Now);
        var player = ClientIdentity.Parse("VibeDLNA-Player/0.4.0 (Hisense 65A6; Living; id=4f1ca92e)", "192.168.1.34");
        var file = Path.Combine(_root, "Serie.mp4");

        activity.RecordVideoRange(player, file, 1_000_000, 4_000_000, TimeSpan.FromMinutes(2), null);
        Assert.Single(activity.GetContinueWatching());

        activity.RecordVideoRange(player, file, 3_900_000, 4_000_000, TimeSpan.FromMinutes(2), null);
        Assert.Empty(activity.GetContinueWatching());
    }

    [Fact]
    public void ClienteAjenoNoEntraEnSeguirViendoPeroSiEnViendoAhora()
    {
        var clock = new MutableClock(new DateTimeOffset(2026, 11, 14, 12, 0, 0, TimeSpan.Zero));
        var activity = new ViewingActivity(Path.Combine(_root, "appdata"), () => clock.Now);
        var vlc = ClientIdentity.Parse("VLC/3.0.18 LibVLC/3.0.18", "192.168.1.60");

        activity.RecordVideoRange(vlc, Path.Combine(_root, "clip.mkv"), 0, 1_000, null, null);

        Assert.Empty(activity.GetContinueWatching());
        var watching = Assert.Single(activity.GetWatchingNow());
        Assert.Equal("VLC", watching.ClientName);
    }

    [Fact]
    public void VideoSeVeQuinceSegundosYSuRegistroSeAgrupaPorSesentaSegundos()
    {
        var clock = new MutableClock(new DateTimeOffset(2026, 11, 14, 12, 0, 0, TimeSpan.Zero));
        var activity = new ViewingActivity(Path.Combine(_root, "appdata"), () => clock.Now);
        var messages = new List<string>();
        activity.Activity += (_, message) => messages.Add(message);
        var player = ClientIdentity.Parse("VibeDLNA-Player/0.4.0 (Hisense 65A6; Living; id=4f1ca92e)", "192.168.1.34");
        var file = Path.Combine(_root, "Prueba H264 AAC.mp4");

        activity.RecordVideoRange(player, file, 0, 4_000_000, null, null);
        Assert.Equal(new[] { "Hisense 65A6 reproduce Prueba H264 AAC" }, messages);
        Assert.Single(activity.GetWatchingNow());

        clock.Now = clock.Now.AddSeconds(10);
        activity.RecordVideoRange(player, file, 1_000_000, 4_000_000, null, null);
        Assert.Single(messages);

        clock.Now = clock.Now.AddSeconds(16);
        Assert.Empty(activity.GetWatchingNow());

        clock.Now = clock.Now.AddSeconds(14);
        activity.RecordVideoRange(player, file, 2_000_000, 4_000_000, null, null);
        Assert.Single(messages);

        clock.Now = clock.Now.AddSeconds(70);
        activity.RecordVideoRange(player, file, 2_000_000, 4_000_000, null, null);
        Assert.Equal(2, messages.Count);
    }

    [Fact]
    public void CarpetaAbiertaSeRegistraConElFormatoDelRegistro()
    {
        var clock = new MutableClock(new DateTimeOffset(2026, 11, 14, 12, 0, 0, TimeSpan.Zero));
        var activity = new ViewingActivity(Path.Combine(_root, "appdata"), () => clock.Now);
        var messages = new List<string>();
        activity.Activity += (_, message) => messages.Add(message);
        var client = ClientIdentity.Parse(null, "192.168.1.34");

        activity.RecordFolderOpened(client, @"D:\Películas", 8, 642);

        Assert.Equal(new[] { @"192.168.1.34 abrió D:\Películas (8 de 642)" }, messages);
    }

    [Fact]
    public void ReproductoresVistosSeGuardanYCaducanA30Dias()
    {
        var clock = new MutableClock(new DateTimeOffset(2026, 11, 14, 12, 0, 0, TimeSpan.Zero));
        var dataDirectory = Path.Combine(_root, "appdata");
        var activity = new ViewingActivity(dataDirectory, () => clock.Now);
        var player = ClientIdentity.Parse("VibeDLNA-Player/0.4.0 (Hisense 65A6; Living; id=4f1ca92e)", "192.168.1.34");

        activity.NoteClient(player);
        activity.Flush();

        Assert.True(File.Exists(Path.Combine(dataDirectory, "reproductores.json")));
        var reloaded = new ViewingActivity(dataDirectory, () => clock.Now);
        Assert.Equal("player|4f1ca92e", Assert.Single(reloaded.GetKnownPlayers()).Key);

        clock.Now = clock.Now.AddDays(31);
        Assert.Empty(new ViewingActivity(dataDirectory, () => clock.Now).GetKnownPlayers());
    }

    private ThumbnailCache CreateCache(string directory, IMediaTechnicalProvider technical) =>
        new(directory, new WritingThumbnailProvider(), new NoDurationProvider(), technical);

    private static ContinueWatchingEntry Entry(string file, double percent, DateTimeOffset updatedAt) =>
        new()
        {
            PlayerId = "4f1ca92e",
            PlayerName = "Hisense 65A6",
            FilePath = file,
            Title = file,
            Percent = percent,
            UpdatedAt = updatedAt
        };

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // Windows puede retener un identificador de archivo tras una prueba fallida.
        }
    }

    private sealed class FixedTechnicalProvider : IMediaTechnicalProvider
    {
        private readonly MediaTechnicalInfo _info;
        private int _calls;

        public FixedTechnicalProvider(MediaTechnicalInfo info)
        {
            _info = info;
        }

        public int Calls => Volatile.Read(ref _calls);

        public MediaTechnicalInfo TryGetInfo(string sourcePath)
        {
            Interlocked.Increment(ref _calls);
            return _info;
        }
    }

    private sealed class NoDurationProvider : IMediaDurationProvider
    {
        public TimeSpan? TryGetDuration(string sourcePath) => null;
    }

    private sealed class WritingThumbnailProvider : IThumbnailProvider
    {
        public bool TrySave(string sourcePath, string targetPath)
        {
            File.WriteAllBytes(targetPath, new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 });
            return true;
        }
    }

    private sealed class MutableClock
    {
        public MutableClock(DateTimeOffset now)
        {
            Now = now;
        }

        public DateTimeOffset Now { get; set; }
    }
}
