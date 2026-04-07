# Tasks — LegacyApiProxy

Tasks are grouped by phase. Complete Phase 1 before moving to the others.

---

## Phase 1 — Connect to the Real Legacy Site

These are the minimum steps to get the proof-of-concept working against your target site.

- [ ] **Set BaseUrl** — update `LegacySite:BaseUrl` in `appsettings.json`.
- [ ] **Fill in the login flow** — open `LegacySiteClient.cs` and replace the `LoginAsync`
      selectors (`#username`, `#password`, `button[type='submit']`) with the real ones.
      Run with `Headless: false` to watch and debug.
- [ ] **Fill in IsLoggedInAsync** — pick a selector that reliably appears only when
      authenticated (user menu, avatar, dashboard element, etc.).
- [ ] **Fill in GetUserInfoAsync** — navigate to the profile page, scrape the real
      fields, update `UserInfoDto` to match.
- [ ] **Fill in ChangePasswordAsync** — navigate to the password-change page, fill the
      real form, wait for the real success signal.
- [ ] **Add a real API key** — put a test key + credentials in `appsettings.Development.json`
      and verify an end-to-end call with curl or the Scalar UI (`/scalar/v1`).
- [ ] **Install Playwright browsers** — after `dotnet build`, run:
      `pwsh src/LegacyApiProxy/bin/Debug/net9.0/playwright.ps1 install chromium`

---

## Phase 2 — Harden the Session Layer

- [ ] **Keepalive navigation** — replace the `page.ReloadAsync()` in `SessionKeepaliveService`
      with a cheap page on the legacy site (e.g. `/dashboard`, a ping endpoint).
- [ ] **Handle login failures** — `LoginAsync` should throw a descriptive exception if
      the legacy site returns an error message; surface this as a 401 or 502 from the API.
- [ ] **Session-expired retry** — `SessionManager.GetOrCreateSessionAsync` already re-logs
      in once; verify this path works end-to-end.
- [ ] **Concurrency stress test** — send 5 concurrent requests for the same API key and
      confirm the SemaphoreSlim prevents races (no "two requests on the same page" errors).
- [ ] **Multiple users** — add a second API key / credential pair and confirm both
      sessions are maintained independently.

---

## Phase 3 — Expand the API Surface

For each new legacy site page you want to expose:

- [ ] Add method to `ILegacySiteClient`.
- [ ] Implement in `LegacySiteClient` with Playwright selectors.
- [ ] Add controller action (read → `InfoController`, write → `AccountController`).
- [ ] Add DTO in `Models/` if needed.
- [ ] Document the endpoint in this file once stable.

### Candidate endpoints (fill in as requirements are known)

| Endpoint | Method | Legacy page | Status |
|----------|--------|-------------|--------|
| `GET /api/info/profile` | Read | `/profile` | scaffold done |
| `POST /api/account/change-password` | Write | `/account/security` | scaffold done |
| _(add rows here)_ | | | |

---

## Phase 4 — Production Readiness

- [ ] **Secrets management** — move credentials out of appsettings into environment
      variables, Azure Key Vault, AWS Secrets Manager, or similar.
- [ ] **Rate limiting** — add `AspNetCoreRateLimit` or .NET 8 built-in rate limiting
      to prevent callers from exhausting sessions.
- [ ] **Authentication middleware** — extract the `X-Api-Key` check into a middleware
      or policy rather than repeating it in each controller action.
- [ ] **Health check endpoint** — expose `/healthz` that reports session count and
      browser status.
- [ ] **Metrics** — add counters for session hits, misses, re-logins, and keepalive
      failures (OpenTelemetry or Prometheus).
- [ ] **Docker support** — add a `Dockerfile` that installs Playwright's Chromium
      dependencies (the official `mcr.microsoft.com/playwright/dotnet` base image is
      the easiest path).
- [ ] **Integration tests** — use Playwright's `BrowserContext` record/replay or
      a local mock of the legacy site to test without hitting production.
- [ ] **API versioning** — prefix routes with `/v1/` before the API is public.

---

## Known Risks / Watch Items

| Risk | Mitigation |
|------|------------|
| Legacy site detects Playwright | Add stealth args; use slow-mo in dev; randomise user-agent |
| Legacy site changes selectors | Brittle — monitor for failures; consider attribute-based selectors over ids |
| Memory growth from many sessions | Cap max concurrent sessions; evict LRU if over limit |
| Credentials in config | Phase 4 secrets management task above |
| Long-running Playwright actions block requests | Set `page.SetDefaultTimeoutAsync` and return 504 on timeout |
