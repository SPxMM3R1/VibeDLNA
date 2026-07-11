using System.Net;
using System.Text;

namespace FolderDlnaServer;

internal sealed class HttpRequestData
{
    private const int MaximumHeaderBytes = 64 * 1024;
    private const int MaximumBodyBytes = 1024 * 1024;
    public required string Method { get; init; }

    public required string Target { get; init; }

    public required string Version { get; init; }

    public required Dictionary<string, string> Headers { get; init; }

    public byte[] Body { get; init; } = Array.Empty<byte>();

    public string BodyText => Encoding.UTF8.GetString(Body);

    public static async Task<HttpRequestData?> ReadAsync(Stream stream, CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        var raw = new MemoryStream();
        var headerEnd = -1;

        while (headerEnd < 0)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                return null;
            }

            raw.Write(buffer, 0, read);
            headerEnd = FindHeaderEnd(raw.GetBuffer(), (int)raw.Length);
            if (raw.Length > MaximumHeaderBytes)
            {
                throw new InvalidDataException("HTTP headers too large.");
            }
        }

        var rawBytes = raw.ToArray();
        var headerText = Encoding.ASCII.GetString(rawBytes, 0, headerEnd);
        var lines = headerText.Split(new[] { "\r\n" }, StringSplitOptions.None);
        if (lines.Length == 0)
        {
            return null;
        }

        var requestLineParts = lines[0].Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
        if (requestLineParts.Length < 3)
        {
            return null;
        }

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 1; index < lines.Length; index++)
        {
            var line = lines[index];
            var separator = line.IndexOf(':');
            if (separator <= 0)
            {
                continue;
            }

            headers[line[..separator].Trim()] = line[(separator + 1)..].Trim();
        }

        var contentLength = 0;
        if (headers.TryGetValue("Content-Length", out var contentLengthValue))
        {
            if (!int.TryParse(contentLengthValue, out contentLength) || contentLength < 0 || contentLength > MaximumBodyBytes)
            {
                throw new InvalidDataException("HTTP body too large or invalid.");
            }
        }

        var bodyStart = headerEnd + 4;
        var body = new byte[contentLength];
        var alreadyRead = Math.Min(contentLength, rawBytes.Length - bodyStart);
        if (alreadyRead > 0)
        {
            Buffer.BlockCopy(rawBytes, bodyStart, body, 0, alreadyRead);
        }

        while (alreadyRead < contentLength)
        {
            var read = await stream.ReadAsync(body.AsMemory(alreadyRead, contentLength - alreadyRead), cancellationToken);
            if (read == 0)
            {
                break;
            }

            alreadyRead += read;
        }

        return new HttpRequestData
        {
            Method = WebUtility.UrlDecode(requestLineParts[0]),
            Target = requestLineParts[1],
            Version = requestLineParts[2],
            Headers = headers,
            Body = body
        };
    }

    private static int FindHeaderEnd(byte[] buffer, int length)
    {
        for (var index = 3; index < length; index++)
        {
            if (buffer[index - 3] == '\r'
                && buffer[index - 2] == '\n'
                && buffer[index - 1] == '\r'
                && buffer[index] == '\n')
            {
                return index - 3;
            }
        }

        return -1;
    }
}
