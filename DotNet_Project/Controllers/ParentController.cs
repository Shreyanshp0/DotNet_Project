using DotNet_Project.Data;
using DotNet_Project.DTOs;
using DotNet_Project.Models;
using DotNet_Project.Services;
using DotNet_Project.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DotNet_Project.Controllers;

[Authorize(Roles = "Parent")]
public class ParentController(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    IAIReportService reportService,
    IParentActivityService parentService) : Controller
{
    public async Task<IActionResult> Index()
    {
        var parentId = userManager.GetUserId(User)!;
        ViewBag.Children = await parentService.GetChildrenForParentAsync(parentId);
        return View();
    }

    [HttpGet]
    public IActionResult AddChild() => View(new ChildCreateViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddChild(ChildCreateViewModel model)
    {
        if (model.DateOfBirth > DateOnly.FromDateTime(DateTime.UtcNow))
            ModelState.AddModelError(nameof(model.DateOfBirth), "Date of birth cannot be in the future.");
        if (!ModelState.IsValid) return View(model);

        var email = model.Email.Trim();
        var child = new ApplicationUser { UserName = email, Email = email, FullName = model.ChildName.Trim() };
        await using var transaction = await db.Database.BeginTransactionAsync();
        var created = await userManager.CreateAsync(child, model.Password);
        if (!created.Succeeded)
        {
            foreach (var error in created.Errors) ModelState.AddModelError(string.Empty, error.Description);
            await transaction.RollbackAsync();
            return View(model);
        }
        var role = await userManager.AddToRoleAsync(child, "Child");
        if (!role.Succeeded)
        {
            await transaction.RollbackAsync();
            await userManager.DeleteAsync(child);
            foreach (var error in role.Errors) ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }

        db.ChildProfiles.Add(new ChildProfile
        {
            ChildUserId = child.Id,
            ParentUserId = userManager.GetUserId(User)!,
            ChildName = model.ChildName.Trim(),
            DateOfBirth = model.DateOfBirth!.Value,
            Gender = model.Gender,
            GradeOrAgeGroup = model.GradeOrAgeGroup
        });
        try
        {
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            await userManager.DeleteAsync(child);
            throw;
        }
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Report(int? id)
    {
        var parentId = userManager.GetUserId(User)!;
        var query = db.Assessments
            .Include(a => a.ChildProfile)
            .Include(a => a.AIReport)
            .Where(a => a.ChildProfile.ParentUserId == parentId);

        Assessment? assessment;
        if (id.HasValue && id.Value > 0)
        {
            assessment = await query.FirstOrDefaultAsync(a => a.Id == id.Value);
        }
        else
        {
            assessment = await query
                .OrderByDescending(a => a.AssessmentDate)
                .ThenByDescending(a => a.CreatedAt)
                .FirstOrDefaultAsync();
        }

        if (assessment is null)
        {
            return View();
        }

        var report = assessment.AIReport;
        if (report is null)
        {
            try
            {
                report = await reportService.GenerateReportAsync(assessment.Id);
            }
            catch
            {
                return View();
            }
        }

        var reportResponse = reportService.FormatResponse(report, assessment, assessment.ChildProfile);
        return View(reportResponse);
    }

    public async Task<IActionResult> DailyActivity(int? childId, string? date)
    {
        var parentId = userManager.GetUserId(User)!;
        var children = await parentService.GetChildrenForParentAsync(parentId);
        ViewBag.Children = children;

        var activeChild = childId.HasValue
            ? children.FirstOrDefault(c => c.Id == childId.Value)
            : children.FirstOrDefault();

        if (activeChild is null)
        {
            return View();
        }

        ViewBag.ActiveChild = activeChild;

        DateOnly? targetDate = null;
        if (!string.IsNullOrWhiteSpace(date) && DateOnly.TryParse(date, out var parsed))
        {
            targetDate = parsed;
        }

        var activity = await parentService.GetDailyActivityAsync(parentId, activeChild.Id, targetDate);
        return View(activity);
    }

    public async Task<IActionResult> History(int? childId)
    {
        var parentId = userManager.GetUserId(User)!;
        var children = await parentService.GetChildrenForParentAsync(parentId);
        ViewBag.Children = children;

        var activeChild = childId.HasValue
            ? children.FirstOrDefault(c => c.Id == childId.Value)
            : children.FirstOrDefault();

        ViewBag.ActiveChild = activeChild;

        if (activeChild is not null)
        {
            ViewBag.Assessments = await db.Assessments
                .Where(a => a.ChildProfileId == activeChild.Id)
                .OrderByDescending(a => a.AssessmentDate)
                .ToListAsync();

            ViewBag.Journals = await db.JournalEntries
                .Where(j => j.ChildProfileId == activeChild.Id)
                .OrderByDescending(j => j.EntryDate)
                .ToListAsync();
        }

        return View();
    }

    public async Task<IActionResult> QuestionnaireDetails(int? id, int? childId)
    {
        var parentId = userManager.GetUserId(User)!;
        var children = await parentService.GetChildrenForParentAsync(parentId);
        ViewBag.Children = children;

        var targetChild = childId.HasValue
            ? children.FirstOrDefault(c => c.Id == childId.Value)
            : children.FirstOrDefault();

        ViewBag.ActiveChild = targetChild;

        if (targetChild is null) return View();

        AssessmentAnswersResponse? answers = null;
        if (id.HasValue && id.Value > 0)
        {
            answers = await parentService.GetChildAssessmentAnswersAsync(parentId, targetChild.Id, id.Value);
        }
        else
        {
            var latestAssessment = await db.Assessments
                .Where(a => a.ChildProfileId == targetChild.Id)
                .OrderByDescending(a => a.AssessmentDate)
                .FirstOrDefaultAsync();

            if (latestAssessment is not null)
            {
                answers = await parentService.GetChildAssessmentAnswersAsync(parentId, targetChild.Id, latestAssessment.Id);
            }
        }

        return View(answers);
    }

    public async Task<IActionResult> JournalDetails(int? id, int? childId)
    {
        var parentId = userManager.GetUserId(User)!;
        var children = await parentService.GetChildrenForParentAsync(parentId);
        ViewBag.Children = children;

        var targetChild = childId.HasValue
            ? children.FirstOrDefault(c => c.Id == childId.Value)
            : children.FirstOrDefault();

        ViewBag.ActiveChild = targetChild;
        if (targetChild is null) return View();

        JournalEntry? entry = null;
        if (id.HasValue && id.Value > 0)
        {
            entry = await db.JournalEntries
                .FirstOrDefaultAsync(j => j.Id == id.Value && j.ChildProfileId == targetChild.Id);
        }
        else
        {
            entry = await db.JournalEntries
                .Where(j => j.ChildProfileId == targetChild.Id)
                .OrderByDescending(j => j.EntryDate)
                .FirstOrDefaultAsync();
        }

        return View(entry);
    }

    public async Task<IActionResult> ChatHistory(int? childId, string? conversationId = null)
    {
        var parentId = userManager.GetUserId(User)!;
        var children = await parentService.GetChildrenForParentAsync(parentId);
        ViewBag.Children = children;

        var activeChild = childId.HasValue
            ? children.FirstOrDefault(c => c.Id == childId.Value)
            : children.FirstOrDefault();

        ViewBag.ActiveChild = activeChild;

        if (activeChild is null)
        {
            return View(new ParentChildChatHistoryResponse());
        }

        var history = await parentService.GetChildChatHistoryAsync(parentId, activeChild.Id, conversationId);
        return View(history ?? new ParentChildChatHistoryResponse { ChildProfileId = activeChild.Id, ChildName = activeChild.ChildName });
    }
}
