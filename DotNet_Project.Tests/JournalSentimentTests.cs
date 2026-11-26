using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using DotNet_Project.Data;
using DotNet_Project.DTOs;
using DotNet_Project.Models;
using DotNet_Project.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNet_Project.Tests;

public class JournalSentimentTests
{
    [Theory]
    [InlineData("Positive", new[] { "Happiness", "Gratitude" })]
    [InlineData("Neutral", new string[0])]
    [InlineData("Negative", new[] { "Sadness", "Loneliness" })]
    [InlineData("Mixed", new[] { "Excitement", "Stress" })]
    public void Validator_AcceptsValidCategoriesAndEmotions(string sentiment, string[] emotions)
    {
        var json = $$"""
        {
          "sentiment": "{{sentiment}}",
          "emotions": {{System.Text.Json.JsonSerializer.Serialize(emotions)}},
          "insight": "A concise, parent-friendly reflection on the child's journal."
        }
        """;

        var ok = JournalSentimentValidator.TryParseAndValidate(json, out var result, out var error);

        Assert.True(ok, error);
        Assert.NotNull(result);
        Assert.Equal(sentiment, result!.Sentiment);
        Assert.Equal(emotions.Length, result.Emotions.Count);
    }

    [Fact]
    public void Validator_RejectsInvalidCategory_InvalidEmotions_MalformedJson_AndEmptyInsight()
    {
        // Invalid category
        Assert.False(JournalSentimentValidator.TryParseAndValidate(
            """{"sentiment":"Depressed","emotions":["Sadness"],"insight":"Valid insight."}""",
            out _, out _));

        // Invalid emotion label
        Assert.False(JournalSentimentValidator.TryParseAndValidate(
            """{"sentiment":"Negative","emotions":["ClinicalAnxiety"],"insight":"Valid insight."}""",
            out _, out _));

        // Malformed JSON
        Assert.False(JournalSentimentValidator.TryParseAndValidate(
            "{not valid json",
            out _, out _));

        // Empty insight
        Assert.False(JournalSentimentValidator.TryParseAndValidate(
            """{"sentiment":"Neutral","emotions":[],"insight":"   "}""",
            out _, out _));
    }

    [Fact]
    public async Task Persistence_SavesUpdatesWithoutDuplicates_AndPreservesJournalOnFailure()
    {
        var (db, conn, parent, _, child) = await TestDbContextFactory.CreateSeededTestContextAsync();
        using var _ = conn;
        using var __ = db;

        var entry = new JournalEntry
        {
            ChildProfileId = child.Id,
            EntryDate = new DateOnly(2026, 11, 20),
            BestPart = "Art class was fun",
            DifficultPart = "Math homework felt long",
            DayDescription = "Good overall but a little tired",
            Emotion = "Calm"
        };
        db.JournalEntries.Add(entry);
        await db.SaveChangesAsync();

        var fakeGemini = new StubGeminiAiService
        {
            NextResult = new JournalSentimentResult
            {
                Sentiment = "Mixed",
                Emotions = ["Excitement", "Stress"],
                Insight = "Enthusiasm for art alongside mild homework stress."
            }
        };

        var service = new JournalSentimentService(db, fakeGemini, NullLogger<JournalSentimentService>.Instance);

        // First analysis
        var saved = await service.AnalyzeAndSaveAsync(entry.Id);
        Assert.NotNull(saved);
        Assert.Equal("Mixed", saved!.Sentiment);
        Assert.Equal(1, await db.JournalSentimentAnalyses.CountAsync());

        // Re-analysis updates existing record instead of duplicating
        fakeGemini.NextResult = new JournalSentimentResult
        {
            Sentiment = "Positive",
            Emotions = ["Happiness"],
            Insight = "Updated positive interpretation."
        };
        var updated = await service.AnalyzeAndSaveAsync(entry.Id);
        Assert.NotNull(updated);
        Assert.Equal("Positive", updated!.Sentiment);
        Assert.Equal(1, await db.JournalSentimentAnalyses.CountAsync());

        // Original journal content unchanged
        var reloadedEntry = await db.JournalEntries.FindAsync(entry.Id);
        Assert.Equal("Art class was fun", reloadedEntry!.BestPart);

        // Failed analysis on a new entry does not delete the journal
        var entry2 = new JournalEntry
        {
            ChildProfileId = child.Id,
            EntryDate = new DateOnly(2026, 11, 21),
            BestPart = "Played outside",
            DifficultPart = "None",
            DayDescription = "Nice day"
        };
        db.JournalEntries.Add(entry2);
        await db.SaveChangesAsync();

        fakeGemini.NextResult = null;
        var failed = await service.AnalyzeAndSaveAsync(entry2.Id);
        Assert.Null(failed);
        Assert.NotNull(await db.JournalEntries.FindAsync(entry2.Id));
    }

