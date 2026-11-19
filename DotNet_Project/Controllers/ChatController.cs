using DotNet_Project.Data;
using DotNet_Project.DTOs;
using DotNet_Project.Models;
using DotNet_Project.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace DotNet_Project.Controllers;

[Authorize(Roles = "Child")]
public class ChatController(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    IChatService chatService) : Controller
{
    [HttpPost("/Chat/SendMessage")]
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

        var profile = await User.GetCurrentChildProfileAsync(db, userManager);
        if (profile is null) return Forbid();

        var result = await chatService.SendMessageAsync(profile.Id, request.Message, request.ConversationId);
        return Json(result);
    }

    [HttpGet("/api/chat/history/{conversationId}")]
    public async Task<IActionResult> GetHistory([FromRoute] string conversationId)
    {
        var profile = await User.GetCurrentChildProfileAsync(db, userManager);
        if (profile is null) return Forbid();

        var history = await chatService.GetHistoryAsync(profile.Id, conversationId);
        return Json(history);
    }
}
