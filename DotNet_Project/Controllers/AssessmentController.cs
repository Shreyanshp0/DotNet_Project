using DotNet_Project.Data;
using DotNet_Project.Models;
using DotNet_Project.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace DotNet_Project.Controllers;

[Authorize(Roles = "Child")]
public class AssessmentController : Controller
{
    public async Task<IActionResult> Index(
        [FromServices] IGeminiAiService geminiService,
        [FromServices] ApplicationDbContext db,
        [FromServices] UserManager<ApplicationUser> userManager)
    {
        var profile = await User.GetCurrentChildProfileAsync(db, userManager);
        if (profile is null) return Forbid();

        var questions = await geminiService.GenerateQuestionsAsync(profile);
        return View("~/Views/Child/Assessment.cshtml", questions);
    }

    public IActionResult Submitted() => View();
}
