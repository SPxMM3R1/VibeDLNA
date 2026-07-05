namespace FolderDlnaServer;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        using var mutex = new Mutex(true, "VibeDLNA.SingleInstance", out var isFirstInstance);
        if (!isFirstInstance)
        {
            if (!args.Any(static arg => arg.Equals("--minimized", StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show(
                    "VibeDLNA ya esta abierto. Revisa el area de notificacion.",
                    "VibeDLNA",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }

            return;
        }

        var startMinimized = args.Any(static arg => arg.Equals("--minimized", StringComparison.OrdinalIgnoreCase));
        Application.Run(new MainForm(startMinimized));
    }
}
