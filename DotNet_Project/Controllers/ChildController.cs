using DotNet_Project.Data;
using DotNet_Project.DTOs;
using DotNet_Project.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DotNet_Project.Controllers;

[Authorize(Roles = "Child")]
public class ChildController(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    DotNet_Project.Services.ISupportRequestService? supportRequestService = null) : Controller
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

        ViewBag.RecentRelaxation = await db.ExerciseSessions
            .Where(s => s.ChildProfileId == profile.Id && s.WasCompleted)
            .OrderByDescending(s => s.CompletedAt)
            .Take(3)
            .ToListAsync();

        if (supportRequestService is not null)
        {
            ViewBag.SupportRequests = await supportRequestService.GetForChildAsync(profile.ChildUserId);
        }

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

    public IActionResult Relaxation() => View("~/Views/Child/Relaxation.cshtml");

    public IActionResult Breathing() => View("~/Views/Child/Breathing.cshtml");

    public IActionResult Grounding() => View("~/Views/Child/Grounding.cshtml");

    public IActionResult Affirmations() => View("~/Views/Child/Affirmations.cshtml");

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateSupportRequest(string? reason, string? message)
    {
        var userId = userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId) || supportRequestService is null)
        {
            return Forbid();
        }

        var (created, isDuplicate, error) = await supportRequestService.CreateAsync(userId, reason, message);
        if (error is not null || created is null)
        {
            TempData["SupportError"] = error ?? "Unable to save your support request right now.";
        }
        else if (isDuplicate)
        {
            TempData["SupportSuccess"] = "Your support request is already saved for your parent to see on their dashboard.";
        }
        else
        {
            TempData["SupportSuccess"] = "Your support request has been saved. Your parent will see it on their dashboard when they sign in.";
        }

        return RedirectToAction(nameof(Index));
    }
}
