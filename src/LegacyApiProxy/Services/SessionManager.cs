using System.Collections.Concurrent;
using LegacyApiProxy.Models;
using Microsoft.Playwright;

namespace LegacyApiProxy.Services;

/// <summary>
/// Owns all active browser sessions.
///
/// Session lifecycle:
///   - Created lazily on the first API call for a given API key.
///   - Reused on subsequent calls while IsExpired == false.
///   - Touch()'d after each successful use to slide the TTL window.
///   - Expired sessions are evicted by SessionKeepaliveService.
///   - If the legacy site has logged the user out mid-session, Login is retried once.
/// </summary>
public sealed class SessionManager : IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, SessionEntry> _sessions = new();
    private readonly ILegacySiteClient _client;
    private readonly IConfiguration _config;
    private readonly ILogger<SessionManager> _logger;

    private IPlaywright? _playwright;
    private IBrowser? _browser;

    public SessionManager(
        ILegacySiteClient client,
        IConfiguration config,
        ILogger<SessionManager> logger)
    {
        _client = client;
        _config = config;
        _logger = logger;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns an authenticated, exclusive-access IPage for the given API key.
    /// Creates or re-authenticates the session as needed.
    /// Caller must dispose the returned handle to release the page lock.
    /// </summary>
    public async Task<(IPage Page, IDisposable Lock)> GetOrCreateSessionAsync(
        string apiKey,
        CancellationToken ct = default)
    {
        var credentials = ResolveCredentials(apiKey);
        await EnsureBrowserAsync();

        var entry = await GetOrCreateEntryAsync(apiKey, credentials, ct);

        var pageLock = await entry.AcquireAsync(ct);

        // Guard: if the legacy site expired the server-side session, re-login
        if (!await _client.IsLoggedInAsync(entry.Page))
        {
            _logger.LogWarning("Session for {ApiKey} is no longer authenticated. Re-logging in.", apiKey);
            await _client.LoginAsync(entry.Page, credentials.Username, credentials.Password);
        }

        entry.Touch();
        return (entry.Page, pageLock);
    }

    /// <summary>Called by the keepalive service to ping and evict sessions.</summary>
    public IEnumerable<(string Key, SessionEntry Entry)> GetAllSessions() =>
        _sessions.Select(kv => (kv.Key, kv.Value));

    public async Task RemoveSessionAsync(string apiKey)
    {
        if (_sessions.TryRemove(apiKey, out var entry))
        {
            _logger.LogInformation("Removing session for {ApiKey}", apiKey);
            await entry.DisposeAsync();
        }
    }

    // ── Internals ─────────────────────────────────────────────────────────────

    private async Task<SessionEntry> GetOrCreateEntryAsync(
        string apiKey,
        ApiKeyConfig credentials,
        CancellationToken ct)
    {
        if (_sessions.TryGetValue(apiKey, out var existing) && !existing.IsExpired)
            return existing;

        // Evict the stale entry if present
        if (_sessions.TryRemove(apiKey, out var stale))
            await stale.DisposeAsync();

        _logger.LogInformation("Creating new session for {ApiKey}", apiKey);

        var ttl = TimeSpan.FromMinutes(
            _config.GetValue<int>("LegacySite:SessionTtlMinutes", 30));

        var page = await _browser!.NewPageAsync();
        await _client.LoginAsync(page, credentials.Username, credentials.Password);

        var entry = new SessionEntry(apiKey, page, ttl);
        _sessions[apiKey] = entry;
        return entry;
    }

    private ApiKeyConfig ResolveCredentials(string apiKey)
    {
        var cfg = _config.GetSection($"ApiKeys:{apiKey}").Get<ApiKeyConfig>()
            ?? throw new UnauthorizedAccessException($"Unknown API key: {apiKey}");
        return cfg;
    }

    private async Task EnsureBrowserAsync()
    {
        if (_browser is not null) return;

        _playwright = await Playwright.CreateAsync();
        var headless = _config.GetValue<bool>("LegacySite:Headless", true);

        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = headless,
            // Reduce fingerprinting — helps with sites that detect automation
            Args = ["--disable-blink-features=AutomationControlled"]
        });
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var (_, entry) in _sessions)
            await entry.DisposeAsync();

        _sessions.Clear();

        if (_browser is not null) await _browser.CloseAsync();
        _playwright?.Dispose();
    }
}
