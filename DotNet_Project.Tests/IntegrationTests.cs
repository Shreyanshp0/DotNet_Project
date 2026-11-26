using System.Net;
using System.Net.Http.Json;
using DotNet_Project.Data;
using DotNet_Project.DTOs;
using DotNet_Project.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DotNet_Project.Tests;

public class IntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public IntegrationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CompleteEndToEndFlow_WorksSeamlessly()
    {
        var parentClient = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = true,
            HandleCookies = true
        });

        var parentEmail = $"parent_{Guid.NewGuid():N}@example.com";
        var password = "SecurePass1!";

        // 1. Register Parent
        var registerContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["FullName"] = "Parent Jane",
            ["Email"] = parentEmail,
            ["Password"] = password,
            ["ConfirmPassword"] = password
        });
        var registerResponse = await parentClient.PostAsync("/Account/Register", registerContent);
        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

        // 2. Add Child
        var childEmail = $"child_{Guid.NewGuid():N}@example.com";
        var addChildContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["ChildName"] = "Alex Smith",
            ["DateOfBirth"] = "2014-03-10",
            ["Email"] = childEmail,
            ["Password"] = password,
            ["ConfirmPassword"] = password,
            ["Gender"] = "Male",
            ["GradeOrAgeGroup"] = "Grade 7"
        });
        var addChildResponse = await parentClient.PostAsync("/Parent/AddChild", addChildContent);
        Assert.Equal(HttpStatusCode.OK, addChildResponse.StatusCode);

        // 3. Child Login with a separate client
        var childClient = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = true,
            HandleCookies = true
        });
        var loginContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["UserNameOrEmail"] = childEmail,
            ["Password"] = password
        });
        var loginResponse = await childClient.PostAsync("/Account/Login", loginContent);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        // 3b. First login requires password change before accessing dashboard/APIs
        var blockedMoodRes = await childClient.PostAsJsonAsync("/api/mood/checkin", new MoodCheckInRequest { Mood = "Great", Reason = "Test" });
        Assert.Equal(HttpStatusCode.Forbidden, blockedMoodRes.StatusCode);

        var newChildPassword = "NewSecurePass2!";
        var changePwdRes = await childClient.PostAsync("/Account/ChangePassword", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["CurrentPassword"] = password,
            ["NewPassword"] = newChildPassword,
            ["ConfirmNewPassword"] = newChildPassword
        }));
        Assert.Equal(HttpStatusCode.OK, changePwdRes.StatusCode);

        // 4. Child: Mood Check-In (POST /api/mood/checkin)
        var moodReq = new MoodCheckInRequest { Mood = "Great", Reason = "Had fun at school" };
        var moodPostRes = await childClient.PostAsJsonAsync("/api/mood/checkin", moodReq);
        Assert.Equal(HttpStatusCode.OK, moodPostRes.StatusCode);
        var moodPostData = await moodPostRes.Content.ReadFromJsonAsync<MoodResponse>();
        Assert.NotNull(moodPostData);
        Assert.Equal("Great", moodPostData.Mood);

        // Child: Get today's mood (GET /api/mood/today)
        var moodTodayRes = await childClient.GetAsync("/api/mood/today");
        Assert.Equal(HttpStatusCode.OK, moodTodayRes.StatusCode);
        var moodTodayData = await moodTodayRes.Content.ReadFromJsonAsync<MoodTodayResponse>();
        Assert.NotNull(moodTodayData);
        Assert.Single(moodTodayData.Entries);

        // 5. Child: Journal Submit (POST /api/journal/submit)
        var journalReq = new JournalSubmitRequest
        {
            BestPart = "Played soccer with friends",
            DifficultPart = "Math test was tricky",
            DayDescription = "Fun and busy day",
            HelpRequired = "Math tutoring",
            Emotion = "Happy"
        };
        var journalPostRes = await childClient.PostAsJsonAsync("/api/journal/submit", journalReq);
        Assert.Equal(HttpStatusCode.Created, journalPostRes.StatusCode);
        var journalPostData = await journalPostRes.Content.ReadFromJsonAsync<JournalSubmitResponse>();
        Assert.NotNull(journalPostData);
        Assert.True(journalPostData.Id > 0);

        // Child: Submit again today -> Should return 409 Conflict
        var duplicateJournalRes = await childClient.PostAsJsonAsync("/api/journal/submit", journalReq);
        Assert.Equal(HttpStatusCode.Conflict, duplicateJournalRes.StatusCode);

        // Child: Journal History (GET /api/journal/history)
        var journalHistoryRes = await childClient.GetAsync("/api/journal/history");
        Assert.Equal(HttpStatusCode.OK, journalHistoryRes.StatusCode);
        var journalHistoryData = await journalHistoryRes.Content.ReadFromJsonAsync<JournalHistoryResponse>();
        Assert.NotNull(journalHistoryData);
        Assert.Equal(1, journalHistoryData.TotalCount);

        // 6. Child: Assessment Submit (POST /api/assessment/submit)
        var assessmentReq = new AssessmentSubmitRequest
        {
            Answers = Enumerable.Range(1, 10).Select(i => new AssessmentAnswerItem
            {
                QuestionId = i,
                QuestionText = AssessmentQuestions.Definitions[i].Text,
                Answer = "Often"
            }).ToList()
        };
        var assessmentPostRes = await childClient.PostAsJsonAsync("/api/assessment/submit", assessmentReq);
        Assert.Equal(HttpStatusCode.Created, assessmentPostRes.StatusCode);
        var assessmentPostData = await assessmentPostRes.Content.ReadFromJsonAsync<AssessmentSubmitResponse>();
        Assert.NotNull(assessmentPostData);
        Assert.True(assessmentPostData.AssessmentId > 0);

        // Child: Assessment History (GET /api/assessment/history)
        var assessmentHistRes = await childClient.GetAsync("/api/assessment/history");
        Assert.Equal(HttpStatusCode.OK, assessmentHistRes.StatusCode);
        var assessmentHistData = await assessmentHistRes.Content.ReadFromJsonAsync<AssessmentHistoryResponse>();
        Assert.NotNull(assessmentHistData);
        Assert.Equal(1, assessmentHistData.TotalCount);
        Assert.True(assessmentHistData.Assessments[0].HasReport);

        // Child: Assessment Answers (GET /api/assessment/{id}/answers)
        var assessmentAnswersRes = await childClient.GetAsync($"/api/assessment/{assessmentPostData.AssessmentId}/answers");
        Assert.Equal(HttpStatusCode.OK, assessmentAnswersRes.StatusCode);
        var assessmentAnswersData = await assessmentAnswersRes.Content.ReadFromJsonAsync<AssessmentAnswersResponse>();
        Assert.NotNull(assessmentAnswersData);
        Assert.Equal(10, assessmentAnswersData.Answers.Count);

        // 7. Child: MindCare Buddy Chat (POST /Chat/SendMessage)
        var chatReq = new ChatMessageRequest
        {
            Message = "I am a bit nervous about tomorrow's presentation",
            ConversationId = "conv-integration-test"
        };
        var chatPostRes = await childClient.PostAsJsonAsync("/Chat/SendMessage", chatReq);
        Assert.Equal(HttpStatusCode.OK, chatPostRes.StatusCode);
        var chatPostData = await chatPostRes.Content.ReadFromJsonAsync<ChatSendMessageResponse>();
        Assert.NotNull(chatPostData);
        Assert.Equal("child", chatPostData.UserMessage.Sender);
        Assert.Equal("buddy", chatPostData.BuddyReply.Sender);
        Assert.False(string.IsNullOrWhiteSpace(chatPostData.BuddyReply.Message));

        // Child: Chat History (GET /api/chat/history/{convId})
        var chatHistoryRes = await childClient.GetAsync($"/api/chat/history/{chatPostData.ConversationId}");
        Assert.Equal(HttpStatusCode.OK, chatHistoryRes.StatusCode);
        var chatHistoryData = await chatHistoryRes.Content.ReadFromJsonAsync<ChatHistoryResponse>();
        Assert.NotNull(chatHistoryData);
        Assert.Equal(2, chatHistoryData.Messages.Count);

        // 8. Parent: View AI Report (GET /api/report/{assessmentId})
        var reportRes = await parentClient.GetAsync($"/api/report/{assessmentPostData.AssessmentId}");
        Assert.Equal(HttpStatusCode.OK, reportRes.StatusCode);
        var reportData = await reportRes.Content.ReadFromJsonAsync<AIReportResponse>();
        Assert.NotNull(reportData);
        Assert.Equal("Alex Smith", reportData.ChildName);
        Assert.Equal("Positive", reportData.OverallPattern);
        Assert.False(string.IsNullOrWhiteSpace(reportData.Summary));
        Assert.False(string.IsNullOrWhiteSpace(reportData.Strengths));

        // 9. Parent: Activity Views - retrieve the exact childProfileId from database
        int childProfileId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var profile = await db.ChildProfiles.FirstAsync(c => c.ChildUser.Email == childEmail);
            childProfileId = profile.Id;
        }

        var dailyActRes = await parentClient.GetAsync($"/api/parent/child/{childProfileId}/daily-activity");
        Assert.Equal(HttpStatusCode.OK, dailyActRes.StatusCode);
        var dailyActData = await dailyActRes.Content.ReadFromJsonAsync<ParentDailyActivityResponse>();
        Assert.NotNull(dailyActData);
        Assert.Equal("Alex Smith", dailyActData.ChildName);
        Assert.Single(dailyActData.MoodEntries);
        Assert.NotNull(dailyActData.Journal);
        Assert.NotNull(dailyActData.LatestAssessment);

        var parentJournalRes = await parentClient.GetAsync($"/api/parent/child/{childProfileId}/journal");
        Assert.Equal(HttpStatusCode.OK, parentJournalRes.StatusCode);
        var parentJournalData = await parentJournalRes.Content.ReadFromJsonAsync<JournalHistoryResponse>();
        Assert.NotNull(parentJournalData);
        Assert.Equal(1, parentJournalData.TotalCount);

        var parentAnswersRes = await parentClient.GetAsync($"/api/parent/child/{childProfileId}/assessment/{assessmentPostData.AssessmentId}/answers");
        Assert.Equal(HttpStatusCode.OK, parentAnswersRes.StatusCode);
        var parentAnswersData = await parentAnswersRes.Content.ReadFromJsonAsync<AssessmentAnswersResponse>();
        Assert.NotNull(parentAnswersData);
        Assert.Equal(10, parentAnswersData.Answers.Count);

        // 10. Parent: View Child's MindCare Buddy Chat History (GET /api/parent/child/{id}/chat-history)
        var parentChatRes = await parentClient.GetAsync($"/api/parent/child/{childProfileId}/chat-history");
        Assert.Equal(HttpStatusCode.OK, parentChatRes.StatusCode);
        var parentChatData = await parentChatRes.Content.ReadFromJsonAsync<ParentChildChatHistoryResponse>();
        Assert.NotNull(parentChatData);
        Assert.Equal("Alex Smith", parentChatData.ChildName);
        Assert.Equal(2, parentChatData.Messages.Count);
        Assert.Contains(parentChatData.Messages, m => m.Sender == "child" && m.Message == "I am a bit nervous about tomorrow's presentation");
        Assert.Contains(parentChatData.Messages, m => m.Sender == "buddy");

        // Parent MVC ChatHistory View renders successfully
        var parentChatViewRes = await parentClient.GetAsync($"/Parent/ChatHistory?childId={childProfileId}");
        Assert.Equal(HttpStatusCode.OK, parentChatViewRes.StatusCode);
        var parentChatViewHtml = await parentChatViewRes.Content.ReadAsStringAsync();
        Assert.Contains("MindCare Buddy Conversations", parentChatViewHtml);

        // 11. Role Security Checks:
        // Child cannot access Parent Chat History API -> 403 Forbidden
        var childAccessingParentChat = await childClient.GetAsync($"/api/parent/child/{childProfileId}/chat-history");
        Assert.Equal(HttpStatusCode.Forbidden, childAccessingParentChat.StatusCode);

        // Child cannot access Parent API -> 403 Forbidden
        var childAccessingParentApi = await childClient.GetAsync($"/api/parent/child/{childProfileId}/daily-activity");
        Assert.Equal(HttpStatusCode.Forbidden, childAccessingParentApi.StatusCode);

        // Parent cannot access Child API -> 403 Forbidden
        var parentAccessingChildApi = await parentClient.PostAsJsonAsync("/api/mood/checkin", moodReq);
        Assert.Equal(HttpStatusCode.Forbidden, parentAccessingChildApi.StatusCode);
    }
}
