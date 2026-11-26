using System.Net;
using System.Text.RegularExpressions;
using DotNet_Project.Data;
using DotNet_Project.DTOs;
using DotNet_Project.Models;
using DotNet_Project.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNet_Project.Tests;

public class WeeklyInsightTests
{
    [Fact]
    public void Validator_ValidatesStructuredResponse_AndRejectsMalformedOrMissingSummary()
    {
        var validJson = """
        {
          "summary": "Mood check-ins were varied this week.",
          "observations": ["Three mood entries were recorded."],
          "areasForSupport": ["Offer space to chat about schoolwork."],
          "recommendations": [
            { "activity": "5-4-3-2-1 Grounding", "reason": "An optional calming activity." }
          ],
          "dataLimitations": ["Only three days had entries."]
        }
        """;

        var ok = WeeklyInsightValidator.TryParseAndValidate(validJson, out var result, out var err);
        Assert.True(ok, err);
        Assert.NotNull(result);
        Assert.Single(result!.Recommendations);
        Assert.Equal("/Child/Grounding", result.Recommendations[0].ActivityUrl);

        Assert.False(WeeklyInsightValidator.TryParseAndValidate("{bad json", out _, out _));
        Assert.False(WeeklyInsightValidator.TryParseAndValidate("""{"summary":"  ","observations":["x"]}""", out _, out _));
    }

    [Fact]
    public async Task Aggregation_RespectsAsiaKolkataWeekBoundaries_AndChildIsolation()
    {
        var (db, conn, parent, _, childA) = await TestDbContextFactory.CreateSeededTestContextAsync();
        using var _ = conn;
        using var __ = db;

        var childBUser = new ApplicationUser { Id = "child-b-wk", UserName = "cb_wk@test.com", Email = "cb_wk@test.com", FullName = "Child B" };
        db.Users.Add(childBUser);
        var childB = new ChildProfile
        {
            ChildUserId = childBUser.Id,
            ParentUserId = parent.Id,
            ChildName = "Child B",
            DateOfBirth = new DateOnly(2016, 1, 1)
        };
        db.ChildProfiles.Add(childB);
        await db.SaveChangesAsync();

        // Monday 2026-11-23 00:15 IST == Sunday 2026-11-22 18:45 UTC (belongs to week starting 2026-11-23 in Asia/Kolkata!)
        var istStartInUtc = new DateTime(2026, 11, 22, 18, 45, 0, DateTimeKind.Utc);
        // Sunday 2026-11-22 23:45 IST == Sunday 2026-11-22 18:15 UTC (belongs to PREVIOUS week starting 2026-11-16!)
        var prevWeekInUtc = new DateTime(2026, 11, 22, 18, 15, 0, DateTimeKind.Utc);

        db.MoodEntries.AddRange(
            new MoodEntry { ChildProfileId = childA.Id, Mood = "Great", CreatedAt = istStartInUtc },
            new MoodEntry { ChildProfileId = childA.Id, Mood = "Low", CreatedAt = prevWeekInUtc },
            new MoodEntry { ChildProfileId = childB.Id, Mood = "Okay", CreatedAt = istStartInUtc }
        );
        await db.SaveChangesAsync();

        var service = new WeeklyInsightService(db, new StubWeeklyGemini(), NullLogger<WeeklyInsightService>.Instance);
        var aggA = await service.AggregateWeekAsync(childA.Id, new DateOnly(2026, 11, 23), new DateTime(2026, 11, 25, 10, 0, 0, DateTimeKind.Utc));

        Assert.Equal(new DateOnly(2026, 11, 23), aggA.WeekStartDate);
        Assert.Equal(new DateOnly(2026, 11, 29), aggA.WeekEndDate);
        Assert.True(aggA.IsPartialWeek);
        Assert.Equal(1, aggA.TotalMoodCheckIns);
        Assert.True(aggA.MoodCounts.ContainsKey("Great"));
        Assert.False(aggA.MoodCounts.ContainsKey("Low"));
        Assert.False(aggA.MoodCounts.ContainsKey("Okay"));
    }

