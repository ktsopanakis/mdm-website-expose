using Microsoft.Playwright;

namespace LegacyApiProxy.Models;

/// <summary>
/// Wraps an active Playwright page for a given user session.
/// Touch() is called on every successful use to slide the expiry window.
/// </summary>
public sealed class SessionEntry : IAsyncDisposable
{
    private readonly TimeSpan _ttl;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public IPage Page { get; }
    public string ApiKey { get; }
    public DateTime ExpiresAt { get; private set; }
    public bool IsExpired => DateTime.UtcNow > ExpiresAt;

    public SessionEntry(string apiKey, IPage page, TimeSpan ttl)
    {
        ApiKey = apiKey;
        Page = page;
        _ttl = ttl;
        ExpiresAt = DateTime.UtcNow.Add(ttl);
    }

    /// <summary>Acquire exclusive access to the page for the duration of a request.</summary>
    public Task<IDisposable> AcquireAsync(CancellationToken ct = default) =>
        _lock.AcquireAsync(ct);

    /// <summary>Slide the expiry window forward from now.</summary>
    public void Touch() => ExpiresAt = DateTime.UtcNow.Add(_ttl);

    public async ValueTask DisposeAsync()
    {
        await Page.CloseAsync();
        _lock.Dispose();
    }
}

// Helper to use SemaphoreSlim with a using statement
file static class SemaphoreExtensions
{
    public static async Task<IDisposable> AcquireAsync(this SemaphoreSlim sem, CancellationToken ct)
    {
        await sem.WaitAsync(ct);
        return new Releaser(sem);
    }

    private sealed class Releaser(SemaphoreSlim sem) : IDisposable
    {
        public void Dispose() => sem.Release();
    }
}
