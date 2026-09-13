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
        Window = Color.FromArgb(9, 13, 20),
        Surface = Color.FromArgb(20, 26, 38),
        SurfaceAlt = Color.FromArgb(26, 34, 49),
        Elevated = Color.FromArgb(34, 43, 60),
        Border = Color.FromArgb(62, 75, 98),
        Text = Color.FromArgb(242, 246, 250),
        MutedText = Color.FromArgb(151, 164, 184),
        Accent = Color.FromArgb(25, 137, 226),
        AccentAlt = Color.FromArgb(67, 224, 142),
        Success = Color.FromArgb(61, 220, 139),
        Warning = Color.FromArgb(255, 184, 76),
        Danger = Color.FromArgb(255, 88, 98),
        ButtonText = Color.White
    };

    public static AppPalette Light { get; } = new()
    {
        IsDark = false,
        Window = Color.FromArgb(241, 245, 250),
        Surface = Color.FromArgb(252, 254, 255),
        SurfaceAlt = Color.FromArgb(236, 242, 248),
        Elevated = Color.FromArgb(246, 250, 253),
        Border = Color.FromArgb(200, 213, 229),
        Text = Color.FromArgb(25, 34, 48),
        MutedText = Color.FromArgb(92, 106, 126),
        Accent = Color.FromArgb(15, 108, 189),
        AccentAlt = Color.FromArgb(24, 151, 95),
        Success = Color.FromArgb(23, 143, 88),
        Warning = Color.FromArgb(185, 112, 20),
        Danger = Color.FromArgb(200, 48, 61),
        ButtonText = Color.White
    };
}
