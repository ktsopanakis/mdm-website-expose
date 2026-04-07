using LegacyApiProxy.Models;
using Microsoft.Playwright;

namespace LegacyApiProxy.Services;

/// <summary>
/// Playwright implementation of ILegacySiteClient.
///
/// HOW TO ADD A NEW ENDPOINT:
///   1. Add a method signature to ILegacySiteClient.
///   2. Implement it here using Playwright locators.
///   3. Add a controller action that calls SessionManager.GetOrCreateSessionAsync()
///      and then calls your new method.
///
/// SELECTORS:
///   Replace every TODO selector with the real CSS/XPath from the legacy site.
///   Use page.Locator() over page.QuerySelectorAsync() — it retries automatically.
/// </summary>
public sealed class LegacySiteClient(IConfiguration config, ILogger<LegacySiteClient> logger)
    : ILegacySiteClient
{
    private readonly string _baseUrl = config["LegacySite:BaseUrl"]
        ?? throw new InvalidOperationException("LegacySite:BaseUrl is required.");

    // ── Authentication ────────────────────────────────────────────────────────

    public async Task LoginAsync(IPage page, string username, string password)
    {
        logger.LogInformation("Logging in user {Username}", username);

        await page.GotoAsync($"{_baseUrl}/login"); // TODO: adjust login path

        // TODO: replace selectors with real ones from the legacy site
        await page.Locator("#username").FillAsync(username);
        await page.Locator("#password").FillAsync(password);
        await page.Locator("button[type='submit']").ClickAsync();

        // Wait until we land somewhere other than the login page
        await page.WaitForURLAsync(url => !url.Contains("/login"));

        logger.LogInformation("Login successful for {Username}", username);
    }

    public async Task<bool> IsLoggedInAsync(IPage page)
    {
        // TODO: replace with a selector that only exists when authenticated
        // e.g. a user menu, avatar, or dashboard element
        return await page.Locator("#user-menu").CountAsync() > 0;
    }

    // ── Read actions ──────────────────────────────────────────────────────────

    public async Task<UserInfoDto> GetUserInfoAsync(IPage page)
    {
        await page.GotoAsync($"{_baseUrl}/profile"); // TODO: adjust path

        // TODO: replace selectors
        var name = await page.Locator("#profile-name").InnerTextAsync();
        var email = await page.Locator("#profile-email").InnerTextAsync();

        return new UserInfoDto(name.Trim(), email.Trim());
    }

    // ── Write actions ─────────────────────────────────────────────────────────

    public async Task ChangePasswordAsync(IPage page, string newPassword)
    {
        await page.GotoAsync($"{_baseUrl}/account/security"); // TODO: adjust path

        // TODO: replace selectors and flow
        await page.Locator("#new-password").FillAsync(newPassword);
        await page.Locator("#confirm-password").FillAsync(newPassword);
        await page.Locator("button[type='submit']").ClickAsync();

        // Wait for success confirmation
        await page.WaitForSelectorAsync(".alert-success"); // TODO: adjust selector
    }
}
