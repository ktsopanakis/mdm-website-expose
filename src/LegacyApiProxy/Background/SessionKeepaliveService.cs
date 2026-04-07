using LegacyApiProxy.Services;

namespace LegacyApiProxy.Background;

/// <summary>
/// Runs on a timer to:
///   1. Evict sessions that have passed their TTL.
///   2. Ping still-valid sessions so the legacy site doesn't drop them server-side.
///
/// The keepalive interval should be shorter than the legacy site's own idle timeout.
/// Configure via LegacySite:KeepaliveIntervalMinutes (default: 10).
/// </summary>
public sealed class SessionKeepaliveService(
    SessionManager sessionManager,
    IConfiguration config,
    ILogger<SessionKeepaliveService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(
            config.GetValue<int>("LegacySite:KeepaliveIntervalMinutes", 10));

        logger.LogInformation("Session keepalive service started. Interval: {Interval}", interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(interval, stoppingToken);
            await RunKeepaliveAsync(stoppingToken);
        }
    }

    private async Task RunKeepaliveAsync(CancellationToken ct)
    {
        var sessions = sessionManager.GetAllSessions().ToList();
        logger.LogDebug("Keepalive tick — {Count} active session(s)", sessions.Count);

        foreach (var (key, entry) in sessions)
        {
            if (ct.IsCancellationRequested) break;

            if (entry.IsExpired)
            {
                logger.LogInformation("Evicting expired session for {ApiKey}", key);
                await sessionManager.RemoveSessionAsync(key);
                continue;
            }

            try
            {
                // A lightweight reload or navigation keeps the server-side session alive.
                // TODO: replace with a cheap page on the legacy site (e.g. /ping, /dashboard)
                using var pageLock = await entry.AcquireAsync(ct);
                await entry.Page.ReloadAsync();
                logger.LogDebug("Keepalive ping sent for {ApiKey}", key);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Keepalive failed for {ApiKey} — session will be evicted", key);
                await sessionManager.RemoveSessionAsync(key);
            }
        }
    }
}