    [Fact]
    public async Task JournalService_Submit_TriggersAnalysis_AndSucceedsEvenWhenGeminiFails()
    {
        var (db, conn, _, _, child) = await TestDbContextFactory.CreateSeededTestContextAsync();
        using var _ = conn;
        using var __ = db;

        var throwingGemini = new StubGeminiAiService { ThrowOnAnalyze = true };
        var sentimentService = new JournalSentimentService(db, throwingGemini, NullLogger<JournalSentimentService>.Instance);
        var journalService = new JournalService(db, sentimentService);

        var (res, conflict, validation) = await journalService.SubmitAsync(child.Id, new JournalSubmitRequest
        {
            BestPart = "Reading a book",
            DifficultPart = "Waking up early",
            DayDescription = "Peaceful day",
            Emotion = "Calm"
        });

        Assert.NotNull(res);
        Assert.Null(conflict);
        Assert.Null(validation);
        Assert.Equal(1, await db.JournalEntries.CountAsync());
        Assert.Equal(0, await db.JournalSentimentAnalyses.CountAsync());
    }

    private sealed class StubGeminiAiService : IGeminiAiService
    {
        public JournalSentimentResult? NextResult { get; set; }
        public bool ThrowOnAnalyze { get; set; }

        public Task<List<AssessmentQuestionItem>> GenerateQuestionsAsync(ChildProfile childProfile) =>
            Task.FromResult(new List<AssessmentQuestionItem>());

        public Task<AIReport> GenerateReportAsync(Assessment assessment, ChildProfile childProfile, List<AssessmentAnswer> answers) =>
            Task.FromResult(new AIReport());

        public Task<string> GenerateBuddyReplyAsync(ChildProfile childProfile, string userMessage, List<ChatMessage> conversationHistory) =>
            Task.FromResult("Hello");

        public Task<JournalSentimentResult?> AnalyzeJournalSentimentAsync(string journalText, CancellationToken cancellationToken = default)
        {
            if (ThrowOnAnalyze) throw new HttpRequestException("Simulated Gemini outage");
            return Task.FromResult(NextResult);
        }
    }
}

[Collection("WebApp")]
public class JournalSentimentIntegrationTests
{
    private readonly TestWebApplicationFactory _factory;

