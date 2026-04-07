using LegacyApiProxy.Models;
using Microsoft.Playwright;

namespace LegacyApiProxy.Services;

/// <summary>
/// Abstracts all Playwright interactions with the legacy site.
/// Each method receives an already-authenticated IPage.
/// Add one method per page action you want to expose as an API endpoint.
/// </summary>
public interface ILegacySiteClient
{
    /// <summary>Navigate to the login page and authenticate.</summary>
    Task LoginAsync(IPage page, string username, string password);

    /// <summary>Check whether the current page is the login page (session expired).</summary>
    Task<bool> IsLoggedInAsync(IPage page);

    // ── Example: read actions ────────────────────────────────────────────────

    /// <summary>Scrape user profile information from the legacy site.</summary>
    Task<UserInfoDto> GetUserInfoAsync(IPage page);

    // ── Example: write actions ───────────────────────────────────────────────

    /// <summary>Change the current user's password via the legacy site UI.</summary>
    Task ChangePasswordAsync(IPage page, string newPassword);
}
