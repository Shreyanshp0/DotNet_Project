using DotNet_Project.Data;
using DotNet_Project.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace DotNet_Project.Controllers;

[Authorize(Roles = "Child")]
public class ChildController(ApplicationDbContext db, UserManager<ApplicationUser> userManager) : Controller
{
    public async Task<IActionResult> Index()
    {
        var userId = userManager.GetUserId(User)!;
        var profile = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
            db.ChildProfiles, x => x.ChildUserId == userId);
        if (profile is null) return Forbid();
        return View("~/Views/Child/Index.cshtml");
    }

    public IActionResult MoodCheckIn() => View("~/Views/Child/MoodCheckIn.cshtml");
    public IActionResult Assessment() => View("~/Views/Child/Assessment.cshtml");
    public IActionResult Journal() => View("~/Views/Child/Journal.cshtml");
    public IActionResult JournalHistory() => View("~/Views/Child/JournalHistory.cshtml");
    public IActionResult Buddy() => View("~/Views/Child/Buddy.cshtml");
}