    [Fact]
    public async Task DataSufficiency_AndPersistence_EnforcesMinimumRecords_AndPreventsDuplicates()
    {
        var (db, conn, parent, _, childA) = await TestDbContextFactory.CreateSeededTestContextAsync();
        using var _ = conn;
        using var __ = db;

        var stubGemini = new StubWeeklyGemini();
        var service = new WeeklyInsightService(db, stubGemini, NullLogger<WeeklyInsightService>.Instance);
        var weekStart = new DateOnly(2026, 11, 16);
        var nowAfterWeek = new DateTime(2026, 11, 25, 12, 0, 0, DateTimeKind.Utc);

        // 0 records -> Empty state, Gemini NOT called
        var emptyVm = await service.GetOrGenerateWeeklyInsightAsync(parent.Id, childA.Id, weekStart, utcNowOverride: nowAfterWeek);
        Assert.NotNull(emptyVm);
        Assert.False(emptyVm!.HasData);
        Assert.Equal(0, stubGemini.Calls);

        // 2 records (< 3) -> Insufficient data, Gemini NOT called
        db.MoodEntries.AddRange(
            new MoodEntry { ChildProfileId = childA.Id, Mood = "Good", CreatedAt = new DateTime(2026, 11, 17, 10, 0, 0, DateTimeKind.Utc) },
            new MoodEntry { ChildProfileId = childA.Id, Mood = "Okay", CreatedAt = new DateTime(2026, 11, 18, 10, 0, 0, DateTimeKind.Utc) }
        );
        await db.SaveChangesAsync();

        var limitedVm = await service.GetOrGenerateWeeklyInsightAsync(parent.Id, childA.Id, weekStart, utcNowOverride: nowAfterWeek);
        Assert.NotNull(limitedVm);
        Assert.True(limitedVm!.HasData);
        Assert.False(limitedVm.HasSufficientData);
        Assert.Contains("insufficient data", limitedVm.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, stubGemini.Calls);

        // 3rd record (>= 3) -> Gemini called and persisted, regeneration updates existing row without duplicates
        db.MoodEntries.Add(new MoodEntry { ChildProfileId = childA.Id, Mood = "Great", CreatedAt = new DateTime(2026, 11, 19, 10, 0, 0, DateTimeKind.Utc) });
        await db.SaveChangesAsync();

        stubGemini.NextResult = new WeeklyInsightAiResult
        {
            Summary = "Varied moods across three check-ins.",
            Observations = ["Moods included Good, Okay, and Great."],
            AreasForSupport = ["Continue gentle daily check-ins."],
            Recommendations = [new WeeklyRecommendationItem { Activity = "Box Breathing", Reason = "Optional break.", ActivityUrl = "/Child/Breathing" }],
            DataLimitations = []
        };

        var fullVm = await service.GetOrGenerateWeeklyInsightAsync(parent.Id, childA.Id, weekStart, forceRegenerate: true, utcNowOverride: nowAfterWeek);
        Assert.NotNull(fullVm);
        Assert.True(fullVm!.HasSufficientData);
        Assert.Equal("Varied moods across three check-ins.", fullVm.Summary);
        Assert.Equal(1, stubGemini.Calls);
        Assert.Equal(1, await db.WeeklyInsights.CountAsync(w => w.ChildProfileId == childA.Id && w.WeekStartDate == weekStart));

        // Regenerate again -> still 1 row in DB
        stubGemini.NextResult = null; // Even if Gemini fails, deterministic fallback updates cleanly
        var regenVm = await service.GetOrGenerateWeeklyInsightAsync(parent.Id, childA.Id, weekStart, forceRegenerate: true, utcNowOverride: nowAfterWeek);
        Assert.NotNull(regenVm);
        Assert.Equal(1, await db.WeeklyInsights.CountAsync(w => w.ChildProfileId == childA.Id && w.WeekStartDate == weekStart));
    }

    private sealed class StubWeeklyGemini : IGeminiAiService
    {
        public int Calls { get; private set; }
        public WeeklyInsightAiResult? NextResult { get; set; }

