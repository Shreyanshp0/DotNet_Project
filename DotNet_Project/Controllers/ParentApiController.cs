using DotNet_Project.DTOs;
using DotNet_Project.Models;
using DotNet_Project.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace DotNet_Project.Controllers;

[ApiController]
[Route("api/parent")]
[Authorize(Roles = "Parent")]
public class ParentApiController(
    UserManager<ApplicationUser> userManager,
    IParentActivityService parentService) : ControllerBase
{
    [HttpGet("child/{childProfileId:int}/daily-activity")]
    public async Task<IActionResult> GetDailyActivity([FromRoute] int childProfileId, [FromQuery] string? date = null)
    {
        var parentId = userManager.GetUserId(User)!;

        DateOnly? parsedDate = null;
        if (!string.IsNullOrWhiteSpace(date))
        {
            if (!DateOnly.TryParse(date, out var d))
            {
                return BadRequest(ApiErrorResponse.WithError("Invalid date format. Expected YYYY-MM-DD.", "date"));
            }
            parsedDate = d;
        }

        var activity = await parentService.GetDailyActivityAsync(parentId, childProfileId, parsedDate);
        if (activity is null)
        {
            return NotFound(ApiErrorResponse.WithError("Child profile not found."));
        }

        return Ok(activity);
    }

    [HttpGet("child/{childProfileId:int}/journal")]
    public async Task<IActionResult> GetChildJournal([FromRoute] int childProfileId, [FromQuery] int page = 1, [FromQuery] int size = 10)
    {
        var parentId = userManager.GetUserId(User)!;
        var journalHistory = await parentService.GetChildJournalAsync(parentId, childProfileId, page, size);
        if (journalHistory is null)
        {
            return NotFound(ApiErrorResponse.WithError("Child profile not found."));
        }

        return Ok(journalHistory);
    }

    [HttpGet("child/{childProfileId:int}/assessment/{assessmentId:int}/answers")]
    public async Task<IActionResult> GetAssessmentAnswers([FromRoute] int childProfileId, [FromRoute] int assessmentId)
    {
        var parentId = userManager.GetUserId(User)!;
        var answers = await parentService.GetChildAssessmentAnswersAsync(parentId, childProfileId, assessmentId);
        if (answers is null)
        {
            return NotFound(ApiErrorResponse.WithError("Assessment not found."));
        }

        return Ok(answers);
    }

    [HttpGet("child/{childProfileId:int}/chat-history")]
    public async Task<IActionResult> GetChildChatHistory([FromRoute] int childProfileId, [FromQuery] string? conversationId = null)
    {
        var parentId = userManager.GetUserId(User)!;
        var chatHistory = await parentService.GetChildChatHistoryAsync(parentId, childProfileId, conversationId);
        if (chatHistory is null)
        {
            return NotFound(ApiErrorResponse.WithError("Child profile not found."));
        }

        return Ok(chatHistory);
    }
}
