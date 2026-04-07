using LegacyApiProxy.Models;
using LegacyApiProxy.Services;
using Microsoft.AspNetCore.Mvc;

namespace LegacyApiProxy.Controllers;

/// <summary>
/// Write/action endpoints — perform state-changing operations on the legacy site.
/// All endpoints require the X-Api-Key header.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class AccountController(SessionManager sessions, ILegacySiteClient client) : ControllerBase
{
    /// <summary>Changes the current user's password on the legacy site.</summary>
    [HttpPost("change-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword(
        [FromHeader(Name = "X-Api-Key")] string apiKey,
        [FromBody] ChangePasswordRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var (page, pageLock) = await sessions.GetOrCreateSessionAsync(apiKey, ct);
            using (pageLock)
            {
                await client.ChangePasswordAsync(page, request.NewPassword);
                return NoContent();
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { error = ex.Message });
        }
    }

    // ── Add more POST/PUT/DELETE endpoints here ──────────────────────────────
}
