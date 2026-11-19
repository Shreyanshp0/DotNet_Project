using DotNet_Project.Data;
using DotNet_Project.DTOs;
using DotNet_Project.Models;
using DotNet_Project.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace DotNet_Project.Controllers;

[ApiController]
[Route("api/assessment")]
public class AssessmentApiController(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    IAssessmentService assessmentService) : ControllerBase
{
    [HttpGet("questions")]
    [Authorize(Roles = "Child")]
    public async Task<IActionResult> Questions([FromServices] IGeminiAiService geminiService)
    {
        var profile = await User.GetCurrentChildProfileAsync(db, userManager);
        if (profile is null) return Forbid();

        var questions = await geminiService.GenerateQuestionsAsync(profile);
        return Ok(questions);
    }

    [HttpPost("submit")]
    [Authorize(Roles = "Child")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit([FromBody] AssessmentSubmitRequest request)
    {
        var profile = await User.GetCurrentChildProfileAsync(db, userManager);
        if (profile is null) return Forbid();

        var (response, errors) = await assessmentService.SubmitAsync(profile.Id, request);
        if (errors is not null)
        {
            return BadRequest(new ApiErrorResponse { Errors = errors });
        }

        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpGet("history")]
    [Authorize(Roles = "Child")]
    public async Task<IActionResult> History([FromQuery] int page = 1, [FromQuery] int size = 10)
    {
        var profile = await User.GetCurrentChildProfileAsync(db, userManager);
        if (profile is null) return Forbid();

        var history = await assessmentService.GetHistoryAsync(profile.Id, page, size);
        return Ok(history);
    }

    [HttpGet("{id:int}/answers")]
    [Authorize(Roles = "Child,Parent")]
    public async Task<IActionResult> Answers([FromRoute] int id)
    {
        var isParent = User.IsInRole("Parent");
        var userId = userManager.GetUserId(User)!;

        int? childProfileId = null;
        string? parentUserId = null;

        if (isParent)
        {
            parentUserId = userId;
        }
        else
        {
            var profile = await User.GetCurrentChildProfileAsync(db, userManager);
            if (profile is null) return Forbid();
            childProfileId = profile.Id;
        }

        var result = await assessmentService.GetAnswersAsync(id, childProfileId, parentUserId);
        if (result is null)
        {
            return NotFound(ApiErrorResponse.WithError("Assessment not found."));
        }

        return Ok(result);
    }
}
