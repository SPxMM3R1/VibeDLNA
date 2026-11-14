using System.Globalization;

namespace FolderDlnaServer;

/// <summary>
/// Textos y cifras de la ventana en español: miles con punto, tamaños con coma, horas tipo
/// «1:12:40» y «hace 2 horas». Funciones puras para poder probarlas.
/// </summary>
internal static class ViewFormat
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <summary>Miles con punto: 1284 → «1.284».</summary>
    public static string Thousands(long value)
    {
        var digits = Math.Abs(value).ToString(Invariant);
        var builder = new System.Text.StringBuilder();
        for (var index = 0; index < digits.Length; index++)
        {
            if (index > 0 && (digits.Length - index) % 3 == 0)
            {
                builder.Append('.');
            }

            builder.Append(digits[index]);
        }

        return value < 0 ? "-" + builder : builder.ToString();
    }

    /// <summary>
    /// Tamaño legible con coma decimal: «1,9 TB», «9,4 GB», «31 GB». Base 1024, como el Explorador.
    /// </summary>
    public static string Bytes(long bytes)
    {
        string[] units = { "bytes", "KB", "MB", "GB", "TB" };
        var value = (double)Math.Max(0, bytes);
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        if (unit == 0)
        {
            return $"{Thousands((long)value)} bytes";
        }

        var text = value < 10
            ? value.ToString("0.0", Invariant)
            : value.ToString("0", Invariant);
        return $"{text.Replace('.', ',')} {units[unit]}";
    }

    /// <summary>Reloj de reproducción: «1:12:40» desde una hora, «4:05» antes.</summary>
    public static string Clock(TimeSpan time)
    {
        if (time < TimeSpan.Zero)
        {
            time = TimeSpan.Zero;
        }

        var totalSeconds = (long)Math.Floor(time.TotalSeconds);
        var hours = totalSeconds / 3600;
        var minutes = totalSeconds % 3600 / 60;
        var seconds = totalSeconds % 60;
        return hours > 0
            ? $"{hours}:{minutes:00}:{seconds:00}"
            : $"{minutes}:{seconds:00}";
    }

    /// <summary>
    /// Posición de «Viendo ahora»: «1:12:40 de 2:46:00». Sin duración, solo el porcentaje
    /// («44 %»), porque la posición se deduce del tamaño del archivo.
    /// </summary>
    public static string Progress(TimeSpan? position, TimeSpan? duration, double percent)
    {
        if (position is { } at && duration is { } total && total > TimeSpan.Zero)
        {
            return $"{Clock(at)} de {Clock(total)}";
        }

        return $"{Percent(percent)} %";
    }

    /// <summary>Línea secundaria de «Viendo ahora»: reproductor, y luego la posición.</summary>
    public static string NowPlayingMeta(string playerName, TimeSpan? position, TimeSpan? duration, double percent) =>
        $"{playerName} · {Progress(position, duration, percent)}";

    /// <summary>Porcentaje entero sin decimales: 43,7 → «44».</summary>
    public static string Percent(double percent) =>
        Math.Round(Math.Clamp(percent, 0, 100), MidpointRounding.AwayFromZero).ToString("0", Invariant);

    /// <summary>
    /// «hace 2 horas», «hace 1 minuto», «ahora». Lo que pasó hace más de 30 días se dice «hace más de un mes».
    /// </summary>
    public static string Ago(DateTimeOffset when, DateTimeOffset now)
    {
        var elapsed = now - when;
        if (elapsed < TimeSpan.FromMinutes(1))
        {
            return "ahora";
        }

        if (elapsed < TimeSpan.FromHours(1))
        {
            var minutes = (int)elapsed.TotalMinutes;
            return minutes == 1 ? "hace 1 minuto" : $"hace {minutes} minutos";
        }

        if (elapsed < TimeSpan.FromDays(1))
        {
            var hours = (int)elapsed.TotalHours;
            return hours == 1 ? "hace 1 hora" : $"hace {hours} horas";
        }

        if (elapsed < TimeSpan.FromDays(30))
        {
            var days = (int)elapsed.TotalDays;
            return days == 1 ? "hace 1 día" : $"hace {days} días";
        }

        return "hace más de un mes";
    }

    /// <summary>
    /// Resumen de una carpeta: «642 videos · 1,9 TB». Solo aparecen los tipos que tiene.
    /// </summary>
    public static string FolderSummary(int videos, int audio, int images, long bytes)
    {
        var parts = new List<string>();
        if (videos > 0)
        {
            parts.Add($"{Thousands(videos)} {(videos == 1 ? "video" : "videos")}");
        }

        if (audio > 0)
        {
            parts.Add($"{Thousands(audio)} {(audio == 1 ? "canción" : "canciones")}");
        }

        if (images > 0)
        {
            parts.Add($"{Thousands(images)} {(images == 1 ? "imagen" : "imágenes")}");
        }

        if (parts.Count == 0)
        {
            return "Sin archivos compatibles";
        }

        parts.Add(Bytes(bytes));
        return string.Join(" · ", parts);
    }

    /// <summary>
    /// Identificador corto de un reproductor: «4f1c…a92e». Null si no hay id de 8 caracteres.
    /// </summary>
    public static string? ShortId(string? playerId) =>
        playerId is { Length: 8 } id ? $"{id[..4]}…{id[4..]}" : null;

    /// <summary>
    /// Segunda línea de un reproductor visto: nombre del equipo (solo si es distinto del modelo),
    /// su identificador corto o su IP.
    /// </summary>
    public static string PlayerMeta(string? deviceName, string? model, string? playerId, string ip)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(deviceName)
            && !string.Equals(deviceName.Trim(), model?.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            parts.Add(deviceName.Trim());
        }

        if (ShortId(playerId) is { } shortId)
        {
            parts.Add($"uuid {shortId}");
        }
        else if (!string.IsNullOrWhiteSpace(ip))
        {
            parts.Add(ip);
        }

        return string.Join(" · ", parts);
    }
}
