using DotNet_Project.Data;
using DotNet_Project.DTOs;
using DotNet_Project.Models;
using DotNet_Project.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace DotNet_Project.Controllers;

[ApiController]
[Route("api/journal")]
[Authorize(Roles = "Child")]
public class JournalApiController(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    IJournalService journalService) : ControllerBase
{
    [HttpPost("submit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit([FromBody] JournalSubmitRequest request)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState
                .Where(x => x.Value?.Errors.Count > 0)
                .ToDictionary(
                    k => char.ToLowerInvariant(k.Key[0]) + k.Key[1..],
                    v => v.Value!.Errors.Select(e => e.ErrorMessage).ToList()
                );
            return BadRequest(new ApiErrorResponse { Errors = errors });
        }

        var profile = await User.GetCurrentChildProfileAsync(db, userManager);
        if (profile is null) return Forbid();

        var (response, conflict, validationError) = await journalService.SubmitAsync(profile.Id, request);

        if (conflict != null)
        {
            return StatusCode(StatusCodes.Status409Conflict, ApiErrorResponse.WithError(conflict, "entryDate"));
        }

        if (validationError != null)
        {
            return BadRequest(ApiErrorResponse.WithError(validationError, "emotion"));
        }

        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpGet("history")]
    public async Task<IActionResult> History([FromQuery] int page = 1, [FromQuery] int size = 10)
    {
        var profile = await User.GetCurrentChildProfileAsync(db, userManager);
        if (profile is null) return Forbid();

        var history = await journalService.GetHistoryAsync(profile.Id, page, size);
        return Ok(history);
    }
}
