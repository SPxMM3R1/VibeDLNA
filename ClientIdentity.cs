using System.Text.RegularExpressions;

namespace FolderDlnaServer;

internal enum ClientKind
{
    Desconocido,
    VibeDlnaPlayer,
    Vlc,
    Kodi
}

/// <summary>
/// Quién hace un pedido HTTP, a partir de su User-Agent (contrato §C). El Player de VibeDLNA
/// envía «VibeDLNA-Player/&lt;versión&gt; (&lt;modelo&gt;; &lt;nombre&gt;; id=&lt;8 hex&gt;)»; VLC y Kodi
/// se reconocen por su prefijo; cualquier otro cliente se nombra por su IP.
/// Clase pura: sin disco ni red, para poder probarla.
/// </summary>
internal sealed record ClientIdentity
{
    private static readonly Regex PlayerAgent = new(
        @"^VibeDLNA-Player/(?<version>[^\s()]+)\s*\((?<model>[^;()]*);(?<name>[^;()]*);\s*id=(?<id>[0-9a-fA-F]{8})\)$",
        RegexOptions.CultureInvariant);

    private static readonly Regex VlcAgent = new(
        @"^VLC/(?<version>\S+)",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex KodiAgent = new(
        @"^Kodi/(?<version>\S+)",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private ClientIdentity(
        ClientKind kind,
        string remoteAddress,
        string? playerId,
        string? model,
        string? deviceName,
        string? version)
    {
        Kind = kind;
        RemoteAddress = remoteAddress;
        PlayerId = playerId;
        Model = model;
        DeviceName = deviceName;
        Version = version;
    }

    public ClientKind Kind { get; }

    /// <summary>IP de quien pide, tal como la ve el servidor.</summary>
    public string RemoteAddress { get; }

    /// <summary>Identificador del Player (8 hex en minúsculas), o null si no es un Player de VibeDLNA.</summary>
    public string? PlayerId { get; }

    /// <summary>Modelo del equipo (Build.MODEL), por ejemplo «Hisense 65A6».</summary>
    public string? Model { get; }

    /// <summary>Nombre del dispositivo que dio el usuario, por ejemplo «Living».</summary>
    public string? DeviceName { get; }

    /// <summary>Versión del software del cliente cuando el User-Agent la trae.</summary>
    public string? Version { get; }

    /// <summary>
    /// Nombre corto para el registro de actividad: «Hisense 65A6», «VLC», «Kodi» o la IP.
    /// </summary>
    public string Name => Kind switch
    {
        ClientKind.VibeDlnaPlayer => FirstNonEmpty(Model, DeviceName) ?? "VibeDLNA Player",
        ClientKind.Vlc => "VLC",
        ClientKind.Kodi => "Kodi",
        _ => RemoteAddress
    };

    /// <summary>
    /// Nombre para la pantalla de Reproductores: «VibeDLNA Player · Hisense 65A6», «VLC 3.0.18»
    /// o «Reproductor en 192.168.1.52».
    /// </summary>
    public string DisplayName => Kind switch
    {
        ClientKind.VibeDlnaPlayer => FirstNonEmpty(Model, DeviceName) is { } label
            ? $"VibeDLNA Player · {label}"
            : "VibeDLNA Player",
        ClientKind.Vlc => Version is null ? "VLC" : $"VLC {Version}",
        ClientKind.Kodi => Version is null ? "Kodi" : $"Kodi {Version}",
        _ => $"Reproductor en {RemoteAddress}"
    };

    /// <summary>Identificador abreviado para mostrar: «4f1c…a92e». Null si no hay id de Player.</summary>
    public string? ShortPlayerId => PlayerId is { Length: 8 } id
        ? $"{id[..4]}…{id[4..]}"
        : null;

    /// <summary>
    /// Clave estable para agrupar: el id del Player, o el tipo de cliente más la IP.
    /// </summary>
    public string Key => PlayerId is null
        ? $"{Kind}|{RemoteAddress}"
        : $"player|{PlayerId}";

    /// <summary>Interpreta el User-Agent de un pedido HTTP. Un User-Agent vacío o desconocido da «Desconocido».</summary>
    public static ClientIdentity Parse(string? userAgent, string remoteAddress)
    {
        var agent = (userAgent ?? string.Empty).Trim();
        var address = remoteAddress ?? string.Empty;

        var player = PlayerAgent.Match(agent);
        if (player.Success)
        {
            return new ClientIdentity(
                ClientKind.VibeDlnaPlayer,
                address,
                player.Groups["id"].Value.ToLowerInvariant(),
                NullIfBlank(player.Groups["model"].Value),
                NullIfBlank(player.Groups["name"].Value),
                NullIfBlank(player.Groups["version"].Value));
        }

        var vlc = VlcAgent.Match(agent);
        if (vlc.Success)
        {
            return new ClientIdentity(ClientKind.Vlc, address, null, null, null, vlc.Groups["version"].Value);
        }

        var kodi = KodiAgent.Match(agent);
        if (kodi.Success)
        {
            return new ClientIdentity(ClientKind.Kodi, address, null, null, null, kodi.Groups["version"].Value);
        }

        return new ClientIdentity(ClientKind.Desconocido, address, null, null, null, null);
    }

    private static string? NullIfBlank(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }

    private static string? FirstNonEmpty(string? first, string? second) =>
        !string.IsNullOrWhiteSpace(first) ? first : second;
}
