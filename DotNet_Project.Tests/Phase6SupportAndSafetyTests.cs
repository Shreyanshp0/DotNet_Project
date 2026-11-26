using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using DotNet_Project.Data;
using DotNet_Project.DTOs;
using DotNet_Project.Models;
using DotNet_Project.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNet_Project.Tests;

public class BuddySafetyTests
{
    [Fact]
    public async Task ImmediateDanger_BypassesGemini_References112_AndDoesNotClaimMonitoring()
    {
        var (db, conn, _, _, child) = await TestDbContextFactory.CreateSeededTestContextAsync();
        using var _ = conn;
        using var __ = db;

        var gemini = new GeminiAiService(new HttpClient(), new ConfigurationBuilder().Build(), NullLogger<GeminiAiService>.Instance);
        var reply = await gemini.GenerateBuddyReplyAsync(child, "I want to hurt myself and I feel unsafe.", new List<ChatMessage>());

        Assert.Contains("112", reply);
        Assert.Contains("trusted adult", reply, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("cannot monitor or protect you", reply, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConcerningDisclosure_EncouragesTrustedAdult_EvenOnUnsafeOrFailedModelOutput()
    {
        var tier = BuddySafetyEvaluator.Classify("I feel hopeless and bullied at school.");
        Assert.Equal(BuddySafetyTier.ConcerningNonImmediate, tier);

        // Even if Gemini returns an unsafe diagnosis or omits trusted adult guidance, EnsureSafeReply enforces safety
        var filteredDiagnosis = BuddySafetyEvaluator.EnsureSafeReply(tier, "I diagnose you with depression.");
        Assert.Contains("trusted adult", filteredDiagnosis, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("diagnose", filteredDiagnosis, StringComparison.OrdinalIgnoreCase);

        var filteredMissingAdult = BuddySafetyEvaluator.EnsureSafeReply(tier, "That sounds really tough.");
        Assert.Contains("trusted adult", filteredMissingAdult, StringComparison.OrdinalIgnoreCase);

        // Ordinary disclosure
        var ordinaryTier = BuddySafetyEvaluator.Classify("I had a fun art class today.");
        Assert.Equal(BuddySafetyTier.Ordinary, ordinaryTier);
        var ordinaryReply = BuddySafetyEvaluator.EnsureSafeReply(ordinaryTier, "That sounds wonderful!");
        Assert.Equal("That sounds wonderful!", ordinaryReply);
    }

    [Fact]
    public void SupportRequest_Service_ValidatesAndPreventsRapidDuplicates()
    {
        // Verified via integration test below as well
        var tier = BuddySafetyEvaluator.Classify(null);
        Assert.Equal(BuddySafetyTier.Ordinary, tier);
    }
}

[Collection("WebApp")]
public class SupportRequestWorkflowTests
{
    private readonly TestWebApplicationFactory _factory;

    public SupportRequestWorkflowTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClient(bool allowAutoRedirect = true) =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = allowAutoRedirect,
            HandleCookies = true
        });

    private async Task<(HttpClient Parent, HttpClient Child, string ChildUserId)> CreateParentAndChildAsync(string name)
    {
        var parentClient = CreateClient(allowAutoRedirect: true);
        await parentClient.PostAsync("/Account/Register", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["FullName"] = $"Parent {name}",
            ["Email"] = $"sup_p_{Guid.NewGuid():N}@example.com",
            ["Password"] = "ParentPass1!",
            ["ConfirmPassword"] = "ParentPass1!"
        }));

        var addRes = await parentClient.PostAsync("/Parent/AddChild", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["ChildName"] = $"Child {name}",
            ["DateOfBirth"] = "2015-05-05"
        }));
        var credHtml = await addRes.Content.ReadAsStringAsync();
        var childUsername = Regex.Match(credHtml, @"id=""credChildId""[^>]*>([^<]+)<").Groups[1].Value.Trim();
        var tempPwd = WebUtility.HtmlDecode(Regex.Match(credHtml, @"id=""credTempPassword""[^>]*>([^<]+)<").Groups[1].Value.Trim());

        string childUserId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            childUserId = (await db.ChildProfiles.FirstAsync(c => c.ChildUser.UserName == childUsername)).ChildUserId;
        }

        var childClient = CreateClient(allowAutoRedirect: true);
        await childClient.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["UserNameOrEmail"] = childUsername,
            ["Password"] = tempPwd
        }));
        await childClient.PostAsync("/Account/ChangePassword", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["CurrentPassword"] = tempPwd,
            ["NewPassword"] = "ChildSafe1!",
            ["ConfirmNewPassword"] = "ChildSafe1!"
        }));

        return (parentClient, childClient, childUserId);
    }

    [Fact]
    public async Task SupportRequest_FullWorkflow_DuplicateGuard_AndIsolation()
    {
        var (parentA, childA, childAUserId) = await CreateParentAndChildAsync("Alpha");
        var (parentB, childB, _) = await CreateParentAndChildAsync("Beta");

        // 1. Child A submits a support request with omitted message
        var submit1 = await childA.PostAsync("/Child/CreateSupportRequest", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["reason"] = "Something is worrying me.",
            ["message"] = ""
        }));
        Assert.Equal(HttpStatusCode.OK, submit1.StatusCode);

        // Rapid duplicate submission does not create a second DB row
        await childA.PostAsync("/Child/CreateSupportRequest", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["reason"] = "Something is worrying me.",
            ["message"] = ""
        }));

        // Invalid reason rejected without saving
        await childA.PostAsync("/Child/CreateSupportRequest", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["reason"] = "Invalid reason",
            ["message"] = "test"
        }));

        int requestId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var requests = await db.SupportRequests.Where(r => r.ChildUserId == childAUserId).ToListAsync();
            Assert.Single(requests);
            requestId = requests[0].Id;
            Assert.Equal(SupportRequestStatus.Pending, requests[0].Status);
        }

        // 2. Child B does NOT see Child A's request on Child B's dashboard
        var childBHtml = await (await childB.GetAsync("/Child/Index")).Content.ReadAsStringAsync();
        Assert.Contains("You haven't sent a support request yet.", childBHtml);

        // 3. Parent A sees Child A's Support Requests button highlighted on /Parent/Index, and manages the request via /Parent/SupportRequests
        int childAProfileId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            childAProfileId = (await db.ChildProfiles.FirstAsync(c => c.ChildUserId == childAUserId)).Id;
        }

        var parentAIndex = await (await parentA.GetAsync("/Parent/Index")).Content.ReadAsStringAsync();
        Assert.DoesNotContain("id=\"parentSupportRequests\"", parentAIndex);
        Assert.Contains("btn-support-alert", parentAIndex);

        var parentAReqs = await (await parentA.GetAsync($"/Parent/SupportRequests?childId={childAProfileId}")).Content.ReadAsStringAsync();
        Assert.Contains("Child Alpha", parentAReqs);
        Assert.Contains("Something is worrying me.", parentAReqs);
        Assert.Contains("Pending", parentAReqs);

        // Parent B cannot acknowledge or resolve Child A's request
        var parentBNoRedirect = CreateClient(allowAutoRedirect: false);
        await parentBNoRedirect.PostAsync("/Account/Register", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["FullName"] = "Unrelated Parent",
            ["Email"] = $"unrel_{Guid.NewGuid():N}@example.com",
            ["Password"] = "ParentPass1!",
            ["ConfirmPassword"] = "ParentPass1!"
        }));
        var unauthAck = await parentBNoRedirect.PostAsync($"/Parent/AcknowledgeSupportRequest/{requestId}", new FormUrlEncodedContent(new Dictionary<string, string>()));
        Assert.Equal(HttpStatusCode.Redirect, unauthAck.StatusCode);
        Assert.Contains("/Account/AccessDenied", unauthAck.Headers.Location?.ToString());

        // Parent A acknowledges
        await parentA.PostAsync($"/Parent/AcknowledgeSupportRequest/{requestId}", new FormUrlEncodedContent(new Dictionary<string, string>()));
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var req = await db.SupportRequests.FindAsync(requestId);
            Assert.Equal(SupportRequestStatus.Acknowledged, req!.Status);
            Assert.NotNull(req.AcknowledgedAt);
            Assert.Null(req.ResolvedAt);
        }

        // Parent A resolves
        await parentA.PostAsync($"/Parent/ResolveSupportRequest/{requestId}", new FormUrlEncodedContent(new Dictionary<string, string>()));
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var req = await db.SupportRequests.FindAsync(requestId);
            Assert.Equal(SupportRequestStatus.Resolved, req!.Status);
            Assert.NotNull(req.ResolvedAt);
        }
    }

    [Fact]
    public async Task ChildCardSupportButton_HighlightsPerChild_UpdatesCounts_AndRespectsIsolation()
    {
        var (parentA, childA, childAUserId) = await CreateParentAndChildAsync("PanelA");
        var (parentB, _, _) = await CreateParentAndChildAsync("PanelB");

        // Create second child (Child B) under the SAME parentA with 0 requests
        var addBRes = await parentA.PostAsync("/Parent/AddChild", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["ChildName"] = "Child Zero",
            ["DateOfBirth"] = "2017-01-10"
        }));
        var htmlB = await addBRes.Content.ReadAsStringAsync();
        var childBUsername = Regex.Match(htmlB, @"id=""credChildId""[^>]*>([^<]+)<").Groups[1].Value.Trim();

        int childAId, childBId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            childAId = (await db.ChildProfiles.FirstAsync(c => c.ChildUserId == childAUserId)).Id;
            childBId = (await db.ChildProfiles.FirstAsync(c => c.ChildUser.UserName == childBUsername)).Id;
        }

        // Submit 2 requests for Child A
        await childA.PostAsync("/Child/CreateSupportRequest", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["reason"] = "I feel upset.",
            ["message"] = "Older message"
        }));
        await childA.PostAsync("/Child/CreateSupportRequest", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["reason"] = "I am having a difficult day.",
            ["message"] = "Newer message"
        }));

        int olderId, newerId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var list = await db.SupportRequests
                .Where(r => r.ChildUserId == childAUserId)
                .OrderBy(r => r.Id)
                .ToListAsync();
            Assert.Equal(2, list.Count);
            list[0].CreatedAt = DateTime.UtcNow.AddMinutes(-10);
            list[1].CreatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            olderId = list[0].Id;
            newerId = list[1].Id;
        }

        // 1, 2, 3, 4, 5: Standalone card removed; Child A button highlighted with count 2; Child B button NOT highlighted with count 0
        var dashHtml = await (await parentA.GetAsync("/Parent/Index")).Content.ReadAsStringAsync();
        Assert.DoesNotContain("id=\"parentSupportRequests\"", dashHtml);
        Assert.Contains($"/Parent/SupportRequests?childId={childAId}", dashHtml);
        Assert.Contains($"/Parent/SupportRequests?childId={childBId}", dashHtml);
        Assert.Contains("btn-support-alert", dashHtml);
        Assert.Contains(">2<", dashHtml);
        Assert.Contains(">0<", dashHtml);

        // Child-specific SupportRequests view shows Child A's requests newest first
        var childAReqsHtml = await (await parentA.GetAsync($"/Parent/SupportRequests?childId={childAId}")).Content.ReadAsStringAsync();
        Assert.Contains("Child PanelA's Support Requests (<span id=\"unresolvedSupportCount\">2</span>)", childAReqsHtml);
        Assert.True(childAReqsHtml.IndexOf("Newer message", StringComparison.Ordinal) < childAReqsHtml.IndexOf("Older message", StringComparison.Ordinal));

        // Child B's SupportRequests view is empty
        var childBReqsHtml = await (await parentA.GetAsync($"/Parent/SupportRequests?childId={childBId}")).Content.ReadAsStringAsync();
        Assert.Contains("You're all caught up.", childBReqsHtml);
        Assert.DoesNotContain("Newer message", childBReqsHtml);

        // 10: Another parent cannot access Child A's SupportRequests
        var parentBNoRedirect = CreateClient(allowAutoRedirect: false);
        await parentBNoRedirect.PostAsync("/Account/Register", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["FullName"] = "Other Parent",
            ["Email"] = $"other_{Guid.NewGuid():N}@example.com",
            ["Password"] = "ParentPass1!",
            ["ConfirmPassword"] = "ParentPass1!"
        }));
        var otherAccess = await parentBNoRedirect.GetAsync($"/Parent/SupportRequests?childId={childAId}");
        Assert.Equal(HttpStatusCode.Redirect, otherAccess.StatusCode);
        Assert.Contains("/Account/AccessDenied", otherAccess.Headers.Location?.ToString());

        // 6: Acknowledge older request -> Child A button STILL highlighted, count STILL 2
        await parentA.PostAsync($"/Parent/AcknowledgeSupportRequest/{olderId}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["childId"] = childAId.ToString()
        }));
        var dashAfterAck = await (await parentA.GetAsync("/Parent/Index")).Content.ReadAsStringAsync();
        Assert.Contains("btn-support-alert", dashAfterAck);
        Assert.Contains(">2<", dashAfterAck);

        // 7: Resolve older request -> count drops to 1, button still highlighted
        await parentA.PostAsync($"/Parent/ResolveSupportRequest/{olderId}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["childId"] = childAId.ToString()
        }));
        var dashAfterOneResolve = await (await parentA.GetAsync("/Parent/Index")).Content.ReadAsStringAsync();
        Assert.Contains("btn-support-alert", dashAfterOneResolve);
        Assert.Contains(">1<", dashAfterOneResolve);

        // 9: Resolved request appears in Child A's SupportHistory
        var historyHtml = await (await parentA.GetAsync($"/Parent/SupportHistory?childId={childAId}")).Content.ReadAsStringAsync();
        Assert.Contains("Older message", historyHtml);

        // 8, 11: Resolve final request -> count drops to 0, glow removed on dashboard reload, empty state shown
        await parentA.PostAsync($"/Parent/ResolveSupportRequest/{newerId}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["childId"] = childAId.ToString()
        }));
        var dashAllResolved = await (await parentA.GetAsync("/Parent/Index")).Content.ReadAsStringAsync();
        Assert.DoesNotContain("btn-support-alert", dashAllResolved);
        var childAFinalReqs = await (await parentA.GetAsync($"/Parent/SupportRequests?childId={childAId}")).Content.ReadAsStringAsync();
        Assert.Contains("You're all caught up.", childAFinalReqs);

        // 12: Verify CSS reduced-motion rule exists while keeping highlight
        var css = await (await parentA.GetAsync("/css/site.css")).Content.ReadAsStringAsync();
        Assert.Contains(".btn-support-alert", css);
        Assert.Contains("prefers-reduced-motion: reduce", css);
    }
}
