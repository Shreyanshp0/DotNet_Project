using System.Security.Cryptography;
using System.Text.Json;
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
    IParentActivityService parentService,
    IJournalSentimentService sentimentService,
    IWeeklyInsightService weeklyInsightService,
    ISupportRequestService supportRequestService) : Controller
{
    private const string CredentialsTempDataKey = "ChildCredentials";

    public async Task<IActionResult> Index()
    {
        var parentId = userManager.GetUserId(User)!;
        ViewBag.Children = await parentService.GetChildrenForParentAsync(parentId);
        ViewBag.UnresolvedSupportCounts = await supportRequestService.GetUnresolvedCountsByChildAsync(parentId);
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> SupportRequests(int childId)
    {
        var parentId = userManager.GetUserId(User)!;
        var childProfile = await parentService.GetOwnedChildProfileAsync(parentId, childId);
        if (childProfile is null)
        {
            var exists = await db.ChildProfiles.AnyAsync(c => c.Id == childId);
            return exists ? Forbid() : NotFound();
        }

        ViewBag.ActiveChild = childProfile;
        ViewBag.ResolvedSupportCount = (await supportRequestService.GetResolvedForChildAsync(parentId, childId)).Count;
        var unresolved = await supportRequestService.GetUnresolvedForChildAsync(parentId, childId);
        return View(unresolved);
    }

    [HttpGet]
    public async Task<IActionResult> SupportHistory(int? childId = null)
    {
        var parentId = userManager.GetUserId(User)!;
        if (childId.HasValue)
        {
            var childProfile = await parentService.GetOwnedChildProfileAsync(parentId, childId.Value);
            if (childProfile is null)
            {
                var exists = await db.ChildProfiles.AnyAsync(c => c.Id == childId.Value);
                return exists ? Forbid() : NotFound();
            }

            ViewBag.ActiveChild = childProfile;
            var childResolved = await supportRequestService.GetResolvedForChildAsync(parentId, childId.Value);
            return View(childResolved);
        }

        var resolved = await supportRequestService.GetResolvedForParentAsync(parentId);
        return View(resolved);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AcknowledgeSupportRequest(int id, int? childId = null, string? parentResponse = null)
    {
        var parentId = userManager.GetUserId(User)!;
        var outcome = await supportRequestService.AcknowledgeAsync(parentId, id, parentResponse);
        var isAjax = string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);

        if (outcome == SupportActionOutcome.NotFound) return NotFound();
        if (outcome == SupportActionOutcome.Forbidden) return Forbid();

        if (isAjax)
        {
            var unresolved = childId.HasValue
                ? await supportRequestService.GetUnresolvedForChildAsync(parentId, childId.Value)
                : await supportRequestService.GetUnresolvedForParentAsync(parentId);
            return Json(new
            {
                success = outcome == SupportActionOutcome.Success,
                status = "Acknowledged",
                unresolvedCount = unresolved.Count,
                message = "Support request acknowledged."
            });
        }

        if (outcome == SupportActionOutcome.Success)
            TempData["SupportFeedback"] = "Support request acknowledged. It will remain open until resolved.";

        return childId.HasValue
            ? RedirectToAction(nameof(SupportRequests), new { childId = childId.Value })
            : RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResolveSupportRequest(int id, int? childId = null, string? parentResponse = null)
    {
        var parentId = userManager.GetUserId(User)!;
        var outcome = await supportRequestService.ResolveAsync(parentId, id, parentResponse);
        var isAjax = string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);

        if (outcome == SupportActionOutcome.NotFound) return NotFound();
        if (outcome == SupportActionOutcome.Forbidden) return Forbid();

        if (isAjax)
        {
            var unresolved = childId.HasValue
                ? await supportRequestService.GetUnresolvedForChildAsync(parentId, childId.Value)
                : await supportRequestService.GetUnresolvedForParentAsync(parentId);
            return Json(new
            {
                success = outcome == SupportActionOutcome.Success,
                status = "Resolved",
                unresolvedCount = unresolved.Count,
                message = "Support request marked as resolved."
            });
        }

        if (outcome == SupportActionOutcome.Success)
            TempData["SupportFeedback"] = "Support request marked as resolved and moved to history.";

        return childId.HasValue
            ? RedirectToAction(nameof(SupportRequests), new { childId = childId.Value })
            : RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult AddChild() => View(new ChildCreateViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddChild(ChildCreateViewModel model)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (model.DateOfBirth > today)
            ModelState.AddModelError(nameof(model.DateOfBirth), "Date of birth cannot be in the future.");
        else if (model.DateOfBirth < today.AddYears(-100))
            ModelState.AddModelError(nameof(model.DateOfBirth), "Please enter a valid date of birth.");

        var requestedUserName = model.UserName?.Trim();
        if (!string.IsNullOrWhiteSpace(requestedUserName))
        {
            if (await userManager.FindByNameAsync(requestedUserName) is not null)
                ModelState.AddModelError(nameof(model.UserName), "This Child ID / username is already in use.");
        }

        var requestedEmail = model.Email?.Trim();
        if (!string.IsNullOrWhiteSpace(requestedEmail))
        {
            if (await userManager.FindByEmailAsync(requestedEmail) is not null)
                ModelState.AddModelError(nameof(model.Email), "This email address is already in use.");
        }

        if (!string.IsNullOrEmpty(model.Password) && model.Password != model.ConfirmPassword)
            ModelState.AddModelError(nameof(model.ConfirmPassword), "Passwords do not match.");

        if (!ModelState.IsValid) return View(model);

        var childUserName = string.IsNullOrWhiteSpace(requestedUserName)
            ? await GenerateUniqueChildIdAsync()
            : requestedUserName;

        var childEmail = string.IsNullOrWhiteSpace(requestedEmail)
            ? $"{childUserName.ToLowerInvariant()}@child.mindcare.local"
            : requestedEmail;

        var initialPassword = string.IsNullOrWhiteSpace(model.Password)
            ? GenerateTemporaryPassword()
            : model.Password;

        var child = new ApplicationUser
        {
            UserName = childUserName,
            Email = childEmail,
            FullName = model.ChildName.Trim(),
            MustChangePassword = true
        };

        await using var transaction = await db.Database.BeginTransactionAsync();
        var created = await userManager.CreateAsync(child, initialPassword);
        if (!created.Succeeded)
        {
            await transaction.RollbackAsync();
            foreach (var error in created.Errors) ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }

        var role = await userManager.AddToRoleAsync(child, "Child");
        if (!role.Succeeded)
        {
            await transaction.RollbackAsync();
            if (await userManager.FindByIdAsync(child.Id) is not null)
            {
                await userManager.DeleteAsync(child);
            }
            foreach (var error in role.Errors) ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }

        var childProfile = new ChildProfile
        {
            ChildUserId = child.Id,
            ParentUserId = userManager.GetUserId(User)!,
            ChildName = model.ChildName.Trim(),
            DateOfBirth = model.DateOfBirth!.Value,
            Gender = model.Gender?.Trim(),
            GradeOrAgeGroup = model.GradeOrAgeGroup?.Trim()
        };
        db.ChildProfiles.Add(childProfile);

        try
        {
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            if (await userManager.FindByIdAsync(child.Id) is not null)
            {
                await userManager.DeleteAsync(child);
            }
            ModelState.AddModelError(string.Empty, "Unable to create the child profile. Please verify the details and try again.");
            return View(model);
        }

        TempData[CredentialsTempDataKey] = JsonSerializer.Serialize(new ChildCredentialsViewModel
        {
            ChildProfileId = childProfile.Id,
            ChildName = childProfile.ChildName,
            ChildId = childUserName,
            TemporaryPassword = initialPassword,
            IsPasswordReset = false
        });

        return RedirectToAction(nameof(ChildCredentials));
    }

    [HttpGet]
    public IActionResult ChildCredentials()
    {
        if (TempData[CredentialsTempDataKey] is not string json || string.IsNullOrWhiteSpace(json))
        {
            return RedirectToAction(nameof(Index));
        }

        var model = JsonSerializer.Deserialize<ChildCredentialsViewModel>(json);
        if (model is null)
        {
            return RedirectToAction(nameof(Index));
        }

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> ChildDetails(int id)
    {
        var parentId = userManager.GetUserId(User)!;
        var childProfile = await parentService.GetOwnedChildProfileAsync(parentId, id);
        if (childProfile is null)
        {
            var exists = await db.ChildProfiles.AnyAsync(c => c.Id == id);
            return exists ? Forbid() : NotFound();
        }

        ViewBag.JournalInsights = await sentimentService.GetInsightsForChildAsync(parentId, childProfile.Id);
        ViewBag.RecentRelaxation = await db.ExerciseSessions
            .Where(s => s.ChildProfileId == childProfile.Id && s.WasCompleted)
            .OrderByDescending(s => s.CompletedAt)
            .Take(5)
            .ToListAsync();
        ViewBag.LatestAssessment = await db.Assessments
            .Where(a => a.ChildProfileId == childProfile.Id)
            .OrderByDescending(a => a.AssessmentDate)
            .ThenByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync();
        return View(childProfile);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RetryJournalAnalysis(int journalEntryId, int childId)
    {
        var parentId = userManager.GetUserId(User)!;
        var childProfile = await parentService.GetOwnedChildProfileAsync(parentId, childId);
        if (childProfile is null)
        {
            var exists = await db.ChildProfiles.AnyAsync(c => c.Id == childId);
            return exists ? Forbid() : NotFound();
        }

        var ownsEntry = await db.JournalEntries
            .AnyAsync(j => j.Id == journalEntryId && j.ChildProfileId == childId);
        if (!ownsEntry)
        {
            return NotFound();
        }

        var analysis = await sentimentService.AnalyzeAndSaveAsync(journalEntryId);
        if (analysis is null)
        {
            TempData["ErrorMessage"] = "Journal analysis is currently unavailable. Please try again later.";
        }
        else
        {
            TempData["SuccessMessage"] = "Journal insight updated.";
        }

        return RedirectToAction(nameof(ChildDetails), new { id = childId });
    }

    [HttpGet]
    public async Task<IActionResult> WeeklyInsights(int childId, string? weekStart = null)
    {
        var parentId = userManager.GetUserId(User)!;
        var childProfile = await parentService.GetOwnedChildProfileAsync(parentId, childId);
        if (childProfile is null)
        {
            var exists = await db.ChildProfiles.AnyAsync(c => c.Id == childId);
            return exists ? Forbid() : NotFound();
        }

        DateOnly? parsedWeekStart = null;
        if (!string.IsNullOrWhiteSpace(weekStart) && DateOnly.TryParse(weekStart, out var parsed))
        {
            parsedWeekStart = parsed;
        }

        var vm = await weeklyInsightService.GetOrGenerateWeeklyInsightAsync(parentId, childId, parsedWeekStart);
        if (vm is null) return NotFound();

        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateWeeklyInsight(int childId, string? weekStart = null)
    {
        var parentId = userManager.GetUserId(User)!;
        var childProfile = await parentService.GetOwnedChildProfileAsync(parentId, childId);
        if (childProfile is null)
        {
            var exists = await db.ChildProfiles.AnyAsync(c => c.Id == childId);
            return exists ? Forbid() : NotFound();
        }

        DateOnly? parsedWeekStart = null;
        if (!string.IsNullOrWhiteSpace(weekStart) && DateOnly.TryParse(weekStart, out var parsed))
        {
            parsedWeekStart = parsed;
        }

        await weeklyInsightService.GetOrGenerateWeeklyInsightAsync(parentId, childId, parsedWeekStart, forceRegenerate: true);
        return RedirectToAction(nameof(WeeklyInsights), new { childId, weekStart });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetChildPassword(int id)
    {
        var parentId = userManager.GetUserId(User)!;
        var childProfile = await parentService.GetOwnedChildProfileAsync(parentId, id);
        if (childProfile is null)
        {
            var exists = await db.ChildProfiles.AnyAsync(c => c.Id == id);
            return exists ? Forbid() : NotFound();
        }

        var childUser = childProfile.ChildUser;
        if (childUser is null)
        {
            return NotFound();
        }

        var tempPassword = GenerateTemporaryPassword();
        var resetToken = await userManager.GeneratePasswordResetTokenAsync(childUser);
        var resetResult = await userManager.ResetPasswordAsync(childUser, resetToken, tempPassword);
        if (!resetResult.Succeeded)
        {
            TempData["ErrorMessage"] = string.Join(" ", resetResult.Errors.Select(e => e.Description));
            return RedirectToAction(nameof(ChildDetails), new { id });
        }

        childUser.MustChangePassword = true;
        await userManager.UpdateAsync(childUser);
        await userManager.SetLockoutEndDateAsync(childUser, null);

        TempData[CredentialsTempDataKey] = JsonSerializer.Serialize(new ChildCredentialsViewModel
        {
            ChildProfileId = childProfile.Id,
            ChildName = childProfile.ChildName,
            ChildId = childUser.UserName ?? childUser.Email ?? string.Empty,
            TemporaryPassword = tempPassword,
            IsPasswordReset = true
        });

        return RedirectToAction(nameof(ChildCredentials));
    }

    private async Task<string> GenerateUniqueChildIdAsync()
    {
        var year = DateTime.UtcNow.Year;
        for (int attempt = 0; attempt < 20; attempt++)
        {
            var num = RandomNumberGenerator.GetInt32(10000, 100000);
            var candidate = $"MC-{year}-{num}";
            if (await userManager.FindByNameAsync(candidate) is null)
            {
                return candidate;
            }
        }
        return $"MC-{year}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
    }

    private static string GenerateTemporaryPassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnopqrstuvwxyz";
        const string digits = "23456789";
        const string special = "!@#$%&*?";
        const string all = upper + lower + digits + special;

        var chars = new char[12];
        chars[0] = upper[RandomNumberGenerator.GetInt32(upper.Length)];
        chars[1] = lower[RandomNumberGenerator.GetInt32(lower.Length)];
        chars[2] = digits[RandomNumberGenerator.GetInt32(digits.Length)];
        chars[3] = special[RandomNumberGenerator.GetInt32(special.Length)];

        for (int i = 4; i < chars.Length; i++)
        {
            chars[i] = all[RandomNumberGenerator.GetInt32(all.Length)];
        }

        for (int i = chars.Length - 1; i > 0; i--)
        {
            int j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }

        return new string(chars);
    }

    public async Task<IActionResult> Report(int? id, int? childId)
    {
        var parentId = userManager.GetUserId(User)!;
        var children = await parentService.GetChildrenForParentAsync(parentId);
        ViewBag.Children = children;

        Assessment? assessment = null;
        ChildProfile? activeChild = null;

        if (id.HasValue && id.Value > 0)
        {
            assessment = await db.Assessments
                .Include(a => a.ChildProfile)
                .Include(a => a.AIReport)
                .FirstOrDefaultAsync(a => a.Id == id.Value);

            if (assessment is null)
            {
                return NotFound();
            }

            if (assessment.ChildProfile.ParentUserId != parentId)
            {
                return Forbid();
            }

            if (childId.HasValue && assessment.ChildProfileId != childId.Value)
            {
                return NotFound();
            }

            activeChild = assessment.ChildProfile;
        }
        else
        {
            activeChild = childId.HasValue
                ? children.FirstOrDefault(c => c.Id == childId.Value)
                : children.FirstOrDefault();

            if (childId.HasValue && activeChild is null)
            {
                var exists = await db.ChildProfiles.AnyAsync(c => c.Id == childId.Value);
                return exists ? Forbid() : NotFound();
            }

            if (activeChild is not null)
            {
                assessment = await db.Assessments
                    .Include(a => a.ChildProfile)
                    .Include(a => a.AIReport)
                    .Where(a => a.ChildProfileId == activeChild.Id && a.ChildProfile.ParentUserId == parentId)
                    .OrderByDescending(a => a.AssessmentDate)
                    .ThenByDescending(a => a.CreatedAt)
                    .FirstOrDefaultAsync();
            }
        }

        ViewBag.ActiveChild = activeChild;

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

        if (childId.HasValue && activeChild is null)
        {
            var exists = await db.ChildProfiles.AnyAsync(c => c.Id == childId.Value);
            return exists ? Forbid() : NotFound();
        }

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

        if (childId.HasValue && activeChild is null)
        {
            var exists = await db.ChildProfiles.AnyAsync(c => c.Id == childId.Value);
            return exists ? Forbid() : NotFound();
        }

        ViewBag.ActiveChild = activeChild;

        if (activeChild is not null)
        {
            ViewBag.Assessments = await db.Assessments
                .Where(a => a.ChildProfileId == activeChild.Id)
                .OrderByDescending(a => a.AssessmentDate)
                .ThenByDescending(a => a.CreatedAt)
                .ToListAsync();

            ViewBag.Journals = await db.JournalEntries
                .Where(j => j.ChildProfileId == activeChild.Id)
                .OrderByDescending(j => j.EntryDate)
                .ThenByDescending(j => j.CreatedAt)
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

        if (childId.HasValue && targetChild is null)
        {
            var exists = await db.ChildProfiles.AnyAsync(c => c.Id == childId.Value);
            return exists ? Forbid() : NotFound();
        }

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
                .ThenByDescending(a => a.CreatedAt)
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
        if (targetChild is null)
        {
            if (childId.HasValue && await db.ChildProfiles.AnyAsync(c => c.Id == childId.Value))
            {
                return Forbid();
            }
            return View();
        }

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
