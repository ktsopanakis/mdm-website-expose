using LegacyApiProxy.Services;
using Microsoft.AspNetCore.Mvc;

namespace LegacyApiProxy.Controllers;

/// <summary>
/// Read-only endpoints — scrape data from the legacy site and return it as JSON.
/// All endpoints require the X-Api-Key header.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class InfoController(SessionManager sessions, ILegacySiteClient client) : ControllerBase
{
    /// <summary>Returns the authenticated user's profile information.</summary>
    [HttpGet("profile")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetProfile(
        [FromHeader(Name = "X-Api-Key")] string apiKey,
        CancellationToken ct)
    {
        try
        {
            var (page, pageLock) = await sessions.GetOrCreateSessionAsync(apiKey, ct);
            using (pageLock)
            {
                var info = await client.GetUserInfoAsync(page);
                return Ok(info);
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { error = ex.Message });
        }
    }

    // ── Add more GET endpoints here ──────────────────────────────────────────
    //
    // Example pattern:
    //
    // [HttpGet("some-page")]
    // public async Task<IActionResult> GetSomething(
    //     [FromHeader(Name = "X-Api-Key")] string apiKey, CancellationToken ct)
    // {
    //     var (page, pageLock) = await sessions.GetOrCreateSessionAsync(apiKey, ct);
    //     using (pageLock)
    //     {
    //         var result = await client.GetSomethingAsync(page);
    //         return Ok(result);
    //     }
    // }
}