        public Task<List<AssessmentQuestionItem>> GenerateQuestionsAsync(ChildProfile childProfile) => Task.FromResult(new List<AssessmentQuestionItem>());
        public Task<AIReport> GenerateReportAsync(Assessment assessment, ChildProfile childProfile, List<AssessmentAnswer> answers) => Task.FromResult(new AIReport());
        public Task<string> GenerateBuddyReplyAsync(ChildProfile childProfile, string userMessage, List<ChatMessage> conversationHistory) => Task.FromResult("Hello");
        public Task<string> GenerateParentBuddyReplyAsync(ApplicationUser parentUser, string userMessage, List<ParentChatMessage> conversationHistory) => Task.FromResult("Hello parent");
        public Task<JournalSentimentResult?> AnalyzeJournalSentimentAsync(string journalText, CancellationToken cancellationToken = default) => Task.FromResult<JournalSentimentResult?>(null);

        public Task<WeeklyInsightAiResult?> GenerateWeeklyInsightAsync(WeeklyAggregationResult aggregation, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(NextResult);
        }
    }
}

[Collection("WebApp")]
public class WeeklyInsightAuthorizationTests
{
    private readonly TestWebApplicationFactory _factory;

    public WeeklyInsightAuthorizationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task WeeklyInsights_EnforcesParentOwnership_AndRejectsOtherParentsAndChildren()
    {
        var parentA = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = true, HandleCookies = true });
        var parentB = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var childClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

        await parentA.PostAsync("/Account/Register", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["FullName"] = "Parent A Wk",
            ["Email"] = $"wka_{Guid.NewGuid():N}@example.com",
            ["Password"] = "ParentPass1!",
            ["ConfirmPassword"] = "ParentPass1!"
        }));
        await parentB.PostAsync("/Account/Register", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["FullName"] = "Parent B Wk",
            ["Email"] = $"wkb_{Guid.NewGuid():N}@example.com",
            ["Password"] = "ParentPass1!",
            ["ConfirmPassword"] = "ParentPass1!"
        }));

        var addRes = await parentA.PostAsync("/Parent/AddChild", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["ChildName"] = "Weekly Child",
            ["DateOfBirth"] = "2015-03-03"
        }));
        var credHtml = await addRes.Content.ReadAsStringAsync();
        var childIdStr = Regex.Match(credHtml, @"id=""credChildId""[^>]*>([^<]+)<").Groups[1].Value.Trim();
        var tempPwd = WebUtility.HtmlDecode(Regex.Match(credHtml, @"id=""credTempPassword""[^>]*>([^<]+)<").Groups[1].Value.Trim());

        int childProfileId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            childProfileId = (await db.ChildProfiles.FirstAsync(c => c.ChildUser.UserName == childIdStr)).Id;
        }

        // Parent A can view WeeklyInsights
        var okRes = await parentA.GetAsync($"/Parent/WeeklyInsights?childId={childProfileId}");
        Assert.Equal(HttpStatusCode.OK, okRes.StatusCode);
        var okHtml = await okRes.Content.ReadAsStringAsync();
        Assert.Contains("Weekly Child", okHtml);

        // Parent B cannot view or regenerate Parent A's child's WeeklyInsights
        var bGet = await parentB.GetAsync($"/Parent/WeeklyInsights?childId={childProfileId}");
        Assert.True(bGet.StatusCode == HttpStatusCode.Forbidden || bGet.StatusCode == HttpStatusCode.Redirect);

        var bPost = await parentB.PostAsync("/Parent/GenerateWeeklyInsight", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["childId"] = childProfileId.ToString()
        }));
        Assert.True(bPost.StatusCode == HttpStatusCode.Forbidden || bPost.StatusCode == HttpStatusCode.Redirect);

        // Child cannot access Parent WeeklyInsights
        await childClient.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["UserNameOrEmail"] = childIdStr,
            ["Password"] = tempPwd
        }));
        await childClient.PostAsync("/Account/ChangePassword", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["CurrentPassword"] = tempPwd,
            ["NewPassword"] = "ChildSafe1!",
            ["ConfirmNewPassword"] = "ChildSafe1!"
        }));
        var childToWeekly = await childClient.GetAsync($"/Parent/WeeklyInsights?childId={childProfileId}");
        Assert.Equal(HttpStatusCode.Redirect, childToWeekly.StatusCode);
        Assert.Contains("/Account/AccessDenied", childToWeekly.Headers.Location?.ToString());
    }
}
