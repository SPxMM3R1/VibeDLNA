namespace FolderDlnaServer;

internal interface IThemeAware
{
    void ApplyPalette(AppPalette palette);
}

internal static class ThemePaint
{
    public static Color ResolveBackColor(Control control, AppPalette palette)
    {
        for (var parent = control.Parent; parent is not null; parent = parent.Parent)
        {
            if (parent.BackColor != Color.Transparent && parent.BackColor.A > 0)
            {
                return parent.BackColor;
            }
        }

        return palette.Window;
    }

    public static void PaintParentBackground(Control control, PaintEventArgs e, AppPalette palette)
    {
        using var brush = new SolidBrush(ResolveBackColor(control, palette));
        e.Graphics.FillRectangle(brush, control.ClientRectangle);
    }
}
