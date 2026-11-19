using DotNet_Project.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DotNet_Project.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager)
    {
        // 1. Ensure Roles Exist
        foreach (var role in new[] { "Parent", "Child" })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        // 2. Demo Parent User: Sarah Jenkins
        const string parentEmail = "parent@mindcare.com";
        var parentUser = await userManager.FindByEmailAsync(parentEmail);
        if (parentUser == null)
        {
            parentUser = new ApplicationUser
            {
                UserName = parentEmail,
                Email = parentEmail,
                FullName = "Sarah Jenkins",
                EmailConfirmed = true,
                CreatedAt = DateTime.UtcNow
            };
            var res = await userManager.CreateAsync(parentUser, "ParentPass1!");
            if (res.Succeeded)
            {
                await userManager.AddToRoleAsync(parentUser, "Parent");
            }
        }

        // 3. Demo Child User: Alex Jenkins (Grade 7, Age 12)
        const string childEmail = "alex@mindcare.com";
        var childUser = await userManager.FindByEmailAsync(childEmail);
        if (childUser == null && parentUser != null)
        {
            childUser = new ApplicationUser
            {
                UserName = childEmail,
                Email = childEmail,
                FullName = "Alex Jenkins",
                EmailConfirmed = true,
                CreatedAt = DateTime.UtcNow
            };
            var res = await userManager.CreateAsync(childUser, "ChildPass1!");
            if (res.Succeeded)
            {
                await userManager.AddToRoleAsync(childUser, "Child");

                var profile = new ChildProfile
                {
                    ChildUserId = childUser.Id,
                    ParentUserId = parentUser.Id,
                    ChildName = "Alex Jenkins",
                    DateOfBirth = new DateOnly(2014, 3, 10),
                    Gender = "Male",
                    GradeOrAgeGroup = "Grade 7",
                    CreatedAt = DateTime.UtcNow
                };
                db.ChildProfiles.Add(profile);
                await db.SaveChangesAsync();
            }
        }
    }
}
