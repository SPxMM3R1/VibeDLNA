namespace FolderDlnaServer;

/// <summary>
/// Shares a bounded queue between background thumbnail regeneration and
/// on-demand requests from DLNA clients.
/// </summary>
internal sealed class ThumbnailRequestAgent : IDisposable
{
    private const int MaxConcurrentOperations = 2;
    private const int MaxWarmupOperations = 1;

    private readonly ThumbnailCache _cache;
    private readonly SemaphoreSlim _slots = new(MaxConcurrentOperations, MaxConcurrentOperations);
    private int _disposed;

    public ThumbnailRequestAgent(ThumbnailCache cache)
    {
        _cache = cache;
    }

    public async Task<string?> RequestAsync(string filePath, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        await _slots.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Dispose can race with a request that was waiting for a slot. Do
            // not start new work after shutdown, but let an operation that
            // already owns a slot finish and release it normally.
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();

            // Thumbnail generation uses synchronous Windows Shell APIs. Keep it
            // off the request/accept loop and the UI thread.
            return await Task.Run(
                () => _cache.GetOrCreate(filePath),
                CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            _slots.Release();
        }
    }

    public Task WarmAsync(IEnumerable<string> folders, CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return Task.CompletedTask;
        }

        var roots = NormalizeRoots(folders);
        return Task.Run(
            () => WarmCoreAsync(roots, cancellationToken),
            CancellationToken.None);
    }

    public void Dispose()
    {
        // SemaphoreSlim is deliberately not disposed here. HTTP requests are
        // accepted independently of the server's accept loop and may still be
        // releasing a slot while the server is being torn down. The semaphore
        // has no external handle until its wait handle is requested, so leaving
        // it for GC avoids closing it underneath an active request.
        Interlocked.Exchange(ref _disposed, 1);
    }

    private async Task WarmCoreAsync(IReadOnlyList<string> roots, CancellationToken cancellationToken)
    {
        try
        {
            await Parallel.ForEachAsync(
                ThumbnailCache.EnumerateVideoFiles(roots, cancellationToken),
                new ParallelOptions
                {
                    CancellationToken = cancellationToken,
                    // Keep one operation slot available for on-demand requests
                    // even while the library contains a very large catalogue.
                    MaxDegreeOfParallelism = MaxWarmupOperations
                },
                async (filePath, token) =>
                {
                    try
                    {
                        await RequestAsync(filePath, token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested)
                    {
                        // Cancellation is expected when the server stops or rescans.
                    }
                    catch
                    {
                        // One inaccessible or changing file must not stop the warmup.
                    }
                });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Cancellation is expected when the server stops or restarts the warmup.
        }
    }

    private static string[] NormalizeRoots(IEnumerable<string> folders)
    {
        var roots = new List<string>();
        foreach (var folder in folders)
        {
            if (string.IsNullOrWhiteSpace(folder))
            {
                continue;
            }

            try
            {
                var fullPath = Path.GetFullPath(folder);
                if (Directory.Exists(fullPath)
                    && !roots.Contains(fullPath, StringComparer.OrdinalIgnoreCase))
                {
                    roots.Add(fullPath);
                }
            }
            catch
            {
                // Ignore malformed or unavailable roots.
            }
        }

        return roots.ToArray();
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            throw new ObjectDisposedException(nameof(ThumbnailRequestAgent));
        }
    }
}
