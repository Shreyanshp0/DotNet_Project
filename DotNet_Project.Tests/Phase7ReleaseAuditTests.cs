using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using DotNet_Project.Data;
using DotNet_Project.DTOs;
using DotNet_Project.Models;
using DotNet_Project.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DotNet_Project.Tests;

[Collection("WebApp")]
public class Phase7ReleaseAuditTests
{
    private readonly TestWebApplicationFactory _factory;

    public Phase7ReleaseAuditTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClient(bool allowAutoRedirect = true) =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = allowAutoRedirect,
            HandleCookies = true
        });

    private async Task<(HttpClient Client, string Username, string Password, int ProfileId)> CreateChildAsync(
        HttpClient parentClient,
        string name,
        string dob)
    {
        var addRes = await parentClient.PostAsync("/Parent/AddChild", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["ChildName"] = name,
            ["DateOfBirth"] = dob
        }));
        var html = await addRes.Content.ReadAsStringAsync();
        var username = Regex.Match(html, @"id=""credChildId""[^>]*>([^<]+)<").Groups[1].Value.Trim();
        var tempPwd = WebUtility.HtmlDecode(Regex.Match(html, @"id=""credTempPassword""[^>]*>([^<]+)<").Groups[1].Value.Trim());

        int profileId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            profileId = (await db.ChildProfiles.FirstAsync(c => c.ChildUser.UserName == username)).Id;
        }

        var childClient = CreateClient(allowAutoRedirect: true);
        await childClient.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["UserNameOrEmail"] = username,
            ["Password"] = tempPwd
        }));
        var newPwd = "AuditSafe1!";
        await childClient.PostAsync("/Account/ChangePassword", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["CurrentPassword"] = tempPwd,
            ["NewPassword"] = newPwd,
            ["ConfirmNewPassword"] = newPwd
        }));

        return (childClient, username, newPwd, profileId);
    }

    [Fact]
    public async Task FullCrossChildAndCrossParentIsolation_AcrossAll8Domains()
    {
        var parent1 = CreateClient(allowAutoRedirect: true);
        var parent2 = CreateClient(allowAutoRedirect: false);

        await parent1.PostAsync("/Account/Register", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["FullName"] = "Audit Parent 1",
            ["Email"] = $"aud_p1_{Guid.NewGuid():N}@example.com",
            ["Password"] = "ParentPass1!",
            ["ConfirmPassword"] = "ParentPass1!"
        }));
        await parent2.PostAsync("/Account/Register", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["FullName"] = "Audit Parent 2",
            ["Email"] = $"aud_p2_{Guid.NewGuid():N}@example.com",
            ["Password"] = "ParentPass1!",
            ["ConfirmPassword"] = "ParentPass1!"
        }));

        var (childA, _, _, childAId) = await CreateChildAsync(parent1, "AuditChildAlpha", "2014-04-04");
        var (childB, _, _, childBId) = await CreateChildAsync(parent1, "AuditChildBeta", "2016-06-06");

        // 1. Distinct Moods (2 moods + 1 journal = 3 emotional records per child so WeeklyInsights sufficiency >= 3 is met)
        await childA.PostAsJsonAsync("/api/mood/checkin", new MoodCheckInRequest { Mood = "Great", Reason = "AlphaUniqueMoodReason" });
        await childA.PostAsJsonAsync("/api/mood/checkin", new MoodCheckInRequest { Mood = "Good", Reason = "AlphaSecondMood" });
        await childB.PostAsJsonAsync("/api/mood/checkin", new MoodCheckInRequest { Mood = "Low", Reason = "BetaUniqueMoodReason" });
        await childB.PostAsJsonAsync("/api/mood/checkin", new MoodCheckInRequest { Mood = "Okay", Reason = "BetaSecondMood" });

        // 2. Distinct Journals
        await childA.PostAsJsonAsync("/api/journal/submit", new JournalSubmitRequest
        {
            BestPart = "AlphaUniqueBestPart",
            DifficultPart = "None",
            DayDescription = "Alpha day",
            Emotion = "Happy"
        });
        await childB.PostAsJsonAsync("/api/journal/submit", new JournalSubmitRequest
        {
            BestPart = "BetaUniqueBestPart",
            DifficultPart = "None",
            DayDescription = "Beta day",
            Emotion = "Tired"
        });

        // 3. Distinct Assessments & AI Reports
        var assessARes = await childA.PostAsJsonAsync("/api/assessment/submit", new AssessmentSubmitRequest
        {
            Answers = Enumerable.Range(1, 10).Select(i => new AssessmentAnswerItem
            {
                QuestionId = i,
                QuestionText = AssessmentQuestions.Definitions[i].Text,
                Answer = "Always"
            }).ToList()
        });
        var assessAData = await assessARes.Content.ReadFromJsonAsync<AssessmentSubmitResponse>();

        var assessBRes = await childB.PostAsJsonAsync("/api/assessment/submit", new AssessmentSubmitRequest
        {
            Answers = Enumerable.Range(1, 10).Select(i => new AssessmentAnswerItem
            {
                QuestionId = i,
                QuestionText = AssessmentQuestions.Definitions[i].Text,
                Answer = "Rarely"
            }).ToList()
        });
        var assessBData = await assessBRes.Content.ReadFromJsonAsync<AssessmentSubmitResponse>();

        // 4. Distinct Chat Histories
        await childA.PostAsJsonAsync("/Chat/SendMessage", new ChatMessageRequest { Message = "AlphaUniqueChatMsg", ConversationId = "conv-alpha" });
        await childB.PostAsJsonAsync("/Chat/SendMessage", new ChatMessageRequest { Message = "BetaUniqueChatMsg", ConversationId = "conv-beta" });

        // 5. Distinct Support Requests
        await childA.PostAsync("/Child/CreateSupportRequest", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["reason"] = "I feel upset.",
            ["message"] = "AlphaUniqueSupportMsg"
        }));
        await childB.PostAsync("/Child/CreateSupportRequest", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["reason"] = "Something is worrying me.",
            ["message"] = "BetaUniqueSupportMsg"
        }));

        // 6. Distinct Sentiment Analyses & Weekly Insights (seeded directly for deterministic verification)
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var journalA = await db.JournalEntries.FirstAsync(j => j.ChildProfileId == childAId);
            var journalB = await db.JournalEntries.FirstAsync(j => j.ChildProfileId == childBId);

            db.JournalSentimentAnalyses.AddRange(
                new JournalSentimentAnalysis { JournalEntryId = journalA.Id, Sentiment = "Positive", EmotionIndicators = "[\"Happiness\"]", AIInsight = "AlphaUniqueSentimentInsight" },
                new JournalSentimentAnalysis { JournalEntryId = journalB.Id, Sentiment = "Mixed", EmotionIndicators = "[\"Stress\"]", AIInsight = "BetaUniqueSentimentInsight" }
            );
            db.WeeklyInsights.AddRange(
                new WeeklyInsight { ChildProfileId = childAId, WeekStartDate = new DateOnly(2026, 11, 23), WeekEndDate = new DateOnly(2026, 11, 29), Summary = "AlphaUniqueWeeklySummary" },
                new WeeklyInsight { ChildProfileId = childBId, WeekStartDate = new DateOnly(2026, 11, 23), WeekEndDate = new DateOnly(2026, 11, 29), Summary = "BetaUniqueWeeklySummary" }
            );
            await db.SaveChangesAsync();
        }

        // Verify Child A APIs never leak Child B
        var childAMoods = await (await childA.GetAsync("/api/mood/today")).Content.ReadAsStringAsync();
        Assert.Contains("AlphaUniqueMoodReason", childAMoods);
        Assert.DoesNotContain("BetaUniqueMoodReason", childAMoods);

        var childAJournals = await (await childA.GetAsync("/api/journal/history")).Content.ReadAsStringAsync();
        Assert.Contains("AlphaUniqueBestPart", childAJournals);
        Assert.DoesNotContain("BetaUniqueBestPart", childAJournals);

        // Verify Parent 1 viewing Child A sees ONLY Alpha, never Beta
        var detailsA = await (await parent1.GetAsync($"/Parent/ChildDetails/{childAId}")).Content.ReadAsStringAsync();
        Assert.Contains("AlphaUniqueSentimentInsight", detailsA);
        Assert.DoesNotContain("BetaUniqueSentimentInsight", detailsA);

        var dailyA = await (await parent1.GetAsync($"/Parent/DailyActivity?childId={childAId}")).Content.ReadAsStringAsync();
        Assert.Contains("AlphaUniqueMoodReason", dailyA);
        Assert.DoesNotContain("BetaUniqueMoodReason", dailyA);

        var chatA = await (await parent1.GetAsync($"/Parent/ChatHistory?childId={childAId}")).Content.ReadAsStringAsync();
        Assert.Contains("AlphaUniqueChatMsg", chatA);
        Assert.DoesNotContain("BetaUniqueChatMsg", chatA);

        var weeklyA = await (await parent1.GetAsync($"/Parent/WeeklyInsights?childId={childAId}&weekStart=2026-11-23")).Content.ReadAsStringAsync();
        Assert.Contains("AlphaUniqueWeeklySummary", weeklyA);
        Assert.DoesNotContain("BetaUniqueWeeklySummary", weeklyA);

        var supportA = await (await parent1.GetAsync($"/Parent/SupportRequests?childId={childAId}")).Content.ReadAsStringAsync();
        Assert.Contains("AlphaUniqueSupportMsg", supportA);
        Assert.DoesNotContain("BetaUniqueSupportMsg", supportA);

        var reportA = await (await parent1.GetAsync($"/Parent/Report?childId={childAId}")).Content.ReadAsStringAsync();
        Assert.Contains($"MC-{assessAData!.AssessmentId}", reportA);
        Assert.DoesNotContain($"MC-{assessBData!.AssessmentId}", reportA);

        // Verify Parent 2 cannot access Child A across MVC and API endpoints
        Assert.Equal(HttpStatusCode.NotFound, (await parent2.GetAsync($"/api/parent/child/{childAId}/daily-activity")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await parent2.GetAsync($"/api/report/{assessAData.AssessmentId}")).StatusCode);

        var p2Details = await parent2.GetAsync($"/Parent/ChildDetails/{childAId}");
        Assert.True(p2Details.StatusCode == HttpStatusCode.Redirect || p2Details.StatusCode == HttpStatusCode.Forbidden);

        // Verify AccessDenied returns 403 Forbidden directly without a redirect loop
        var p2Followed = CreateClient(allowAutoRedirect: true);
        await p2Followed.PostAsync("/Account/Register", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["FullName"] = "Audit Parent 2b",
            ["Email"] = $"aud_p2b_{Guid.NewGuid():N}@example.com",
            ["Password"] = "ParentPass1!",
            ["ConfirmPassword"] = "ParentPass1!"
        }));
        var accessDeniedRes = await p2Followed.GetAsync($"/Parent/ChildDetails/{childAId}");
        Assert.Equal(HttpStatusCode.Forbidden, accessDeniedRes.StatusCode);
    }
}
