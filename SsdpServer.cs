using System.Net;
using System.Net.Sockets;
using System.Text;

namespace FolderDlnaServer;

internal sealed class SsdpServer : IDisposable
{
    private static readonly IPAddress MulticastAddress = IPAddress.Parse("239.255.255.250");
    private const int MulticastPort = 1900;
    private readonly string _uuid;
    private readonly IPAddress _localAddress;
    private readonly Func<string> _locationFactory;
    private readonly string _serverHeader;
    private Socket? _socket;
    private CancellationTokenSource? _cancellation;
    private Task? _receiveTask;
    private Task? _notifyTask;

    public event EventHandler<string>? Message;

    public SsdpServer(string uuid, IPAddress localAddress, Func<string> locationFactory)
    {
        _uuid = uuid;
        _localAddress = localAddress;
        _locationFactory = locationFactory;
        _serverHeader = $"Windows/{Environment.OSVersion.Version.Major}.{Environment.OSVersion.Version.Minor} UPnP/1.0 VibeDLNA/1.0";
    }

    private IEnumerable<(string Target, string Usn)> NotificationTargets
    {
        get
        {
            yield return ("upnp:rootdevice", $"uuid:{_uuid}::upnp:rootdevice");
            yield return ($"uuid:{_uuid}", $"uuid:{_uuid}");
            yield return ("urn:schemas-upnp-org:device:MediaServer:1", $"uuid:{_uuid}::urn:schemas-upnp-org:device:MediaServer:1");
            yield return ("urn:schemas-upnp-org:service:ContentDirectory:1", $"uuid:{_uuid}::urn:schemas-upnp-org:service:ContentDirectory:1");
            yield return ("urn:schemas-upnp-org:service:ConnectionManager:1", $"uuid:{_uuid}::urn:schemas-upnp-org:service:ConnectionManager:1");
        }
    }

    public Task StartAsync()
    {
        _cancellation = new CancellationTokenSource();
        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);
        _socket.Bind(new IPEndPoint(IPAddress.Any, MulticastPort));
        _socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(MulticastAddress, _localAddress));
        _socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface, _localAddress.GetAddressBytes());
        _socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 4);

        _receiveTask = Task.Run(() => ReceiveLoopAsync(_cancellation.Token));
        _notifyTask = Task.Run(() => NotifyLoopAsync(_cancellation.Token));
        return SendNotifyAsync("ssdp:alive", _cancellation.Token);
    }

    public async Task StopAsync()
    {
        if (_cancellation is null)
        {
            return;
        }

        try
        {
            await SendNotifyAsync("ssdp:byebye", CancellationToken.None);
        }
        catch
        {
            // Best effort announcement.
        }

        _cancellation.Cancel();
        _socket?.Close();

        try
        {
            if (_receiveTask is not null)
            {
                await _receiveTask;
            }
        }
        catch
        {
            // Socket closes as part of shutdown.
        }

        try
        {
            if (_notifyTask is not null)
            {
                await _notifyTask;
            }
        }
        catch
        {
            // Timer stops as part of shutdown.
        }

        _socket?.Dispose();
        _socket = null;
        _cancellation.Dispose();
        _cancellation = null;
    }

    public void Dispose()
    {
        _cancellation?.Cancel();
        _socket?.Dispose();
        _cancellation?.Dispose();
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        while (!cancellationToken.IsCancellationRequested && _socket is not null)
        {
            EndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
            SocketReceiveFromResult result;
            try
            {
                result = await _socket.ReceiveFromAsync(buffer, SocketFlags.None, remoteEndPoint, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch
            {
                continue;
            }

            var request = Encoding.UTF8.GetString(buffer, 0, result.ReceivedBytes);
            if (!request.StartsWith("M-SEARCH", StringComparison.OrdinalIgnoreCase)
                || !request.Contains("ssdp:discover", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var searchTarget = ExtractHeader(request, "ST");
            if (string.IsNullOrWhiteSpace(searchTarget))
            {
                continue;
            }

            await RespondToSearchAsync(searchTarget, result.RemoteEndPoint, cancellationToken);
        }
    }

    private async Task RespondToSearchAsync(string searchTarget, EndPoint remoteEndPoint, CancellationToken cancellationToken)
    {
        if (_socket is null)
        {
            return;
        }

        var targets = searchTarget.Equals("ssdp:all", StringComparison.OrdinalIgnoreCase)
            ? NotificationTargets
            : NotificationTargets.Where(target => target.Target.Equals(searchTarget, StringComparison.OrdinalIgnoreCase));

        foreach (var (target, usn) in targets)
        {
            var response = string.Join("\r\n",
                "HTTP/1.1 200 OK",
                "CACHE-CONTROL: max-age=1800",
                $"DATE: {DateTime.UtcNow:R}",
                "EXT:",
                $"LOCATION: {_locationFactory()}",
                $"SERVER: {_serverHeader}",
                $"ST: {target}",
                $"USN: {usn}",
                "BOOTID.UPNP.ORG: 1",
                "CONFIGID.UPNP.ORG: 1",
                "",
                "");
            var bytes = Encoding.UTF8.GetBytes(response);
            await _socket.SendToAsync(bytes, SocketFlags.None, remoteEndPoint, cancellationToken);
        }
    }

    private async Task NotifyLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(60));
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            await SendNotifyAsync("ssdp:alive", cancellationToken);
        }
    }

    private async Task SendNotifyAsync(string notificationSubtype, CancellationToken cancellationToken)
    {
        if (_socket is null)
        {
            return;
        }

        foreach (var (target, usn) in NotificationTargets)
        {
            var message = string.Join("\r\n",
                "NOTIFY * HTTP/1.1",
                "HOST: 239.255.255.250:1900",
                "CACHE-CONTROL: max-age=1800",
                $"LOCATION: {_locationFactory()}",
                $"NT: {target}",
                $"NTS: {notificationSubtype}",
                $"SERVER: {_serverHeader}",
                $"USN: {usn}",
                "BOOTID.UPNP.ORG: 1",
                "CONFIGID.UPNP.ORG: 1",
                "",
                "");
            var bytes = Encoding.UTF8.GetBytes(message);
            await _socket.SendToAsync(bytes, SocketFlags.None, new IPEndPoint(MulticastAddress, MulticastPort), cancellationToken);
        }

        if (notificationSubtype == "ssdp:alive")
        {
            Message?.Invoke(this, "Anuncio DLNA enviado en la red local.");
        }
    }

    private static string ExtractHeader(string request, string headerName)
    {
        foreach (var line in request.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = line.IndexOf(':');
            if (separator <= 0)
            {
                continue;
            }

            if (line[..separator].Trim().Equals(headerName, StringComparison.OrdinalIgnoreCase))
            {
                return line[(separator + 1)..].Trim();
            }
        }

        return string.Empty;
    }
}
