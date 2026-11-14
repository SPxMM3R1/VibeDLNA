using Xunit;

namespace FolderDlnaServer.Tests;

/// <summary>
/// Pruebas de la lógica que usa la ventana 1.1.0: formatos, filas de «Viendo ahora», registro
/// de actividad y conteo de la biblioteca.
/// </summary>
public sealed class VentanaTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 14, 16, 0, 0, TimeSpan.Zero);

    private readonly string _root = Path.Combine(Path.GetTempPath(), "VibeDLNA.Tests", Guid.NewGuid().ToString("N"));

    public VentanaTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Theory]
    [InlineData(1284, "1.284")]
    [InlineData(999, "999")]
    [InlineData(1000, "1.000")]
    [InlineData(1234567, "1.234.567")]
    [InlineData(0, "0")]
    public void MilesConPunto(long value, string expected)
    {
        Assert.Equal(expected, ViewFormat.Thousands(value));
    }

    [Theory]
    [InlineData(1.9, "TB", "1,9 TB")]
    [InlineData(9.4, "GB", "9,4 GB")]
    [InlineData(31, "GB", "31 GB")]
    [InlineData(210, "GB", "210 GB")]
    public void TamanosConComaDecimal(double amount, string unit, string expected)
    {
        var exponent = unit switch
        {
            "TB" => 4,
            "GB" => 3,
            _ => 2
        };
        var bytes = (long)(amount * Math.Pow(1024, exponent));

        Assert.Equal(expected, ViewFormat.Bytes(bytes));
    }

    [Fact]
    public void RelojDeReproduccionConHorasOSinEllas()
    {
        Assert.Equal("1:12:40", ViewFormat.Clock(TimeSpan.FromSeconds(4360)));
        Assert.Equal("4:05", ViewFormat.Clock(TimeSpan.FromSeconds(245)));
        Assert.Equal("0:00", ViewFormat.Clock(TimeSpan.Zero));
    }

    [Fact]
    public void ProgresoConDuracionMuestraPosicionYSinDuracionPorcentaje()
    {
        Assert.Equal(
            "1:12:40 de 2:46:00",
            ViewFormat.Progress(TimeSpan.FromSeconds(4360), TimeSpan.FromSeconds(9960), 43.7));
        Assert.Equal("44 %", ViewFormat.Progress(null, null, 43.7));
        Assert.Equal("44 %", ViewFormat.Progress(TimeSpan.FromSeconds(10), null, 43.7));
    }

    [Fact]
    public void FilaDeViendoAhoraIncluyeReproductorYPosicion()
    {
        Assert.Equal(
            "VibeDLNA Player · Hisense 65A6 · 1:12:40 de 2:46:00",
            ViewFormat.NowPlayingMeta(
                "VibeDLNA Player · Hisense 65A6",
                TimeSpan.FromSeconds(4360),
                TimeSpan.FromSeconds(9960),
                43.7));
    }

    [Theory]
    [InlineData(-10, "hace un momento")]
    [InlineData(-59, "hace un momento")]
    [InlineData(-60, "hace 1 minuto")]
    [InlineData(-120 * 60, "hace 2 horas")]
    [InlineData(-3 * 24 * 60 * 60, "hace 3 días")]
    [InlineData(-45 * 24 * 60 * 60, "hace más de un mes")]
    public void HaceCuantoSeDice(int secondsAgo, string expected)
    {
        Assert.Equal(expected, ViewFormat.Ago(Now.AddSeconds(secondsAgo), Now));
    }

    [Fact]
    public void ResumenDeCarpetaComoElMockup()
    {
        Assert.Equal(
            "642 videos · 1,9 TB",
            ViewFormat.FolderSummary(642, 0, 0, (long)(1.9 * Math.Pow(1024, 4))));
        Assert.Equal(
            "312 canciones · 9,4 GB",
            ViewFormat.FolderSummary(0, 312, 0, (long)(9.4 * Math.Pow(1024, 3))));
        Assert.Equal("Sin archivos compatibles", ViewFormat.FolderSummary(0, 0, 0, 0));
    }

    [Fact]
    public void ReproductorVistoMuestraNombreSoloSiEsDistintoDelModelo()
    {
        Assert.Equal("Living · uuid 4f1c…a92e", ViewFormat.PlayerMeta("Living", "Hisense 65A6", "4f1ca92e", "192.168.1.34"));
        Assert.Equal("uuid 4f1c…a92e", ViewFormat.PlayerMeta("Hisense 65A6", "Hisense 65A6", "4f1ca92e", "192.168.1.34"));
        Assert.Equal("192.168.1.52", ViewFormat.PlayerMeta(null, null, null, "192.168.1.52"));
    }

    [Theory]
    [InlineData("Hisense 65A6 reproduce Dune: Parte dos (2160p HEVC)", "Play")]
    [InlineData(@"Hisense 65A6 abrió D:\Películas (8 de 642)", "Nav")]
    [InlineData("Pixel 8 pidió un rango fuera del archivo · Amor.mp4 (416)", "Err")]
    [InlineData("Error al iniciar: el puerto está ocupado", "Err")]
    [InlineData("Servidor iniciado en http://192.168.1.20:8210/description.xml", "Sys")]
    [InlineData("Miniaturas: 1.190 de 1.284 listas; quedan 94 de D:\\Series", "Sys")]
    public void RegistroSeClasificaPorSuFrase(string message, string expected)
    {
        Assert.Equal(expected, ActivityLog.Classify(message).ToString());
    }

    [Fact]
    public void FiltrosDelRegistroPorTipoYTexto()
    {
        var entries = new[]
        {
            new ActivityEntry(Now.UtcDateTime, ActivityKind.Play, "Hisense 65A6 reproduce Dune"),
            new ActivityEntry(Now.UtcDateTime, ActivityKind.Nav, "Hisense 65A6 abrió D:\\Películas (8 de 642)"),
            new ActivityEntry(Now.UtcDateTime, ActivityKind.Sys, "Biblioteca DLNA actualizada."),
            new ActivityEntry(Now.UtcDateTime, ActivityKind.Err, "Pixel 8 pidió un rango fuera del archivo · Amor.mp4 (416)")
        };

        Assert.Equal(4, ActivityLog.Apply(entries, ActivityFilter.All, null).Count);
        Assert.Equal(ActivityKind.Play, Assert.Single(ActivityLog.Apply(entries, ActivityFilter.Play, null)).Kind);
        Assert.Equal(ActivityKind.Err, Assert.Single(ActivityLog.Apply(entries, ActivityFilter.Err, null)).Kind);
        Assert.Single(ActivityLog.Apply(entries, ActivityFilter.All, "AMOR"));
        Assert.Empty(ActivityLog.Apply(entries, ActivityFilter.Sys, "dune"));
        Assert.Single(ActivityLog.Apply(entries, ActivityFilter.Play, "  dune  "));
    }

    [Fact]
    public void ViendoAhoraLlevaElNombreDelReproductorYSuClave()
    {
        var clock = new MutableClock(Now);
        var activity = new ViewingActivity(Path.Combine(_root, "appdata"), () => clock.Now);
        var player = ClientIdentity.Parse("VibeDLNA-Player/0.4.0 (Hisense 65A6; Living; id=4f1ca92e)", "192.168.1.34");

        activity.RecordVideoRange(player, Path.Combine(_root, "Dune.mp4"), 0, 1_000, TimeSpan.FromMinutes(10), null);

        var watching = Assert.Single(activity.GetWatchingNow());
        Assert.Equal("Hisense 65A6", watching.ClientName);
        Assert.Equal("VibeDLNA Player · Hisense 65A6", watching.ClientDisplayName);
        Assert.Equal("player|4f1ca92e", watching.ClientKey);
    }

    [Fact]
    public void RangoFueraDelArchivoSeRegistraConSuFrase()
    {
        var activity = new ViewingActivity(Path.Combine(_root, "appdata"), () => Now);
        var messages = new List<string>();
        activity.Activity += (_, message) => messages.Add(message);
        var pixel = ClientIdentity.Parse("VibeDLNA-Player/0.4.0 (Pixel 8; Pixel 8; id=9b0231d7)", "192.168.1.52");

        activity.RecordRangeOutOfBounds(pixel, Path.Combine(_root, "Amor.mp4"));

        Assert.Equal(new[] { "Pixel 8 pidió un rango fuera del archivo · Amor.mp4 (416)" }, messages);
    }

    [Fact]
    public void BibliotecaCuentaPorTipoSinAbrirArchivosYSigueLasMiniaturas()
    {
        var folder = Path.Combine(_root, "Media");
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, "a.mp4"), new byte[] { 1, 2, 3, 4 });
        File.WriteAllBytes(Path.Combine(folder, "b.mkv"), new byte[] { 1, 2 });
        File.WriteAllBytes(Path.Combine(folder, "c.mp3"), new byte[] { 1, 2, 3 });
        File.WriteAllBytes(Path.Combine(folder, "d.jpg"), new byte[] { 1 });
        File.WriteAllText(Path.Combine(folder, "notas.txt"), "no cuenta");
        var cache = new ThumbnailCache(Path.Combine(_root, "thumbnails"), new WritingThumbnailProvider(), new NoDurationProvider(), new EmptyTechnicalProvider());
        var tracker = new LibraryTracker(cache, () => false);

        tracker.Begin(new[] { folder }, shareVideos: true, shareAudio: true, shareImages: true, CancellationToken.None);
        var counted = WaitFor(() => tracker.Snapshot().Counted);

        Assert.True(counted);
        var overview = tracker.Snapshot();
        Assert.Equal(2, overview.Videos);
        Assert.Equal(1, overview.Audio);
        Assert.Equal(1, overview.Images);
        Assert.Equal(4, overview.ThumbnailsTotal);
        Assert.Equal(0, overview.ThumbnailsReady);
        var stats = Assert.Single(overview.Folders);
        Assert.True(stats.Available);
        Assert.Equal(10, stats.Bytes);
        Assert.Equal(4, stats.Files);

        Assert.NotNull(cache.GetOrCreate(Path.Combine(folder, "a.mp4")));
        tracker.Begin(new[] { folder }, shareVideos: true, shareAudio: true, shareImages: true, CancellationToken.None);
        var ready = WaitFor(() => tracker.Snapshot().ThumbnailsReady == 1);

        Assert.True(ready);
        Assert.Equal(25, tracker.Snapshot().Folders[0].Percent);
        tracker.Stop();
    }

    [Fact]
    public void BibliotecaRespetaLosTiposCompartidos()
    {
        var folder = Path.Combine(_root, "Solo");
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, "a.mp4"), new byte[] { 1 });
        File.WriteAllBytes(Path.Combine(folder, "b.mp3"), new byte[] { 1 });
        var cache = new ThumbnailCache(Path.Combine(_root, "thumbnails"), new WritingThumbnailProvider(), new NoDurationProvider(), new EmptyTechnicalProvider());
        var tracker = new LibraryTracker(cache, () => false);

        tracker.Begin(new[] { folder }, shareVideos: false, shareAudio: true, shareImages: false, CancellationToken.None);
        WaitFor(() => tracker.Snapshot().Counted);

        var overview = tracker.Snapshot();
        Assert.Equal(0, overview.Videos);
        Assert.Equal(1, overview.Audio);
        tracker.Stop();
    }

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

    private static bool WaitFor(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return true;
            }

            Thread.Sleep(20);
        }

        return condition();
    }

    private sealed class WritingThumbnailProvider : IThumbnailProvider
    {
        public bool TrySave(string sourcePath, string targetPath)
        {
            File.WriteAllBytes(targetPath, new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 });
            return true;
        }
    }

    private sealed class NoDurationProvider : IMediaDurationProvider
    {
        public TimeSpan? TryGetDuration(string sourcePath) => null;
    }

    private sealed class EmptyTechnicalProvider : IMediaTechnicalProvider
    {
        public MediaTechnicalInfo TryGetInfo(string sourcePath) => new();
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
