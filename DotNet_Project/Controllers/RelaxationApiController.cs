using DotNet_Project.Data;
using DotNet_Project.DTOs;
using DotNet_Project.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DotNet_Project.Controllers;

[ApiController]
[Route("api/relaxation")]
[Authorize(Roles = "Child")]
public class RelaxationApiController(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager) : ControllerBase
{
    private static readonly Dictionary<string, string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Breathing"] = "Breathing",
        ["Grounding"] = "Grounding",
        ["Affirmations"] = "Affirmations"
    };

    [HttpPost("complete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete([FromBody] RecordExerciseSessionRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiErrorResponse.WithError("Invalid exercise session request."));
        }

        var typeKey = request.ExerciseType?.Trim() ?? string.Empty;
        if (!AllowedTypes.TryGetValue(typeKey, out var canonicalType))
        {
            return BadRequest(ApiErrorResponse.WithError("Exercise type must be Breathing, Grounding, or Affirmations.", "exerciseType"));
        }

        if (request.DurationSeconds < 0 || request.DurationSeconds > 3600)
        {
            return BadRequest(ApiErrorResponse.WithError("Duration must be between 0 and 3600 seconds.", "durationSeconds"));
        }

        var profile = await User.GetCurrentChildProfileAsync(db, userManager);
        if (profile is null) return Forbid();

        var session = new ExerciseSession
        {
            ChildProfileId = profile.Id,
            ExerciseType = canonicalType,
            DurationSeconds = request.DurationSeconds,
            WasCompleted = request.WasCompleted,
            CompletedAt = DateTime.UtcNow
        };

        db.ExerciseSessions.Add(session);
        await db.SaveChangesAsync();

        return StatusCode(StatusCodes.Status201Created, new ExerciseSessionDto
        {
            Id = session.Id,
            ExerciseType = session.ExerciseType,
            DurationSeconds = session.DurationSeconds,
            CompletedAt = session.CompletedAt,
            WasCompleted = session.WasCompleted
        });
    }

    [HttpGet("recent")]
    public async Task<IActionResult> Recent()
    {
        var profile = await User.GetCurrentChildProfileAsync(db, userManager);
        if (profile is null) return Forbid();

        var sessions = await db.ExerciseSessions
            .Where(s => s.ChildProfileId == profile.Id && s.WasCompleted)
            .OrderByDescending(s => s.CompletedAt)
            .Take(5)
            .Select(s => new ExerciseSessionDto
            {
                Id = s.Id,
                ExerciseType = s.ExerciseType,
                DurationSeconds = s.DurationSeconds,
                CompletedAt = s.CompletedAt,
                WasCompleted = s.WasCompleted
            })
            .ToListAsync();

        return Ok(sessions);
    }
}
