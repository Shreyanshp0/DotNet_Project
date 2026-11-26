using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using DotNet_Project.Data;
using DotNet_Project.DTOs;
using DotNet_Project.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DotNet_Project.Tests;

public class ChildAccountWorkflowTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ChildAccountWorkflowTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClient(bool allowAutoRedirect = false) =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = allowAutoRedirect,
            HandleCookies = true
        });

    private async Task<(HttpClient Client, string Email, string UserId)> RegisterParentAsync(string fullName = "Parent Test")
    {
        var client = CreateClient(allowAutoRedirect: true);
        var email = $"parent_{Guid.NewGuid():N}@example.com";
        var res = await client.PostAsync("/Account/Register", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["FullName"] = fullName,
            ["Email"] = email,
            ["Password"] = "ParentPass1!",
            ["ConfirmPassword"] = "ParentPass1!"
        }));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        Assert.NotNull(user);
        return (client, email, user!.Id);
    }

    private static (string ChildId, string TempPassword) ExtractCredentialsFromHtml(string html)
    {
        var idMatch = Regex.Match(html, @"id=""credChildId""[^>]*>([^<]+)<");
        var pwdMatch = Regex.Match(html, @"id=""credTempPassword""[^>]*>([^<]+)<");
        Assert.True(idMatch.Success, "Could not find Child ID in credentials page.");
        Assert.True(pwdMatch.Success, "Could not find Temporary Password in credentials page.");
        return (WebUtility.HtmlDecode(idMatch.Groups[1].Value.Trim()), WebUtility.HtmlDecode(pwdMatch.Groups[1].Value.Trim()));
    }

    [Fact]
    public async Task AddChild_AutoGeneratesUniqueCredentials_LinksProfile_AndEnforcesFirstLoginPasswordChange()
    {
        var (parentClient, _, parentId) = await RegisterParentAsync("Parent One");

        // 1. Create two children with auto-generated IDs and temporary passwords
        var addRes1 = await parentClient.PostAsync("/Parent/AddChild", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["ChildName"] = "Mia Test",
            ["DateOfBirth"] = "2015-05-12"
        }));
        Assert.Equal(HttpStatusCode.OK, addRes1.StatusCode);
        var html1 = await addRes1.Content.ReadAsStringAsync();
        var (childId1, tempPwd1) = ExtractCredentialsFromHtml(html1);

        var addRes2 = await parentClient.PostAsync("/Parent/AddChild", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["ChildName"] = "Leo Test",
            ["DateOfBirth"] = "2016-08-20"
        }));
        Assert.Equal(HttpStatusCode.OK, addRes2.StatusCode);
        var html2 = await addRes2.Content.ReadAsStringAsync();
        var (childId2, _) = ExtractCredentialsFromHtml(html2);

        Assert.NotEqual(childId1, childId2);
        Assert.StartsWith("MC-", childId1);

        // Verify role, MustChangePassword, and ChildProfile linkage in DB
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var childUser = await userManager.FindByNameAsync(childId1);
            Assert.NotNull(childUser);
            Assert.True(childUser!.MustChangePassword);
            Assert.True(await userManager.IsInRoleAsync(childUser, "Child"));

            var profile = await db.ChildProfiles.FirstOrDefaultAsync(p => p.ChildUserId == childUser.Id);
            Assert.NotNull(profile);
            Assert.Equal(parentId, profile!.ParentUserId);
        }

        // 2. Child logs in with generated Child ID & temp password -> redirected to ChangePassword
        var childClientNoRedirect = CreateClient(allowAutoRedirect: false);
        var loginRes = await childClientNoRedirect.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["UserNameOrEmail"] = childId1,
            ["Password"] = tempPwd1
        }));
        Assert.Equal(HttpStatusCode.Redirect, loginRes.StatusCode);
        Assert.Contains("/Account/ChangePassword", loginRes.Headers.Location?.ToString());

        // 3. While MustChangePassword is true, child dashboard is blocked, API is 403, Logout is allowed
        var dashboardWhileBlocked = await childClientNoRedirect.GetAsync("/Child/Index");
        Assert.Equal(HttpStatusCode.Redirect, dashboardWhileBlocked.StatusCode);
        Assert.Contains("/Account/ChangePassword", dashboardWhileBlocked.Headers.Location?.ToString());

        var apiWhileBlocked = await childClientNoRedirect.PostAsJsonAsync("/api/mood/checkin", new MoodCheckInRequest { Mood = "Great" });
        Assert.Equal(HttpStatusCode.Forbidden, apiWhileBlocked.StatusCode);

        // 4. Child changes password
        var newPassword = "ChildNewPass1!";
        var changeRes = await childClientNoRedirect.PostAsync("/Account/ChangePassword", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["CurrentPassword"] = tempPwd1,
            ["NewPassword"] = newPassword,
            ["ConfirmNewPassword"] = newPassword
        }));
        Assert.Equal(HttpStatusCode.Redirect, changeRes.StatusCode);
        Assert.Contains("/Child", changeRes.Headers.Location?.ToString());

        // 5. Child can now access dashboard
        var dashboardAfterChange = await childClientNoRedirect.GetAsync("/Child/Index");
        Assert.Equal(HttpStatusCode.OK, dashboardAfterChange.StatusCode);

        // 6. Old temporary password no longer works
        var oldPwdClient = CreateClient(allowAutoRedirect: false);
        var oldLoginRes = await oldPwdClient.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["UserNameOrEmail"] = childId1,
            ["Password"] = tempPwd1
        }));
        Assert.Equal(HttpStatusCode.OK, oldLoginRes.StatusCode); // Returns login view with error, not redirect
    }

    [Fact]
    public async Task AddChild_DuplicateUsernameOrInvalidInput_IsRejectedWithoutOrphans()
    {
        var (parentClient, _, _) = await RegisterParentAsync("Parent Dup");
        var customUsername = $"custom_{Guid.NewGuid():N}"[..16];

        // Create first child with custom username
        var res1 = await parentClient.PostAsync("/Parent/AddChild", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["ChildName"] = "First Child",
            ["DateOfBirth"] = "2015-01-01",
            ["UserName"] = customUsername
        }));
        Assert.Equal(HttpStatusCode.OK, res1.StatusCode);

        // Attempt duplicate username
        var res2 = await parentClient.PostAsync("/Parent/AddChild", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["ChildName"] = "Duplicate Child",
            ["DateOfBirth"] = "2015-01-01",
            ["UserName"] = customUsername
        }));
        Assert.Equal(HttpStatusCode.OK, res2.StatusCode);
        var html2 = await res2.Content.ReadAsStringAsync();
        Assert.Contains("already in use", html2);

        // Attempt invalid future date of birth
        var badUsername = $"bad_{Guid.NewGuid():N}"[..16];
        var res3 = await parentClient.PostAsync("/Parent/AddChild", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["ChildName"] = "Future Child",
            ["DateOfBirth"] = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)).ToString("yyyy-MM-dd"),
            ["UserName"] = badUsername
        }));
        Assert.Equal(HttpStatusCode.OK, res3.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.Null(await userManager.FindByNameAsync(badUsername));
        Assert.False(await db.ChildProfiles.AnyAsync(p => p.ChildName == "Future Child"));
    }

    [Fact]
    public async Task ParentResetChildPassword_GeneratesNewTempPassword_AndRequiresChangeOnNextLogin()
    {
        var (parentClient, _, _) = await RegisterParentAsync("Parent Reset");
        var addRes = await parentClient.PostAsync("/Parent/AddChild", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["ChildName"] = "Sam Reset",
            ["DateOfBirth"] = "2014-06-15"
        }));
        var (childId, tempPwd1) = ExtractCredentialsFromHtml(await addRes.Content.ReadAsStringAsync());

        int profileId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            profileId = (await db.ChildProfiles.FirstAsync(c => c.ChildUser.UserName == childId)).Id;
        }

        // Parent resets password
        var resetRes = await parentClient.PostAsync($"/Parent/ResetChildPassword/{profileId}", new FormUrlEncodedContent(new Dictionary<string, string>()));
        Assert.Equal(HttpStatusCode.OK, resetRes.StatusCode);
        var (resetChildId, tempPwd2) = ExtractCredentialsFromHtml(await resetRes.Content.ReadAsStringAsync());
        Assert.Equal(childId, resetChildId);
        Assert.NotEqual(tempPwd1, tempPwd2);

        // Child can log in with new temporary password and is prompted to change it
        var childClient = CreateClient(allowAutoRedirect: false);
        var loginRes = await childClient.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["UserNameOrEmail"] = childId,
            ["Password"] = tempPwd2
        }));
        Assert.Equal(HttpStatusCode.Redirect, loginRes.StatusCode);
        Assert.Contains("/Account/ChangePassword", loginRes.Headers.Location?.ToString());
    }

    [Fact]
    public async Task Authorization_EnforcesParentOwnership_ChildIsolation_AndUnauthenticatedRedirects()
    {
        var (parentA, _, _) = await RegisterParentAsync("Parent A");
        var (parentB, _, _) = await RegisterParentAsync("Parent B");

        var addRes = await parentA.PostAsync("/Parent/AddChild", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["ChildName"] = "Owned Child",
            ["DateOfBirth"] = "2015-04-10"
        }));
        var (childId, tempPwd) = ExtractCredentialsFromHtml(await addRes.Content.ReadAsStringAsync());

        int profileId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            profileId = (await db.ChildProfiles.FirstAsync(c => c.ChildUser.UserName == childId)).Id;
        }

        // Parent B cannot view Parent A's child details or reset password
        var detailsByB = await parentB.GetAsync($"/Parent/ChildDetails/{profileId}");
        Assert.Equal(HttpStatusCode.Forbidden, detailsByB.StatusCode);

        var resetByB = await parentB.PostAsync($"/Parent/ResetChildPassword/{profileId}", new FormUrlEncodedContent(new Dictionary<string, string>()));
        Assert.Equal(HttpStatusCode.Forbidden, resetByB.StatusCode);

        // Parent B's dashboard does not show Parent A's child
        var indexByB = await (await parentB.GetAsync("/Parent/Index")).Content.ReadAsStringAsync();
        Assert.DoesNotContain("Owned Child", indexByB);

        // Child logs in and changes password, then tries to access Parent routes -> Forbidden
        var childClient = CreateClient(allowAutoRedirect: false);
        await childClient.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["UserNameOrEmail"] = childId,
            ["Password"] = tempPwd
        }));
        await childClient.PostAsync("/Account/ChangePassword", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["CurrentPassword"] = tempPwd,
            ["NewPassword"] = "ChildSafe99!",
            ["ConfirmNewPassword"] = "ChildSafe99!"
        }));

        var childToParentIndex = await childClient.GetAsync("/Parent/Index");
        Assert.Equal(HttpStatusCode.Redirect, childToParentIndex.StatusCode);
        Assert.Contains("/Account/AccessDenied", childToParentIndex.Headers.Location?.ToString());

        var childToAddChild = await childClient.GetAsync("/Parent/AddChild");
        Assert.Equal(HttpStatusCode.Redirect, childToAddChild.StatusCode);
        Assert.Contains("/Account/AccessDenied", childToAddChild.Headers.Location?.ToString());

        var childToReset = await childClient.PostAsync($"/Parent/ResetChildPassword/{profileId}", new FormUrlEncodedContent(new Dictionary<string, string>()));
        Assert.Equal(HttpStatusCode.Redirect, childToReset.StatusCode);
        Assert.Contains("/Account/AccessDenied", childToReset.Headers.Location?.ToString());

        // Unauthenticated user redirected to Login
        var anonClient = CreateClient(allowAutoRedirect: false);
        var anonRes = await anonClient.GetAsync("/Parent/AddChild");
        Assert.Equal(HttpStatusCode.Redirect, anonRes.StatusCode);
        Assert.Contains("/Account/Login", anonRes.Headers.Location?.ToString());
    }
}
