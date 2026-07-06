using System.Runtime.InteropServices;

namespace FolderDlnaServer;

internal static class NativeTheme
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaSystemBackdropType = 38;
    private const int DwmwaMicaEffect = 1029;
    private const int DwmSystemBackdropNone = 1;
    private const int DwmSystemBackdropMainWindow = 2;

    public static void ApplyWindowEffects(Form form, bool dark)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763))
        {
            return;
        }

        TrySetWindowAttribute(form.Handle, DwmwaUseImmersiveDarkMode, dark ? 1 : 0);
        TryEnableMica(form.Handle);
    }

    public static void ApplyDarkTitleBar(Form form, bool dark)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763))
        {
            return;
        }

        TrySetWindowAttribute(form.Handle, DwmwaUseImmersiveDarkMode, dark ? 1 : 0);
    }

    public static void ApplyFlatWindow(Form form, bool dark)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763))
        {
            return;
        }

        TrySetWindowAttribute(form.Handle, DwmwaUseImmersiveDarkMode, dark ? 1 : 0);
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621))
        {
            TrySetWindowAttribute(form.Handle, DwmwaSystemBackdropType, DwmSystemBackdropNone);
        }
        else if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            TrySetWindowAttribute(form.Handle, DwmwaMicaEffect, 0);
        }
    }

    private static void TryEnableMica(IntPtr handle)
    {
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621))
        {
            TrySetWindowAttribute(handle, DwmwaSystemBackdropType, DwmSystemBackdropMainWindow);
            return;
        }

        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            TrySetWindowAttribute(handle, DwmwaMicaEffect, 1);
        }
    }

    private static void TrySetWindowAttribute(IntPtr handle, int attribute, int value)
    {
        try
        {
            _ = DwmSetWindowAttribute(handle, attribute, ref value, sizeof(int));
        }
        catch
        {
            // Older Windows builds simply keep the normal themed background.
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int attributeValue, int attributeSize);
}
