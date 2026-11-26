using DotNet_Project.DTOs;
using DotNet_Project.Models;
using DotNet_Project.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace DotNet_Project.Controllers;

[Authorize(Roles = "Parent")]
public class ParentBuddyController(
    UserManager<ApplicationUser> userManager,
    IParentBuddyService parentBuddyService) : Controller
{
    [HttpGet("/Parent/Buddy")]
    public IActionResult Index() => View("~/Views/Parent/Buddy.cshtml");

    [HttpPost("/Parent/Buddy/SendMessage")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendMessage([FromBody] ChatMessageRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.Message))
        {
            return BadRequest(ApiErrorResponse.WithError("Message cannot be empty.", "message"));
        }

        if (request.Message.Length > 500)
        {
            return BadRequest(ApiErrorResponse.WithError("Message cannot exceed 500 characters.", "message"));
        }

        var parentId = userManager.GetUserId(User);
        if (string.IsNullOrEmpty(parentId)) return Forbid();

        var result = await parentBuddyService.SendMessageAsync(parentId, request.Message, request.ConversationId);
        return Json(result);
    }

    [HttpGet("/api/parent-buddy/conversations")]
    public async Task<IActionResult> GetConversations()
    {
        var parentId = userManager.GetUserId(User);
        if (string.IsNullOrEmpty(parentId)) return Forbid();

        var conversations = await parentBuddyService.GetConversationsAsync(parentId);
        return Json(conversations);
    }

    [HttpGet("/api/parent-buddy/history/{conversationId}")]
    public async Task<IActionResult> GetHistory([FromRoute] string conversationId)
    {
        var parentId = userManager.GetUserId(User);
        if (string.IsNullOrEmpty(parentId)) return Forbid();

        var history = await parentBuddyService.GetHistoryAsync(parentId, conversationId);
        return Json(history);
    }
}
