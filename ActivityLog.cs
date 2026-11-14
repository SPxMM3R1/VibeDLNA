namespace FolderDlnaServer;

/// <summary>Tipo de línea del registro, con su color en la ventana.</summary>
internal enum ActivityKind
{
    /// <summary>Un reproductor empezó a ver un video.</summary>
    Play,

    /// <summary>Un reproductor abrió una carpeta.</summary>
    Nav,

    /// <summary>Estado del servidor, miniaturas, carpetas, actualizaciones.</summary>
    Sys,

    /// <summary>Algo falló o pidió algo imposible.</summary>
    Err
}

/// <summary>Filtro de la pantalla Actividad (píldoras).</summary>
internal enum ActivityFilter
{
    All,
    Play,
    Nav,
    Sys,
    Err
}

/// <summary>Una línea del registro de la sesión.</summary>
internal sealed record ActivityEntry(DateTime Time, ActivityKind Kind, string Message);

/// <summary>
/// Clasifica las líneas del registro por su texto y aplica los filtros de la pantalla Actividad.
/// Funciones puras para poder probarlas.
/// </summary>
internal static class ActivityLog
{
    /// <summary>Máximo de líneas que guarda la ventana en memoria.</summary>
    public const int MaximumEntries = 500;

    /// <summary>Decide el tipo de una línea a partir de su frase (contrato §D y registro del servidor).</summary>
    public static ActivityKind Classify(string message)
    {
        var text = message ?? string.Empty;
        if (Contains(text, " reproduce "))
        {
            return ActivityKind.Play;
        }

        if (Contains(text, " abrió "))
        {
            return ActivityKind.Nav;
        }

        if (text.StartsWith("Error", StringComparison.OrdinalIgnoreCase)
            || Contains(text, "fuera del archivo")
            || Contains(text, "no se pudo")
            || Contains(text, "no esta disponible")
            || Contains(text, "no está disponible"))
        {
            return ActivityKind.Err;
        }

        return ActivityKind.Sys;
    }

    /// <summary>Filtra por tipo y, si hay texto, por coincidencia en el mensaje (sin mayúsculas).</summary>
    public static IReadOnlyList<ActivityEntry> Apply(
        IEnumerable<ActivityEntry> entries,
        ActivityFilter filter,
        string? query)
    {
        var needle = (query ?? string.Empty).Trim();
        return entries
            .Where(entry => Matches(entry.Kind, filter))
            .Where(entry => needle.Length == 0 || Contains(entry.Message, needle))
            .ToList();
    }

    /// <summary>Etiqueta de cada tipo en la columna «tipo» del registro.</summary>
    public static string KindLabel(ActivityKind kind) => kind switch
    {
        ActivityKind.Play => "Reproduce",
        ActivityKind.Nav => "Navega",
        ActivityKind.Sys => "Servidor",
        _ => "Error"
    };

    private static bool Matches(ActivityKind kind, ActivityFilter filter) => filter switch
    {
        ActivityFilter.Play => kind == ActivityKind.Play,
        ActivityFilter.Nav => kind == ActivityKind.Nav,
        ActivityFilter.Sys => kind == ActivityKind.Sys,
        ActivityFilter.Err => kind == ActivityKind.Err,
        _ => true
    };

    private static bool Contains(string text, string fragment) =>
        text.Contains(fragment, StringComparison.OrdinalIgnoreCase);
}