    public JournalSentimentIntegrationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task EndToEnd_JournalInsights_Retry_AndAuthorization()
    {
        var stubGemini = new ConfigurableMockGemini();
        using var customFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IGeminiAiService>();
                services.AddSingleton<IGeminiAiService>(stubGemini);
            });
        });

        var parentA = customFactory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = true, HandleCookies = true });
        var parentB = customFactory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var childClient = customFactory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = true, HandleCookies = true });

        // Register Parent A & Parent B
        await parentA.PostAsync("/Account/Register", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["FullName"] = "Parent Alpha",
            ["Email"] = $"alpha_{Guid.NewGuid():N}@example.com",
            ["Password"] = "ParentPass1!",
            ["ConfirmPassword"] = "ParentPass1!"
        }));
        await parentB.PostAsync("/Account/Register", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["FullName"] = "Parent Beta",
            ["Email"] = $"beta_{Guid.NewGuid():N}@example.com",
            ["Password"] = "ParentPass1!",
            ["ConfirmPassword"] = "ParentPass1!"
        }));

        // Parent A creates Child
        var addRes = await parentA.PostAsync("/Parent/AddChild", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["ChildName"] = "Maya Sentiment",
            ["DateOfBirth"] = "2015-07-04"
        }));
        var credHtml = await addRes.Content.ReadAsStringAsync();
        var childId = Regex.Match(credHtml, @"id=""credChildId""[^>]*>([^<]+)<").Groups[1].Value.Trim();
        var tempPwd = WebUtility.HtmlDecode(Regex.Match(credHtml, @"id=""credTempPassword""[^>]*>([^<]+)<").Groups[1].Value.Trim());

        int childProfileId;
        using (var scope = customFactory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            childProfileId = (await db.ChildProfiles.FirstAsync(c => c.ChildUser.UserName == childId)).Id;
        }

        // Child logs in and changes password
        await childClient.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["UserNameOrEmail"] = childId,
            ["Password"] = tempPwd
        }));
        await childClient.PostAsync("/Account/ChangePassword", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["CurrentPassword"] = tempPwd,
            ["NewPassword"] = "ChildSafe1!",
            ["ConfirmNewPassword"] = "ChildSafe1!"
        }));

        // Simulate Gemini unavailable during submission -> Journal still returns 201 Created
        stubGemini.Result = null;
        var submitRes = await childClient.PostAsJsonAsync("/api/journal/submit", new JournalSubmitRequest
        {
            BestPart = "Played music",
            DifficultPart = "Science quiz was hard",
            DayDescription = "Excited for band practice but tired from studying",
            Emotion = "Calm"
        });
        Assert.Equal(HttpStatusCode.Created, submitRes.StatusCode);
        var submitData = await submitRes.Content.ReadFromJsonAsync<JournalSubmitResponse>();
        Assert.NotNull(submitData);

        // Parent A retries analysis when Gemini recovers
        stubGemini.Result = new JournalSentimentResult
        {
            Sentiment = "Mixed",
            Emotions = ["Excitement", "Stress"],
            Insight = "The entry shows excitement about music alongside some quiz pressure."
        };
        var retryRes = await parentA.PostAsync("/Parent/RetryJournalAnalysis", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["journalEntryId"] = submitData!.Id.ToString(),
            ["childId"] = childProfileId.ToString()
        }));
        Assert.Equal(HttpStatusCode.OK, retryRes.StatusCode);
        var detailsHtml = await retryRes.Content.ReadAsStringAsync();
        Assert.Contains("Mixed", detailsHtml);
        Assert.Contains("Excitement", detailsHtml);
        Assert.Contains("AI-generated interpretation based on journal language", detailsHtml);

        // Parent B cannot view or retry Parent A's child insights
        var detailsByB = await parentB.GetAsync($"/Parent/ChildDetails/{childProfileId}");
        Assert.True(detailsByB.StatusCode == HttpStatusCode.Forbidden || detailsByB.StatusCode == HttpStatusCode.Redirect);

        var retryByB = await parentB.PostAsync("/Parent/RetryJournalAnalysis", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["journalEntryId"] = submitData.Id.ToString(),
            ["childId"] = childProfileId.ToString()
        }));
        Assert.True(retryByB.StatusCode == HttpStatusCode.Forbidden || retryByB.StatusCode == HttpStatusCode.Redirect);

        // Unauthenticated user rejected
        var anonClient = customFactory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var anonRes = await anonClient.GetAsync($"/Parent/ChildDetails/{childProfileId}");
        Assert.Equal(HttpStatusCode.Redirect, anonRes.StatusCode);
        Assert.Contains("/Account/Login", anonRes.Headers.Location?.ToString());
    }

    private sealed class ConfigurableMockGemini : IGeminiAiService
    {
        public JournalSentimentResult? Result { get; set; }

        public Task<List<AssessmentQuestionItem>> GenerateQuestionsAsync(ChildProfile childProfile) =>
            Task.FromResult(new List<AssessmentQuestionItem>());

        public Task<AIReport> GenerateReportAsync(Assessment assessment, ChildProfile childProfile, List<AssessmentAnswer> answers) =>
            Task.FromResult(new AIReport());

        public Task<string> GenerateBuddyReplyAsync(ChildProfile childProfile, string userMessage, List<ChatMessage> conversationHistory) =>
            Task.FromResult("Hello");

        public Task<JournalSentimentResult?> AnalyzeJournalSentimentAsync(string journalText, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result);
    }
}
