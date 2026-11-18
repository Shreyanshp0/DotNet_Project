using DotNet_Project.Data;
using DotNet_Project.Models;
using DotNet_Project.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DotNet_Project.Controllers;

[Authorize(Roles = "Parent")]
public class ParentController(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager) : Controller
{
    public async Task<IActionResult> Index()
    {
        var parentId = userManager.GetUserId(User)!;
        ViewBag.Children = await db.ChildProfiles.Where(x => x.ParentUserId == parentId)
            .OrderBy(x => x.ChildName).ToListAsync();
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

    public IActionResult Report(int id = 1042) => View();
    public IActionResult History() => View();
    public IActionResult DailyActivity() => View();
    public IActionResult QuestionnaireDetails() => View();
    public IActionResult JournalDetails() => View();
}
