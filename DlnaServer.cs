using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Xml.Linq;

namespace FolderDlnaServer;

internal sealed class DlnaServer : IDisposable
{
    private readonly string _mediaFolder;
    private readonly string _friendlyName;
    private readonly string _uuid;
    private readonly int _requestedPort;
    private DlnaContentLibrary? _library;
    private TcpListener? _listener;
    private CancellationTokenSource? _cancellation;
    private Task? _acceptLoopTask;
    private SsdpServer? _ssdpServer;
    private IPAddress _localAddress = IPAddress.Loopback;

    public event EventHandler<string>? Message;

    public DlnaServer(string mediaFolder, string friendlyName, string uuid, int requestedPort)
    {
        _mediaFolder = mediaFolder;
        _friendlyName = friendlyName;
        _uuid = uuid;
        _requestedPort = requestedPort;
    }

    public bool IsRunning => _listener is not null;

    public int Port { get; private set; }

    public string BaseUrl => $"http://{_localAddress}:{Port}";

    public string DescriptionUrl => $"{BaseUrl}/description.xml";

    public async Task StartAsync()
    {
        if (IsRunning)
        {
            return;
        }

        _localAddress = NetworkHelper.GetLocalIPv4Address();
        _library = new DlnaContentLibrary(_mediaFolder);
        _cancellation = new CancellationTokenSource();
        _listener = new TcpListener(IPAddress.Any, _requestedPort);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;

        _acceptLoopTask = Task.Run(() => AcceptLoopAsync(_cancellation.Token));

        _ssdpServer = new SsdpServer(_uuid, () => DescriptionUrl);
        _ssdpServer.Message += (_, message) => Message?.Invoke(this, message);
        try
        {
            await _ssdpServer.StartAsync();
        }
        catch (Exception ex)
        {
            Message?.Invoke(this, $"Servidor activo, pero el anuncio DLNA no pudo abrir el puerto SSDP: {ex.Message}");
        }
    }

    public async Task StopAsync()
    {
        if (!IsRunning)
        {
            return;
        }

        if (_ssdpServer is not null)
        {
            await _ssdpServer.StopAsync();
            _ssdpServer.Dispose();
            _ssdpServer = null;
        }

        _cancellation?.Cancel();
        _listener?.Stop();

        try
        {
            if (_acceptLoopTask is not null)
            {
                await _acceptLoopTask;
            }
        }
        catch
        {
            // The listener is stopped intentionally.
        }

        _listener = null;
        _acceptLoopTask = null;
        _cancellation?.Dispose();
        _cancellation = null;
        _library = null;
        Port = 0;
    }

