using System.Net;
using System.Text.RegularExpressions;
using DotNet_Project.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace DotNet_Project.Tests;

[Collection("WebApp")]
public class UiNavigationTests
{
    private readonly TestWebApplicationFactory _factory;

    public UiNavigationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClient(bool allowAutoRedirect = true) =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = allowAutoRedirect,
            HandleCookies = true
        });

    [Fact]
    public async Task LoggedOut_HomePage_ShowsGuestNavigation_AndNoFloatingBuddy()
    {
        var client = CreateClient();
        var html = await (await client.GetAsync("/")).Content.ReadAsStringAsync();

        Assert.Contains("Get started", html);
        Assert.Contains("Log in", html);
        Assert.DoesNotContain("Log out", html);
        Assert.DoesNotContain("Child Dashboard", html);
        Assert.DoesNotContain("Parent Dashboard", html);
        Assert.DoesNotContain("floatingBuddyTrigger", html);
    }

    [Fact]
    public async Task RoleAwareNavigation_AndAssessmentSubmitted_WorkCorrectly()
    {
        // Register Parent
        var parentClient = CreateClient();
        var parentEmail = $"navparent_{Guid.NewGuid():N}@example.com";
        await parentClient.PostAsync("/Account/Register", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["FullName"] = "Nav Parent",
            ["Email"] = parentEmail,
            ["Password"] = "ParentPass1!",
            ["ConfirmPassword"] = "ParentPass1!"
        }));

        // Parent Home page
        var parentHome = await (await parentClient.GetAsync("/")).Content.ReadAsStringAsync();
        Assert.Contains("Go to Parent Dashboard", parentHome);
        Assert.Contains("Parent Dashboard", parentHome);
        Assert.Contains("Buddy Conversations", parentHome);
        Assert.Contains("Assessment History", parentHome);
        Assert.Contains("floatingBuddyTrigger", parentHome);
        Assert.DoesNotContain("floatingBuddyForm", parentHome);
        Assert.DoesNotContain("Get started", parentHome);

        // Create Child
        var addRes = await parentClient.PostAsync("/Parent/AddChild", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["ChildName"] = "Nav Child",
            ["DateOfBirth"] = "2015-03-10"
        }));
        var credHtml = await addRes.Content.ReadAsStringAsync();
        var childId = Regex.Match(credHtml, @"id=""credChildId""[^>]*>([^<]+)<").Groups[1].Value.Trim();
        var tempPwd = WebUtility.HtmlDecode(Regex.Match(credHtml, @"id=""credTempPassword""[^>]*>([^<]+)<").Groups[1].Value.Trim());

        // Child Login + Password Change
        var childClient = CreateClient();
        await childClient.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["UserNameOrEmail"] = childId,
            ["Password"] = tempPwd
        }));
        await childClient.PostAsync("/Account/ChangePassword", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["CurrentPassword"] = tempPwd,
            ["NewPassword"] = "ChildNav1!",
            ["ConfirmNewPassword"] = "ChildNav1!"
        }));

        // Child Home page
        var childHome = await (await childClient.GetAsync("/")).Content.ReadAsStringAsync();
        Assert.Contains("Go to Child Dashboard", childHome);
        Assert.Contains("Child Dashboard", childHome);
        Assert.Contains("floatingBuddyTrigger", childHome);
        Assert.Contains("floatingBuddyForm", childHome);
        Assert.DoesNotContain("Parent Dashboard", childHome);
        Assert.DoesNotContain("Get started", childHome);

        // Assessment Submitted page
        var submittedHtml = await (await childClient.GetAsync("/Assessment/Submitted")).Content.ReadAsStringAsync();
        Assert.Contains("Back to Child Dashboard", submittedHtml);
        Assert.DoesNotContain("Return home", submittedHtml);
    }
}
