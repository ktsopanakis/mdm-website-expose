# Architecture — LegacyApiProxy

## Purpose

Expose a legacy website's functionality as a REST API by automating user interactions
via a headless browser (Playwright). The API accepts an API key per request, maps it
to a set of credentials, and reuses a persistent browser session so the legacy site
is only logged into once per user.

---

## Component Map

```
HTTP Client
    │
    │  X-Api-Key header
    ▼
┌─────────────────────────────────────────────────┐
│  ASP.NET Core Web API                           │
│                                                 │
│  InfoController      — GET  /api/info/*         │
│  AccountController   — POST /api/account/*      │
└───────────────────┬─────────────────────────────┘
                    │ GetOrCreateSessionAsync(apiKey)
                    ▼
┌─────────────────────────────────────────────────┐
│  SessionManager  (Singleton)                    │
│                                                 │
│  ConcurrentDictionary<apiKey, SessionEntry>     │
│                                                 │
│  • Resolves apiKey → credentials (appsettings)  │
│  • Creates browser page on first call           │
│  • Re-authenticates if session expired          │
│  • Locks page per request (SemaphoreSlim)       │
└───────┬───────────────────────┬─────────────────┘
        │ owns                  │ uses
        ▼                       ▼
┌──────────────┐    ┌──────────────────────────────┐
│ SessionEntry │    │  ILegacySiteClient            │
│              │    │  (LegacySiteClient impl)      │
│  IPage       │    │                               │
│  ExpiresAt   │    │  LoginAsync()                 │
│  TTL slide   │    │  IsLoggedInAsync()            │
│  Page lock   │    │  GetUserInfoAsync()           │
└──────────────┘    │  ChangePasswordAsync()        │
                    │  ... one method per action    │
                    └──────────────┬────────────────┘
                                   │ Playwright calls
                                   ▼
                    ┌──────────────────────────────┐
                    │  Chromium (headless)          │
                    │  Playwright Browser           │
                    └──────────────┬───────────────┘
                                   │ HTTP(S)
                                   ▼
                    ┌──────────────────────────────┐
                    │  Legacy Website              │
                    └──────────────────────────────┘

┌─────────────────────────────────────────────────┐
│  SessionKeepaliveService  (BackgroundService)   │
│                                                 │
│  Runs every KeepaliveIntervalMinutes            │
│  • Evicts expired sessions                      │
│  • Pings live sessions (page reload)            │
└─────────────────────────────────────────────────┘
```

---

## Session Lifecycle

```
API Request
    │
    ├─ Session found & not expired?  ──Yes──► Touch TTL ──► use page
    │
    └─ No ──► Create IPage ──► LoginAsync() ──► store SessionEntry
                                    │
                                    ▼
                             Re-login guard:
                             IsLoggedInAsync() == false?
                             ──Yes──► LoginAsync() again (once)
```

---

## Configuration

All runtime knobs live in `appsettings.json` / environment variables:

| Key | Default | Description |
|-----|---------|-------------|
| `LegacySite:BaseUrl` | — | Root URL of the legacy site |
| `LegacySite:SessionTtlMinutes` | 30 | How long a session lives without activity |
| `LegacySite:KeepaliveIntervalMinutes` | 10 | How often the keepalive service runs |
| `LegacySite:Headless` | true | false = show browser window (useful in dev) |
| `ApiKeys:{key}:Username` | — | Credential mapped to this API key |
| `ApiKeys:{key}:Password` | — | Credential mapped to this API key |

In production, inject secrets via environment variables or a secrets vault rather than
committing them in appsettings.json.

---

## Adding a New Endpoint — Checklist

1. **Add a method** to `ILegacySiteClient` for the page action.
2. **Implement it** in `LegacySiteClient` using Playwright locators.
3. **Add a controller action** in the appropriate controller:
   - Read-only → `InfoController`
   - State-changing → `AccountController`
4. **Add a DTO** in `Models/` if the response shape is new.
5. Test with `Headless: false` in `appsettings.Development.json` to watch the browser.

---

## Key Design Decisions

| Decision | Rationale |
|----------|-----------|
| Singleton `SessionManager` | One browser process shared across all requests |
| Per-session `SemaphoreSlim` | Prevents two concurrent requests from racing on the same page |
| TTL sliding window (`Touch`) | Sessions live as long as they are actively used |
| Re-login guard on every request | Handles server-side session expiry gracefully |
| `ILegacySiteClient` interface | Enables mocking in tests without a real browser |
| `Headless: false` in dev | Makes it easy to see and debug page interactions |

---

## Security Notes

- API keys are the only auth boundary between callers and the legacy credentials.
  Use strong, randomly generated keys in production.
- Never commit real credentials to source control — use environment variables.
- Consider adding rate limiting (e.g. `AspNetCoreRateLimit`) to protect against
  session exhaustion attacks.
- Playwright runs with `--disable-blink-features=AutomationControlled` to reduce
  detection; add more stealth flags if the legacy site uses bot-detection.