    public void Dispose()
    {
        _cancellation?.Cancel();
        _listener?.Stop();
        _ssdpServer?.Dispose();
        _cancellation?.Dispose();
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _listener is not null)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }

            _ = Task.Run(() => HandleClientAsync(client, cancellationToken), CancellationToken.None);
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
            using var clientScope = client;
            try
            {
                client.ReceiveTimeout = 15000;
                client.SendTimeout = 30000;
                await using var stream = client.GetStream();
                var request = await HttpRequestData.ReadAsync(stream, cancellationToken);
                if (request is null)
                {
                    return;
                }

            await RouteRequestAsync(request, stream, cancellationToken);
        }
        catch
        {
            // DLNA clients often disconnect while probing. Ignore per-request noise.
        }
    }

    private async Task RouteRequestAsync(HttpRequestData request, Stream stream, CancellationToken cancellationToken)
    {
        var method = request.Method.ToUpperInvariant();
        var uri = CreateUri(request.Target);
        var path = uri.AbsolutePath;

        if (method == "OPTIONS")
        {
            await WriteResponseAsync(stream, 200, "OK", "text/plain", Array.Empty<byte>(), request.Method, cancellationToken,
                new Dictionary<string, string> { ["Allow"] = "GET, HEAD, POST, OPTIONS, SUBSCRIBE, UNSUBSCRIBE" });
            return;
        }

        if (method is "SUBSCRIBE" or "UNSUBSCRIBE")
        {
            await WriteResponseAsync(stream, 200, "OK", "text/plain", Array.Empty<byte>(), request.Method, cancellationToken,
                new Dictionary<string, string>
                {
                    ["SID"] = $"uuid:{Guid.NewGuid():D}",
                    ["TIMEOUT"] = "Second-1800"
                });
            return;
        }

        if (method is "GET" or "HEAD")
        {
            if (path.Equals("/", StringComparison.OrdinalIgnoreCase))
            {
                await WriteResponseAsync(stream, 200, "OK", "text/html; charset=utf-8",
                    Encoding.UTF8.GetBytes(BuildLandingPage()), request.Method, cancellationToken);
                return;
            }

            if (path.Equals("/description.xml", StringComparison.OrdinalIgnoreCase)
                || path.Equals("/rootDesc.xml", StringComparison.OrdinalIgnoreCase))
            {
                await WriteResponseAsync(stream, 200, "OK", "text/xml; charset=utf-8",
                    Encoding.UTF8.GetBytes(DlnaXml.DeviceDescription(_friendlyName, _uuid)), request.Method, cancellationToken);
                return;
            }

            if (path.Equals("/ContentDirectory/scpd.xml", StringComparison.OrdinalIgnoreCase))
            {
                await WriteResponseAsync(stream, 200, "OK", "text/xml; charset=utf-8",
                    Encoding.UTF8.GetBytes(DlnaXml.ContentDirectoryScpd()), request.Method, cancellationToken);
                return;
            }

            if (path.Equals("/ConnectionManager/scpd.xml", StringComparison.OrdinalIgnoreCase))
            {
                await WriteResponseAsync(stream, 200, "OK", "text/xml; charset=utf-8",
                    Encoding.UTF8.GetBytes(DlnaXml.ConnectionManagerScpd()), request.Method, cancellationToken);
                return;
            }

            if (path.StartsWith("/media/", StringComparison.OrdinalIgnoreCase))
            {
                await WriteMediaAsync(request, stream, path, cancellationToken);
                return;
            }
        }

        if (method == "POST" && path.Equals("/ContentDirectory/control", StringComparison.OrdinalIgnoreCase))
        {
            await HandleContentDirectoryControlAsync(request, stream, cancellationToken);
            return;
        }

        if (method == "POST" && path.Equals("/ConnectionManager/control", StringComparison.OrdinalIgnoreCase))
        {
            await HandleConnectionManagerControlAsync(request, stream, cancellationToken);
            return;
        }

        await WriteResponseAsync(stream, 404, "Not Found", "text/plain; charset=utf-8",
            Encoding.UTF8.GetBytes("Not found"), request.Method, cancellationToken);
    }

    private async Task HandleContentDirectoryControlAsync(HttpRequestData request, Stream stream, CancellationToken cancellationToken)
    {
        try
        {
            var action = GetSoapAction(request);
            var body = XDocument.Parse(request.BodyText);

            string response;
            if (action == "Browse")
            {
                var objectId = GetXmlValue(body, "ObjectID", "0");
                var browseFlag = GetXmlValue(body, "BrowseFlag", "BrowseDirectChildren");
                var startingIndex = GetXmlUInt(body, "StartingIndex");
                var requestedCount = GetXmlUInt(body, "RequestedCount");
                var entries = browseFlag.Equals("BrowseMetadata", StringComparison.OrdinalIgnoreCase)
                    ? new[] { Library.GetMetadata(objectId) }
                    : Library.GetChildren(objectId);
                var totalMatches = entries.Count;
                var pagedEntries = requestedCount == 0
                    ? entries.Skip((int)startingIndex).ToArray()
                    : entries.Skip((int)startingIndex).Take((int)requestedCount).ToArray();

                var didl = DlnaXml.BuildDidl(pagedEntries, BaseUrl);
                response = DlnaXml.SoapResponse(
                    DlnaXml.ContentDirectoryServiceType,
                    "Browse",
                    new XElement("Result", didl),
                    new XElement("NumberReturned", pagedEntries.Length),
                    new XElement("TotalMatches", totalMatches),
                    new XElement("UpdateID", 1));
            }
            else if (action == "GetSystemUpdateID")
            {
                response = DlnaXml.SoapResponse(DlnaXml.ContentDirectoryServiceType, "GetSystemUpdateID", new XElement("Id", 1));
            }
            else if (action == "GetSearchCapabilities")
            {
                response = DlnaXml.SoapResponse(DlnaXml.ContentDirectoryServiceType, "GetSearchCapabilities", new XElement("SearchCaps", string.Empty));
            }
            else if (action == "GetSortCapabilities")
            {
                response = DlnaXml.SoapResponse(DlnaXml.ContentDirectoryServiceType, "GetSortCapabilities", new XElement("SortCaps", "dc:title"));
            }
            else
            {
                response = DlnaXml.SoapFault(401, "Invalid Action");
            }

            await WriteResponseAsync(stream, 200, "OK", "text/xml; charset=utf-8", Encoding.UTF8.GetBytes(response), request.Method, cancellationToken,
                new Dictionary<string, string> { ["EXT"] = string.Empty });
        }
        catch (Exception ex)
        {
            var fault = DlnaXml.SoapFault(501, ex.Message);
            await WriteResponseAsync(stream, 500, "Internal Server Error", "text/xml; charset=utf-8", Encoding.UTF8.GetBytes(fault), request.Method, cancellationToken);
        }
    }

    private async Task HandleConnectionManagerControlAsync(HttpRequestData request, Stream stream, CancellationToken cancellationToken)
    {
        var action = GetSoapAction(request);
        string response;
        if (action == "GetProtocolInfo")
        {
            response = DlnaXml.SoapResponse(
                DlnaXml.ConnectionManagerServiceType,
                "GetProtocolInfo",
                new XElement("Source", MediaTypes.GetSourceProtocolInfo()),
                new XElement("Sink", string.Empty));
        }
        else if (action == "GetCurrentConnectionIDs")
        {
            response = DlnaXml.SoapResponse(DlnaXml.ConnectionManagerServiceType, "GetCurrentConnectionIDs", new XElement("ConnectionIDs", "0"));
        }
        else if (action == "GetCurrentConnectionInfo")
        {
            response = DlnaXml.SoapResponse(
                DlnaXml.ConnectionManagerServiceType,
                "GetCurrentConnectionInfo",
                new XElement("RcsID", -1),
                new XElement("AVTransportID", -1),
                new XElement("ProtocolInfo", string.Empty),
                new XElement("PeerConnectionManager", string.Empty),
                new XElement("PeerConnectionID", -1),
                new XElement("Direction", "Output"),
                new XElement("Status", "OK"));
        }
        else
        {
            response = DlnaXml.SoapFault(401, "Invalid Action");
        }

        await WriteResponseAsync(stream, 200, "OK", "text/xml; charset=utf-8", Encoding.UTF8.GetBytes(response), request.Method, cancellationToken,
            new Dictionary<string, string> { ["EXT"] = string.Empty });
    }

    private async Task WriteMediaAsync(HttpRequestData request, Stream stream, string path, CancellationToken cancellationToken)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2)
        {
            await WriteResponseAsync(stream, 404, "Not Found", "text/plain", Encoding.UTF8.GetBytes("Not found"), request.Method, cancellationToken);
            return;
        }

        var objectId = Uri.UnescapeDataString(segments[1]);
        if (!Library.TryGetFile(objectId, out var filePath, out var mediaType))
        {
            await WriteResponseAsync(stream, 404, "Not Found", "text/plain", Encoding.UTF8.GetBytes("Not found"), request.Method, cancellationToken);
            return;
        }

        var fileInfo = new FileInfo(filePath);
        var start = 0L;
        var end = fileInfo.Length - 1;
        var isPartial = false;

        if (request.Headers.TryGetValue("Range", out var rangeHeader) && TryParseRange(rangeHeader, fileInfo.Length, out var rangeStart, out var rangeEnd))
        {
            start = rangeStart;
            end = rangeEnd;
            isPartial = true;
        }

        if (start < 0 || end < start || end >= fileInfo.Length)
        {
            await WriteResponseAsync(stream, 416, "Range Not Satisfiable", "text/plain", Array.Empty<byte>(), request.Method, cancellationToken,
                new Dictionary<string, string> { ["Content-Range"] = $"bytes */{fileInfo.Length}" });
            return;
        }

        var contentLength = end - start + 1;
        var headers = new Dictionary<string, string>
        {
            ["Accept-Ranges"] = "bytes",
            ["Last-Modified"] = fileInfo.LastWriteTimeUtc.ToString("R"),
            ["transferMode.dlna.org"] = "Streaming",
            ["contentFeatures.dlna.org"] = "DLNA.ORG_OP=01;DLNA.ORG_FLAGS=01700000000000000000000000000000"
        };

        if (isPartial)
        {
            headers["Content-Range"] = $"bytes {start}-{end}/{fileInfo.Length}";
        }

        await WriteHeaderAsync(
            stream,
            isPartial ? 206 : 200,
            isPartial ? "Partial Content" : "OK",
            mediaType.MimeType,
            contentLength,
            headers,
            cancellationToken);

        if (request.Method.Equals("HEAD", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1024 * 64, useAsync: true);
        fileStream.Seek(start, SeekOrigin.Begin);
        var buffer = new byte[1024 * 64];
        var remaining = contentLength;
        while (remaining > 0)
        {
            var read = await fileStream.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, remaining)), cancellationToken);
            if (read == 0)
            {
                break;
            }

            await stream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            remaining -= read;
        }
    }

    private async Task WriteResponseAsync(
        Stream stream,
        int statusCode,
        string reasonPhrase,
        string contentType,
        byte[] body,
        string requestMethod,
        CancellationToken cancellationToken,
        Dictionary<string, string>? extraHeaders = null)
    {
        await WriteHeaderAsync(stream, statusCode, reasonPhrase, contentType, body.Length, extraHeaders, cancellationToken);
        if (!requestMethod.Equals("HEAD", StringComparison.OrdinalIgnoreCase) && body.Length > 0)
        {
            await stream.WriteAsync(body, cancellationToken);
        }
    }

    private async Task WriteHeaderAsync(
        Stream stream,
        int statusCode,
        string reasonPhrase,
        string contentType,
        long contentLength,
        Dictionary<string, string>? extraHeaders,
        CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        builder.Append("HTTP/1.1 ").Append(statusCode).Append(' ').Append(reasonPhrase).Append("\r\n");
        builder.Append("Date: ").Append(DateTime.UtcNow.ToString("R")).Append("\r\n");
        builder.Append("Server: Windows UPnP/1.0 FolderDlnaServer/1.0\r\n");
        builder.Append("Connection: close\r\n");
        builder.Append("Content-Type: ").Append(contentType).Append("\r\n");
        builder.Append("Content-Length: ").Append(contentLength).Append("\r\n");
        if (extraHeaders is not null)
        {
            foreach (var (key, value) in extraHeaders)
            {
                builder.Append(key).Append(':');
                if (!string.IsNullOrEmpty(value))
                {
                    builder.Append(' ').Append(value);
                }

                builder.Append("\r\n");
            }
        }

        builder.Append("\r\n");
        var bytes = Encoding.ASCII.GetBytes(builder.ToString());
        await stream.WriteAsync(bytes, cancellationToken);
    }

    private static bool TryParseRange(string rangeHeader, long fileLength, out long start, out long end)
    {
        start = 0;
        end = fileLength - 1;

        if (!rangeHeader.StartsWith("bytes=", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var range = rangeHeader["bytes=".Length..].Split(',', 2)[0].Trim();
        var parts = range.Split('-', 2);
        if (parts.Length != 2)
        {
            return false;
        }

        if (parts[0].Length == 0)
        {
            if (!long.TryParse(parts[1], out var suffixLength) || suffixLength <= 0)
            {
                return false;
            }

            start = Math.Max(0, fileLength - suffixLength);
            end = fileLength - 1;
            return true;
        }

        if (!long.TryParse(parts[0], out start))
        {
            return false;
        }

        if (parts[1].Length > 0 && long.TryParse(parts[1], out var parsedEnd))
        {
            end = parsedEnd;
        }

        end = Math.Min(end, fileLength - 1);
        return start <= end;
    }

    private string BuildLandingPage()
    {
        var escapedName = WebUtility.HtmlEncode(_friendlyName);
        var escapedFolder = WebUtility.HtmlEncode(_mediaFolder);
        return $$"""
            <!doctype html>
            <html lang="es">
            <head>
              <meta charset="utf-8">
              <title>{{escapedName}}</title>
              <style>
                body { font-family: Segoe UI, Arial, sans-serif; margin: 32px; color: #202124; }
                code { background: #f2f2f2; padding: 2px 5px; border-radius: 4px; }
              </style>
            </head>
            <body>
              <h1>{{escapedName}}</h1>
              <p>Servidor DLNA activo.</p>
              <p>Carpeta compartida: <code>{{escapedFolder}}</code></p>
              <p>Descripcion UPnP: <a href="/description.xml">description.xml</a></p>
            </body>
            </html>
            """;
    }

    private static Uri CreateUri(string target)
    {
        if (Uri.TryCreate(target, UriKind.Absolute, out var absoluteUri))
        {
            return absoluteUri;
        }

        return new Uri("http://localhost" + target, UriKind.Absolute);
    }

    private static string GetSoapAction(HttpRequestData request)
    {
        if (request.Headers.TryGetValue("SOAPACTION", out var soapAction))
        {
            var hashIndex = soapAction.IndexOf('#');
            if (hashIndex >= 0)
            {
                return soapAction[(hashIndex + 1)..].Trim('"');
            }
        }

        try
        {
            var document = XDocument.Parse(request.BodyText);
            return document.Descendants().FirstOrDefault(element => element.Parent?.Name.LocalName == "Body")?.Name.LocalName ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string GetXmlValue(XDocument document, string localName, string fallback) =>
        document.Descendants().FirstOrDefault(element => element.Name.LocalName == localName)?.Value ?? fallback;

    private static uint GetXmlUInt(XDocument document, string localName) =>
        uint.TryParse(GetXmlValue(document, localName, "0"), out var value) ? value : 0;

    private DlnaContentLibrary Library => _library ?? throw new InvalidOperationException("Servidor no iniciado.");
}
