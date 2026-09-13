using System.Collections.Concurrent;

namespace FolderDlnaServer;

/// <summary>
/// Builds ready thumbnails in the background. HTTP requests never call this service.
/// </summary>
internal sealed class ThumbnailWarmupService : IDisposable
{
    private const int MaxConcurrentOperations = 2;

    private readonly ThumbnailCache _cache;
    private readonly Action<string>? _reportFailure;
    private readonly ConcurrentDictionary<string, byte> _reportedFailures = new(
        StringComparer.OrdinalIgnoreCase);
    private readonly object _sync = new();
    private Task? _currentTask;
    private CancellationTokenSource? _currentCancellation;
    private int _disposed;

    public ThumbnailWarmupService(ThumbnailCache cache, Action<string>? reportFailure = null)
    {
        _cache = cache;
        _reportFailure = reportFailure;
    }

    public Task CurrentTask
    {
        get
        {
            lock (_sync)
            {
                return _currentTask ?? Task.CompletedTask;
            }
        }
    }

    public void Start(
        IReadOnlyList<string> folders,
        Func<string, bool> includeFile,
        CancellationToken serverCancellation)
    {
        Task? previousTask;
        CancellationTokenSource? previousCancellation;
        CancellationTokenSource warmupCancellation;

        lock (_sync)
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                return;
            }

            previousTask = _currentTask;
            previousCancellation = _currentCancellation;
            previousCancellation?.Cancel();

            warmupCancellation = CancellationTokenSource.CreateLinkedTokenSource(serverCancellation);
            _currentCancellation = warmupCancellation;
            _currentTask = WarmAsync(folders, includeFile, warmupCancellation.Token);
        }

        if (previousTask is not null || previousCancellation is not null)
        {
            _ = FinishPreviousAsync(previousTask, previousCancellation);
        }
    }

    public async Task StopAsync()
    {
        Task? currentTask;
        CancellationTokenSource? currentCancellation;
        lock (_sync)
        {
            currentTask = _currentTask;
            currentCancellation = _currentCancellation;
            _currentTask = null;
            _currentCancellation = null;
            currentCancellation?.Cancel();
        }

        try
        {
            if (currentTask is not null)
            {
                await currentTask.ConfigureAwait(false);
            }
        }
        catch
        {
            // A single warmup failure must not prevent the server from stopping.
        }
        finally
        {
            currentCancellation?.Dispose();
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        try
        {
            StopAsync().GetAwaiter().GetResult();
        }
        catch
        {
            // Disposal is best effort and must not throw during process shutdown.
        }
    }

    private async Task WarmAsync(
        IReadOnlyList<string> folders,
        Func<string, bool> includeFile,
        CancellationToken cancellationToken)
    {
        try
        {
            await Parallel.ForEachAsync(
                ThumbnailCache.EnumerateMediaFiles(folders, cancellationToken),
                new ParallelOptions
                {
                    CancellationToken = cancellationToken,
                    MaxDegreeOfParallelism = MaxConcurrentOperations
                },
                async (filePath, token) =>
                {
                    try
                    {
                        if (includeFile(filePath))
                        {
                            var checksum = await Task.Run(
                                () => _cache.GetOrCreate(filePath, token),
                                token).ConfigureAwait(false);
                            if (checksum is null)
                            {
                                ReportFailure(filePath);
                            }
                            else
                            {
                                _reportedFailures.TryRemove(filePath, out _);
                            }
                        }
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested)
                    {
                        // Cancellation is expected during a rescan or server stop.
                    }
                    catch
                    {
                        // One inaccessible or unsupported file must not stop other files.
                        ReportFailure(filePath);
                    }
                }).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Cancellation is expected during a rescan or server stop.
        }
        catch
        {
            // Folder enumeration errors are isolated from server startup and browsing.
        }
    }

    private static async Task FinishPreviousAsync(
        Task? previousTask,
        CancellationTokenSource? previousCancellation)
    {
        try
        {
            if (previousTask is not null)
            {
                await previousTask.ConfigureAwait(false);
            }
        }
        catch
        {
            // Cancellation and per-file failures are expected during a rescan.
        }
        finally
        {
            previousCancellation?.Dispose();
        }
    }

    private void ReportFailure(string filePath)
    {
        if (_reportFailure is null
            || !_reportedFailures.TryAdd(filePath, 0))
        {
            return;
        }

        try
        {
            _reportFailure($"No se pudo preparar el artwork de Windows para: {filePath}");
        }
        catch
        {
            // Logging must not stop the remaining warmup operations.
        }
    }
}
