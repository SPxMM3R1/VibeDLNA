namespace FolderDlnaServer;

internal sealed class AppPalette
{
    public required bool IsDark { get; init; }

    public required Color Window { get; init; }

    public required Color Surface { get; init; }

    public required Color SurfaceAlt { get; init; }

    public required Color Elevated { get; init; }

    public required Color Border { get; init; }

    public required Color Text { get; init; }

    public required Color MutedText { get; init; }

    public required Color Accent { get; init; }

    public required Color AccentAlt { get; init; }

    public required Color Success { get; init; }

    public required Color Warning { get; init; }

    public required Color Danger { get; init; }

    public required Color ButtonText { get; init; }

    public static AppPalette Dark { get; } = new()
    {
        IsDark = true,
        Window = Color.FromArgb(12, 16, 24),
        Surface = Color.FromArgb(19, 25, 36),
        SurfaceAlt = Color.FromArgb(25, 33, 47),
        Elevated = Color.FromArgb(32, 41, 57),
        Border = Color.FromArgb(56, 68, 89),
        Text = Color.FromArgb(242, 246, 250),
        MutedText = Color.FromArgb(151, 164, 184),
        Accent = Color.FromArgb(231, 57, 156),
        AccentAlt = Color.FromArgb(67, 224, 142),
        Success = Color.FromArgb(61, 220, 139),
        Warning = Color.FromArgb(255, 184, 76),
        Danger = Color.FromArgb(255, 88, 98),
        ButtonText = Color.White
    };

    public static AppPalette Light { get; } = new()
    {
        IsDark = false,
        Window = Color.FromArgb(244, 247, 251),
        Surface = Color.FromArgb(255, 255, 255),
        SurfaceAlt = Color.FromArgb(238, 243, 249),
        Elevated = Color.FromArgb(248, 250, 253),
        Border = Color.FromArgb(207, 218, 232),
        Text = Color.FromArgb(25, 34, 48),
        MutedText = Color.FromArgb(92, 106, 126),
        Accent = Color.FromArgb(196, 32, 129),
        AccentAlt = Color.FromArgb(24, 151, 95),
        Success = Color.FromArgb(23, 143, 88),
        Warning = Color.FromArgb(185, 112, 20),
        Danger = Color.FromArgb(200, 48, 61),
        ButtonText = Color.White
    };
}
