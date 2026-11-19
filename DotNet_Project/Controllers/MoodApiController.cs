using DotNet_Project.Data;
using DotNet_Project.DTOs;
using DotNet_Project.Models;
using DotNet_Project.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace DotNet_Project.Controllers;

[ApiController]
[Route("api/mood")]
[Authorize(Roles = "Child")]
public class MoodApiController(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    IMoodService moodService) : ControllerBase
{
    [HttpPost("checkin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CheckIn([FromBody] MoodCheckInRequest request)
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

        var (response, error) = await moodService.CheckInAsync(profile.Id, request);
        if (error != null)
        {
            return BadRequest(ApiErrorResponse.WithError(error, "mood"));
        }

        return Ok(response);
    }

    [HttpGet("today")]
    public async Task<IActionResult> Today()
    {
        var profile = await User.GetCurrentChildProfileAsync(db, userManager);
        if (profile is null) return Forbid();

        var result = await moodService.GetTodayEntriesAsync(profile.Id);
        return Ok(result);
    }
}
