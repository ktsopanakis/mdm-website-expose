# LegacyApiProxy

A proof-of-concept ASP.NET Core API that wraps a legacy website using Playwright browser automation.

Each API call is authenticated via an API key that maps to a set of legacy-site credentials.
The browser session is created once and reused across requests — a background service keeps it
alive so the legacy site is not hammered with repeated logins.

## Quick Start

```bash
# 1. Install dependencies
cd src/LegacyApiProxy
dotnet restore

# 2. Install Playwright's Chromium browser
dotnet build
pwsh bin/Debug/net9.0/playwright.ps1 install chromium

# 3. Configure (edit appsettings.Development.json)
#    Set: LegacySite:BaseUrl, ApiKeys:dev-key:Username/Password

# 4. Run (headless: false in dev so you can see the browser)
dotnet run
```

API docs available at `https://localhost:5001/scalar/v1` when running in Development.

## Docs

- [ARCHITECTURE.md](ARCHITECTURE.md) — component map, session lifecycle, design decisions
- [TASKS.md](TASKS.md) — phased task list from PoC to production

## Project Structure

```
src/LegacyApiProxy/
├── Controllers/          # HTTP endpoints (thin — just session + client calls)
├── Services/             # SessionManager, ILegacySiteClient, LegacySiteClient
├── Models/               # DTOs and data classes
├── Background/           # SessionKeepaliveService (hosted service)
├── Program.cs
└── appsettings.json
```

## Adding a New Endpoint

1. Add method to `ILegacySiteClient`
2. Implement in `LegacySiteClient` with Playwright selectors
3. Add controller action in `InfoController` (reads) or `AccountController` (writes)
4. See [TASKS.md — Phase 3](TASKS.md) for the full checklist
