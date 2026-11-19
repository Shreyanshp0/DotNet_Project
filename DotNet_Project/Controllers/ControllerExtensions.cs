using System.Security.Claims;
using DotNet_Project.Data;
using DotNet_Project.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DotNet_Project.Controllers;

public static class ControllerExtensions
{
    public static async Task<ChildProfile?> GetCurrentChildProfileAsync(this ClaimsPrincipal principal, ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        var userId = userManager.GetUserId(principal);
        if (string.IsNullOrEmpty(userId)) return null;
        return await db.ChildProfiles.FirstOrDefaultAsync(c => c.ChildUserId == userId);
    }
}
