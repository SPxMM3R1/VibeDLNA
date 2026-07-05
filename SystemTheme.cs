using Microsoft.Win32;

namespace FolderDlnaServer;

internal static class SystemTheme
{
    private const string PersonalizePath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    public static bool IsDarkAppMode()
    {
        using var key = Registry.CurrentUser.OpenSubKey(PersonalizePath);
        return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
    }

    public static AppPalette Resolve(AppThemeMode mode) =>
        mode switch
        {
            AppThemeMode.Dark => AppPalette.Dark,
            AppThemeMode.Light => AppPalette.Light,
            _ => IsDarkAppMode() ? AppPalette.Dark : AppPalette.Light
        };
}
