using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using DotNet_Project.Data;
using DotNet_Project.DTOs;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DotNet_Project.Tests;

[Collection("WebApp")]
public class RelaxationCornerTests
{
    private readonly TestWebApplicationFactory _factory;

    public RelaxationCornerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClient(bool allowAutoRedirect = false) =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = allowAutoRedirect,
            HandleCookies = true
        });

    private async Task<(HttpClient Parent, HttpClient Child, int ChildProfileId)> CreateParentAndChildAsync()
    {
        var parentClient = CreateClient(allowAutoRedirect: true);
        await parentClient.PostAsync("/Account/Register", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["FullName"] = "Relax Parent",
            ["Email"] = $"relax_p_{Guid.NewGuid():N}@example.com",
            ["Password"] = "ParentPass1!",
            ["ConfirmPassword"] = "ParentPass1!"
        }));

        var addRes = await parentClient.PostAsync("/Parent/AddChild", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["ChildName"] = "Relax Child",
            ["DateOfBirth"] = "2015-04-15"
        }));
        var credHtml = await addRes.Content.ReadAsStringAsync();
        var childId = Regex.Match(credHtml, @"id=""credChildId""[^>]*>([^<]+)<").Groups[1].Value.Trim();
        var tempPwd = WebUtility.HtmlDecode(Regex.Match(credHtml, @"id=""credTempPassword""[^>]*>([^<]+)<").Groups[1].Value.Trim());

        int profileId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            profileId = (await db.ChildProfiles.FirstAsync(c => c.ChildUser.UserName == childId)).Id;
        }

        var childClient = CreateClient(allowAutoRedirect: true);
        await childClient.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["UserNameOrEmail"] = childId,
            ["Password"] = tempPwd
        }));
        await childClient.PostAsync("/Account/ChangePassword", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["CurrentPassword"] = tempPwd,
            ["NewPassword"] = "RelaxChild1!",
            ["ConfirmNewPassword"] = "RelaxChild1!"
        }));

        return (parentClient, childClient, profileId);
    }

    [Fact]
    public async Task RelaxationCorner_Pages_EnforceChildAuthorization()
    {
        var (parentClient, childClient, _) = await CreateParentAndChildAsync();

        // Child can access all Relaxation Corner pages
        foreach (var route in new[] { "/Child/Relaxation", "/Child/Breathing", "/Child/Grounding", "/Child/Affirmations" })
        {
            var res = await childClient.GetAsync(route);
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        }

        // Parent cannot access child-only Relaxation Corner pages
        var parentNoRedirect = CreateClient(allowAutoRedirect: false);
        // Copy cookie by logging in as a parent without auto-redirect
        await parentNoRedirect.PostAsync("/Account/Register", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["FullName"] = "Parent Blocked",
            ["Email"] = $"relax_pb_{Guid.NewGuid():N}@example.com",
            ["Password"] = "ParentPass1!",
            ["ConfirmPassword"] = "ParentPass1!"
        }));
        var parentToRelax = await parentNoRedirect.GetAsync("/Child/Relaxation");
        Assert.Equal(HttpStatusCode.Redirect, parentToRelax.StatusCode);
        Assert.Contains("/Account/AccessDenied", parentToRelax.Headers.Location?.ToString());

        // Unauthenticated users are rejected
        var anon = CreateClient(allowAutoRedirect: false);
        var anonToRelax = await anon.GetAsync("/Child/Relaxation");
        Assert.Equal(HttpStatusCode.Redirect, anonToRelax.StatusCode);
        Assert.Contains("/Account/Login", anonToRelax.Headers.Location?.ToString());

        var anonApi = await anon.PostAsJsonAsync("/api/relaxation/complete", new RecordExerciseSessionRequest
        {
            ExerciseType = "Breathing",
            DurationSeconds = 60
        });
        Assert.Equal(HttpStatusCode.Unauthorized, anonApi.StatusCode);
    }

    [Fact]
    public async Task RelaxationApi_SavesValidSession_RejectsInvalidInput_AndIsolatesChildren()
    {
        var (parentClient, child1Client, child1Id) = await CreateParentAndChildAsync();
        var (_, child2Client, child2Id) = await CreateParentAndChildAsync();

        // Valid session saved to Child 1
        var okRes = await child1Client.PostAsJsonAsync("/api/relaxation/complete", new RecordExerciseSessionRequest
        {
            ExerciseType = "Breathing",
            DurationSeconds = 60,
            WasCompleted = true
        });
        Assert.Equal(HttpStatusCode.Created, okRes.StatusCode);
        var saved = await okRes.Content.ReadFromJsonAsync<ExerciseSessionDto>();
        Assert.NotNull(saved);
        Assert.Equal("Breathing", saved!.ExerciseType);
        Assert.Equal(60, saved.DurationSeconds);

        // Invalid activity type rejected
        var badTypeRes = await child1Client.PostAsJsonAsync("/api/relaxation/complete", new RecordExerciseSessionRequest
        {
            ExerciseType = "InvalidType",
            DurationSeconds = 60
        });
        Assert.Equal(HttpStatusCode.BadRequest, badTypeRes.StatusCode);

        // Invalid duration rejected
        var badDurationRes = await child1Client.PostAsJsonAsync("/api/relaxation/complete", new RecordExerciseSessionRequest
        {
            ExerciseType = "Grounding",
            DurationSeconds = 99999
        });
        Assert.Equal(HttpStatusCode.BadRequest, badDurationRes.StatusCode);

        // Child 2's recent sessions do not include Child 1's session
        var child2Recent = await child2Client.GetFromJsonAsync<List<ExerciseSessionDto>>("/api/relaxation/recent");
        Assert.NotNull(child2Recent);
        Assert.Empty(child2Recent!);

        // Parent's ChildDetails for Child 1 displays Breathing activity
        var detailsHtml = await (await parentClient.GetAsync($"/Parent/ChildDetails/{child1Id}")).Content.ReadAsStringAsync();
        Assert.Contains("Breathing", detailsHtml);
    }

    [Fact]
    public async Task RelaxationAssets_ContainReducedMotion_SingleTimerGuard_GroundingAndAffirmations()
    {
        var (_, childClient, _) = await CreateParentAndChildAsync();

        var breathingHtml = await (await childClient.GetAsync("/Child/Breathing")).Content.ReadAsStringAsync();
        Assert.Contains("Box Breathing", breathingHtml);
        Assert.Contains("Gentle Breathing (No holds)", breathingHtml);

        var groundingHtml = await (await childClient.GetAsync("/Child/Grounding")).Content.ReadAsStringAsync();
        Assert.Contains("5-4-3-2-1 Grounding", groundingHtml);
        Assert.Contains("Skip this step", groundingHtml);

        var affirmationsHtml = await (await childClient.GetAsync("/Child/Affirmations")).Content.ReadAsStringAsync();
        Assert.Contains("I am learning something new every day.", affirmationsHtml);
        Assert.Contains("Shuffle", affirmationsHtml);
        Assert.Contains("Favourite", affirmationsHtml);

        var cssRes = await (await childClient.GetAsync("/css/relaxation.css")).Content.ReadAsStringAsync();
        Assert.Contains("prefers-reduced-motion: reduce", cssRes);

        var jsRes = await (await childClient.GetAsync("/js/relaxation.js")).Content.ReadAsStringAsync();
        Assert.Contains("if (isRunning) return;", jsRes);
        Assert.Contains("It is okay to make mistakes.", jsRes);
        Assert.Contains("I deserve kindness and respect.", jsRes);
    }
}
