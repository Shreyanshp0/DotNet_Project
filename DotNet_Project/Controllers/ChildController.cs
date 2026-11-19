using DotNet_Project.Data;
using DotNet_Project.DTOs;
using DotNet_Project.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DotNet_Project.Controllers;

[Authorize(Roles = "Child")]
public class ChildController(ApplicationDbContext db, UserManager<ApplicationUser> userManager) : Controller
{
    public async Task<IActionResult> Index()
    {
        var profile = await User.GetCurrentChildProfileAsync(db, userManager);
        if (profile is null) return Forbid();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var startUtc = today.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var endUtc = today.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        var todayMoodCount = await db.MoodEntries
            .CountAsync(m => m.ChildProfileId == profile.Id && m.CreatedAt >= startUtc && m.CreatedAt <= endUtc);

        var journalSubmittedToday = await db.JournalEntries
            .AnyAsync(j => j.ChildProfileId == profile.Id && j.EntryDate == today);

        var lastAssessment = await db.Assessments
            .Where(a => a.ChildProfileId == profile.Id && a.Status == "Submitted")
            .OrderByDescending(a => a.AssessmentDate)
            .FirstOrDefaultAsync();

        var progressCount = (todayMoodCount > 0 ? 1 : 0) + (journalSubmittedToday ? 1 : 0);

        var model = new ChildDashboardDto
        {
            ChildProfileId = profile.Id,
            ChildName = profile.ChildName,
            TodayMoodCount = todayMoodCount,
            JournalSubmittedToday = journalSubmittedToday,
            LastAssessmentDate = lastAssessment?.AssessmentDate.ToString("MMMM dd, yyyy"),
            DailyProgressCount = progressCount,
            DailyProgressTotal = 2
        };

        return View("~/Views/Child/Index.cshtml", model);
    }

    public IActionResult MoodCheckIn() => View("~/Views/Child/MoodCheckIn.cshtml");

    public async Task<IActionResult> Assessment([FromServices] DotNet_Project.Services.IGeminiAiService geminiService)
    {
        var profile = await User.GetCurrentChildProfileAsync(db, userManager);
        if (profile is null) return Forbid();

        var questions = await geminiService.GenerateQuestionsAsync(profile);
        return View("~/Views/Child/Assessment.cshtml", questions);
    }

    public IActionResult Journal() => View("~/Views/Child/Journal.cshtml");

    public async Task<IActionResult> JournalHistory()
    {
        var profile = await User.GetCurrentChildProfileAsync(db, userManager);
        if (profile is not null)
        {
            ViewBag.Entries = await db.JournalEntries
                .Where(j => j.ChildProfileId == profile.Id)
                .OrderByDescending(j => j.EntryDate)
                .ToListAsync();
        }
        return View("~/Views/Child/JournalHistory.cshtml");
    }

    public IActionResult Buddy() => View("~/Views/Child/Buddy.cshtml");
}
