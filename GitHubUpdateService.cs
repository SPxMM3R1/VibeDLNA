using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows.Forms;

namespace FolderDlnaServer;

internal sealed class GitHubUpdateService
{
    public const string RepositoryOwner = "SPxMM3R1";
    public const string RepositoryName = "VibeDLNA";

    private static readonly Uri LatestReleaseUri = new(
        $"https://api.github.com/repos/{RepositoryOwner}/{RepositoryName}/releases/latest");
    private static readonly HttpClient HttpClient = CreateHttpClient();

    public Version CurrentVersion { get; } = GetCurrentVersion();

    public async Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await HttpClient.GetAsync(LatestReleaseUri, cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return new UpdateCheckResult(
                    UpdateCheckState.NoRelease,
                    CurrentVersion,
                    null,
                    "No hay una release publicada todavía.");
            }

            if (!response.IsSuccessStatusCode)
            {
                return new UpdateCheckResult(
                    UpdateCheckState.Error,
                    CurrentVersion,
                    null,
                    "No se pudo consultar GitHub en este momento.");
            }

            await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken);
            var releaseElement = document.RootElement;
            var tagName = GetString(releaseElement, "tag_name");
            if (!TryParseVersion(tagName, out var releaseVersion))
            {
                return new UpdateCheckResult(
                    UpdateCheckState.Error,
                    CurrentVersion,
                    null,
                    "La release más reciente de GitHub no tiene una versión válida.");
            }

            var release = new GitHubReleaseInfo(
                tagName,
                releaseVersion,
                GetString(releaseElement, "name") is { Length: > 0 } name ? name : tagName,
                GetString(releaseElement, "html_url"),
                SelectAsset(releaseElement));

            if (release.Version.CompareTo(CurrentVersion) <= 0)
            {
                return new UpdateCheckResult(
                    UpdateCheckState.UpToDate,
                    CurrentVersion,
                    release,
                    $"VibeDLNA está actualizada (v{CurrentVersion}).");
            }

            if (release.Asset is null)
            {
                return new UpdateCheckResult(
                    UpdateCheckState.Unavailable,
                    CurrentVersion,
                    release,
                    $"Hay v{release.Version}, pero no contiene un paquete compatible.");
            }

            return new UpdateCheckResult(
                UpdateCheckState.Available,
                CurrentVersion,
                release,
                $"Hay una actualización disponible: v{release.Version}.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return new UpdateCheckResult(
                UpdateCheckState.Error,
                CurrentVersion,
                null,
                "No se pudo consultar GitHub en este momento.");
        }
    }

    public async Task<UpdateInstallResult> DownloadAndStartAsync(
        GitHubReleaseInfo release,
        CancellationToken cancellationToken)
    {
        if (release.Asset is null || !IsAllowedGitHubUri(release.Asset.DownloadUri))
        {
            return new UpdateInstallResult(false, "La release no tiene un paquete de actualización válido.");
        }

        var workDirectory = Path.Combine(
            Path.GetTempPath(),
            "VibeDLNA-Update",
            Guid.NewGuid().ToString("N"));
        var updateStarted = false;

        try
        {
            Directory.CreateDirectory(workDirectory);
            var assetName = Path.GetFileName(release.Asset.Name);
            if (string.IsNullOrWhiteSpace(assetName))
            {
                return new UpdateInstallResult(false, "El paquete de actualización no tiene un nombre válido.");
            }

            var downloadPath = Path.Combine(workDirectory, assetName);
            using (var response = await HttpClient.GetAsync(
                       release.Asset.DownloadUri,
                       HttpCompletionOption.ResponseHeadersRead,
                       cancellationToken))
            {
                response.EnsureSuccessStatusCode();
                await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using var fileStream = new FileStream(
                    downloadPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 1024 * 64,
                    useAsync: true);
                await responseStream.CopyToAsync(fileStream, cancellationToken);
            }

            var downloadedInfo = new FileInfo(downloadPath);
            if (release.Asset.Size > 0 && downloadedInfo.Length != release.Asset.Size)
            {
                return new UpdateInstallResult(false, "El paquete descargado no coincide con el tamaño publicado.");
            }

            if (!string.IsNullOrWhiteSpace(release.Asset.Sha256))
            {
                var checksum = await Task.Run(
                    () => ComputeChecksum(downloadPath),
                    cancellationToken);
                if (!checksum.Equals(release.Asset.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    return new UpdateInstallResult(false, "La verificación SHA-256 del paquete falló.");
                }
            }

            if (release.Asset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                var extractedDirectory = Path.Combine(workDirectory, "extracted");
                ExtractZipSafely(downloadPath, extractedDirectory);
                var executable = Directory
                    .EnumerateFiles(extractedDirectory, "VibeDLNA.exe", SearchOption.AllDirectories)
                    .OrderBy(path => path.Length)
                    .FirstOrDefault();
                if (executable is null)
                {
                    return new UpdateInstallResult(false, "El ZIP no contiene VibeDLNA.exe.");
                }

                updateStarted = LaunchPortableUpdater(executable, workDirectory);
            }
            else if (release.Asset.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                if (release.Asset.Name.StartsWith("VibeDLNA-Setup-", StringComparison.OrdinalIgnoreCase))
                {
                    updateStarted = LaunchInstaller(downloadPath, workDirectory);
                }
                else
                {
                    updateStarted = LaunchPortableUpdater(downloadPath, workDirectory);
                }
            }
            else
            {
                return new UpdateInstallResult(false, "El formato del paquete no es compatible.");
            }

            return updateStarted
                ? new UpdateInstallResult(true, "Actualización iniciada. VibeDLNA se reiniciará en unos segundos.")
                : new UpdateInstallResult(false, "No se pudo iniciar el actualizador.");
        }
        catch (OperationCanceledException)
        {
            return new UpdateInstallResult(false, "Actualización cancelada.");
        }
        catch
        {
            return new UpdateInstallResult(false, "No se pudo descargar o preparar la actualización.");
        }
        finally
        {
            if (!updateStarted)
            {
                TryDeleteDirectory(workDirectory);
            }
        }
    }

    internal static bool TryParseVersion(string? tagName, out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(tagName))
        {
            return false;
        }

        var value = tagName.Trim();
        if (value.StartsWith("v", StringComparison.OrdinalIgnoreCase))
        {
            value = value[1..];
        }

        var separator = value.IndexOf('-');
        if (separator >= 0)
        {
            value = value[..separator];
        }

        if (!Version.TryParse(value, out var parsed))
        {
            return false;
        }

        version = new Version(
            parsed.Major,
            parsed.Minor,
            Math.Max(parsed.Build, 0));
        return true;
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(45)
        };
        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("VibeDLNA", GetCurrentVersion().ToString()));
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        return client;
    }

    private static Version GetCurrentVersion()
    {
        var assemblyVersion = Assembly.GetEntryAssembly()?.GetName().Version;
        return assemblyVersion is null
            ? new Version(1, 0, 0)
            : new Version(
                assemblyVersion.Major,
                assemblyVersion.Minor,
                Math.Max(assemblyVersion.Build, 0));
    }

    private static GitHubReleaseAsset? SelectAsset(JsonElement releaseElement)
    {
        if (!releaseElement.TryGetProperty("assets", out var assets)
            || assets.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var candidates = assets.EnumerateArray()
            .Select(TryReadAsset)
            .Where(asset => asset is not null)
            .Cast<GitHubReleaseAsset>()
            .ToArray();

        return candidates.FirstOrDefault(asset =>
                   asset.Name.Equals("VibeDLNA-windows.zip", StringComparison.OrdinalIgnoreCase))
            ?? candidates.FirstOrDefault(asset =>
                asset.Name.StartsWith("VibeDLNA-Setup-", StringComparison.OrdinalIgnoreCase)
                && asset.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            ?? candidates.FirstOrDefault(asset =>
                asset.Name.Equals("VibeDLNA.exe", StringComparison.OrdinalIgnoreCase));
    }

    private static GitHubReleaseAsset? TryReadAsset(JsonElement assetElement)
    {
        var name = GetString(assetElement, "name");
        var downloadUrl = GetString(assetElement, "browser_download_url");
        if (string.IsNullOrWhiteSpace(name)
            || !Uri.TryCreate(downloadUrl, UriKind.Absolute, out var uri)
            || !IsAllowedGitHubUri(uri))
        {
            return null;
        }

        var size = assetElement.TryGetProperty("size", out var sizeElement)
            && sizeElement.TryGetInt64(out var parsedSize)
            ? parsedSize
            : 0;
        var digest = GetString(assetElement, "digest");
        if (digest?.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) == true)
        {
            digest = digest["sha256:".Length..].Trim();
        }

        return new GitHubReleaseAsset(name, uri, size, digest);
    }

    private static string GetString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;

    private static bool IsAllowedGitHubUri(Uri uri) =>
        uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
        && (uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
            || uri.Host.Equals("api.github.com", StringComparison.OrdinalIgnoreCase)
            || uri.Host.Equals("objects.githubusercontent.com", StringComparison.OrdinalIgnoreCase)
            || uri.Host.Equals("release-assets.githubusercontent.com", StringComparison.OrdinalIgnoreCase));

    private static string ComputeChecksum(string filePath)
    {
        using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 1024 * 128,
            options: FileOptions.SequentialScan);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static void ExtractZipSafely(string archivePath, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);
        var root = Path.GetFullPath(destinationDirectory);
        var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;

        using var archive = ZipFile.OpenRead(archivePath);
        foreach (var entry in archive.Entries)
        {
            var targetPath = Path.GetFullPath(Path.Combine(root, entry.FullName));
            if (!targetPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase)
                && !targetPath.Equals(root, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("El paquete contiene una ruta no válida.");
            }

            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(targetPath);
                continue;
            }

            var parent = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(parent))
            {
                Directory.CreateDirectory(parent);
            }

            using var source = entry.Open();
            using var destination = new FileStream(
                targetPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None);
            source.CopyTo(destination);
        }
    }

    private static bool LaunchInstaller(string installerPath, string workDirectory)
    {
        return Process.Start(new ProcessStartInfo
        {
            FileName = installerPath,
            WorkingDirectory = workDirectory,
            UseShellExecute = true
        }) is not null;
    }

    private static bool LaunchPortableUpdater(string sourcePath, string workDirectory)
    {
        var targetPath = Path.GetFullPath(Application.ExecutablePath);
        var targetDirectory = Path.GetDirectoryName(targetPath);
        if (string.IsNullOrWhiteSpace(targetDirectory) || !CanWriteDirectory(targetDirectory))
        {
            return false;
        }

        var scriptPath = Path.Combine(workDirectory, "apply-update.ps1");
        File.WriteAllText(scriptPath, UpdateScript);
        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            WorkingDirectory = workDirectory,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("-NoLogo");
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-ExecutionPolicy");
        startInfo.ArgumentList.Add("Bypass");
        startInfo.ArgumentList.Add("-File");
        startInfo.ArgumentList.Add(scriptPath);
        startInfo.ArgumentList.Add("-Source");
        startInfo.ArgumentList.Add(Path.GetFullPath(sourcePath));
        startInfo.ArgumentList.Add("-Target");
        startInfo.ArgumentList.Add(targetPath);
        startInfo.ArgumentList.Add("-ProcessId");
        startInfo.ArgumentList.Add(Environment.ProcessId.ToString());
        startInfo.ArgumentList.Add("-WorkDirectory");
        startInfo.ArgumentList.Add(Path.GetFullPath(workDirectory));
        return Process.Start(startInfo) is not null;
    }

    private static bool CanWriteDirectory(string directory)
    {
        var probePath = Path.Combine(directory, $".vibedlna-update-{Guid.NewGuid():N}.tmp");
        try
        {
            using (File.Create(probePath))
            {
            }

            File.Delete(probePath);
            return true;
        }
        catch
        {
            try
            {
                if (File.Exists(probePath))
                {
                    File.Delete(probePath);
                }
            }
            catch
            {
                // Best effort cleanup.
            }

            return false;
        }
    }

    private static void TryDeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch
        {
            // Temporary update files can be cleaned up by the OS later.
        }
    }

    private const string UpdateScript = """
param(
    [Parameter(Mandatory = $true)][string]$Source,
    [Parameter(Mandatory = $true)][string]$Target,
    [Parameter(Mandatory = $true)][int]$ProcessId,
    [Parameter(Mandatory = $true)][string]$WorkDirectory
)

$ErrorActionPreference = "Stop"

for ($attempt = 0; $attempt -lt 180; $attempt++) {
    $runningProcess = Get-Process -Id $ProcessId -ErrorAction SilentlyContinue
    if ($null -eq $runningProcess) {
        break
    }

    Start-Sleep -Milliseconds 500
}

$copied = $false
for ($attempt = 0; $attempt -lt 30; $attempt++) {
    try {
        Copy-Item -LiteralPath $Source -Destination $Target -Force
        $copied = $true
        break
    }
    catch {
        Start-Sleep -Milliseconds 500
    }
}

if (-not $copied) {
    throw "No se pudo reemplazar VibeDLNA.exe."
}

$targetDirectory = Split-Path -Parent $Target
Start-Process -FilePath $Target -WorkingDirectory $targetDirectory

try {
    Remove-Item -LiteralPath $WorkDirectory -Recurse -Force -ErrorAction SilentlyContinue
}
catch {
    # The temporary folder is safe to remove later.
}
""";
}

internal enum UpdateCheckState
{
    UpToDate,
    Available,
    NoRelease,
    Unavailable,
    Error
}

internal sealed record UpdateCheckResult(
    UpdateCheckState State,
    Version CurrentVersion,
    GitHubReleaseInfo? Release,
    string Message);

internal sealed record GitHubReleaseInfo(
    string TagName,
    Version Version,
    string Name,
    string HtmlUrl,
    GitHubReleaseAsset? Asset);

internal sealed record GitHubReleaseAsset(
    string Name,
    Uri DownloadUri,
    long Size,
    string? Sha256);

internal sealed record UpdateInstallResult(bool Started, string Message);
