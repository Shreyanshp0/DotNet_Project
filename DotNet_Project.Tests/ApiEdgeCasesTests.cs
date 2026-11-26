using System.Net;
using System.Net.Http.Json;
using DotNet_Project.Data;
using DotNet_Project.DTOs;
using DotNet_Project.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DotNet_Project.Tests;

[Collection("WebApp")]
public class ApiEdgeCasesTests
{
    private readonly TestWebApplicationFactory _factory;

    public ApiEdgeCasesTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task UnauthenticatedRequests_ToApis_ReturnUnauthorized()
    {
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var moodRes = await client.PostAsJsonAsync("/api/mood/checkin", new MoodCheckInRequest { Mood = "Great" });
        Assert.Equal(HttpStatusCode.Unauthorized, moodRes.StatusCode);

        var journalRes = await client.PostAsJsonAsync("/api/journal/submit", new JournalSubmitRequest
        {
            BestPart = "Lunch", DifficultPart = "None", DayDescription = "Okay"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, journalRes.StatusCode);

        var reportRes = await client.GetAsync("/api/report/1");
        Assert.Equal(HttpStatusCode.Unauthorized, reportRes.StatusCode);

        var parentRes = await client.GetAsync("/api/parent/child/1/daily-activity");
        Assert.Equal(HttpStatusCode.Unauthorized, parentRes.StatusCode);
    }

    [Fact]
    public async Task GetQuestions_AuthenticatedChild_Returns10Questions()
    {
        var (parentClient, childClient, childId) = await CreateParentAndChildClientsAsync();

        var res = await childClient.GetAsync("/api/assessment/questions");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var questions = await res.Content.ReadFromJsonAsync<List<AssessmentQuestionItem>>();
        Assert.NotNull(questions);
        Assert.Equal(10, questions.Count);
    }

    [Fact]
    public async Task MoodCheckIn_WithInvalidData_ReturnsBadRequest()
    {
        var (parentClient, childClient, childId) = await CreateParentAndChildClientsAsync();

        // 1. Missing mood
        var emptyMoodRes = await childClient.PostAsJsonAsync("/api/mood/checkin", new MoodCheckInRequest { Mood = "" });
        Assert.Equal(HttpStatusCode.BadRequest, emptyMoodRes.StatusCode);
        var err1 = await emptyMoodRes.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.NotNull(err1);
        Assert.False(err1.Success);

        // 2. Invalid mood string
        var invalidMoodRes = await childClient.PostAsJsonAsync("/api/mood/checkin", new MoodCheckInRequest { Mood = "Enraged" });
        Assert.Equal(HttpStatusCode.BadRequest, invalidMoodRes.StatusCode);
        var err2 = await invalidMoodRes.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.NotNull(err2);
        Assert.False(err2.Success);
    }

    [Fact]
    public async Task AssessmentSubmit_WithInvalidData_ReturnsBadRequest()
    {
        var (parentClient, childClient, childId) = await CreateParentAndChildClientsAsync();

        // 1. Only 5 questions instead of 10
        var invalidCountReq = new AssessmentSubmitRequest
        {
            Answers = Enumerable.Range(1, 5).Select(i => new AssessmentAnswerItem
            {
                QuestionId = i,
                QuestionText = AssessmentQuestions.Definitions[i].Text,
                Answer = "Often"
            }).ToList()
        };
        var res1 = await childClient.PostAsJsonAsync("/api/assessment/submit", invalidCountReq);
        Assert.Equal(HttpStatusCode.BadRequest, res1.StatusCode);
        var err1 = await res1.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.NotNull(err1);
        Assert.True(err1.Errors.ContainsKey("answers"));

        // 2. Duplicate question ID
        var duplicateQReq = new AssessmentSubmitRequest
        {
            Answers = Enumerable.Range(1, 10).Select(i => new AssessmentAnswerItem
            {
                QuestionId = i == 2 ? 1 : i, // duplicate Question 1
                QuestionText = "text",
                Answer = "Often"
            }).ToList()
        };
        var res2 = await childClient.PostAsJsonAsync("/api/assessment/submit", duplicateQReq);
        Assert.Equal(HttpStatusCode.BadRequest, res2.StatusCode);

        // 3. Invalid answer choice
        var invalidAnswerReq = new AssessmentSubmitRequest
        {
            Answers = Enumerable.Range(1, 10).Select(i => new AssessmentAnswerItem
            {
                QuestionId = i,
                QuestionText = AssessmentQuestions.Definitions[i].Text,
                Answer = i == 1 ? "Unsure" : "Always"
            }).ToList()
        };
        var res3 = await childClient.PostAsJsonAsync("/api/assessment/submit", invalidAnswerReq);
        Assert.Equal(HttpStatusCode.BadRequest, res3.StatusCode);
    }

    [Fact]
    public async Task ChatSendMessage_Validation_And_SafetyAdvice()
    {
        var (parentClient, childClient, childId) = await CreateParentAndChildClientsAsync();

        // 1. Empty message
        var emptyMsgRes = await childClient.PostAsJsonAsync("/Chat/SendMessage", new ChatMessageRequest { Message = "" });
        Assert.Equal(HttpStatusCode.BadRequest, emptyMsgRes.StatusCode);

        // 2. Normal message
        var normalRes = await childClient.PostAsJsonAsync("/Chat/SendMessage", new ChatMessageRequest
        {
            Message = "I had a great time drawing today!",
            ConversationId = "conv-happy"
        });
        Assert.Equal(HttpStatusCode.OK, normalRes.StatusCode);
        var data = await normalRes.Content.ReadFromJsonAsync<ChatSendMessageResponse>();
        Assert.NotNull(data);
        Assert.Equal("conv-happy", data.ConversationId);
        Assert.False(string.IsNullOrWhiteSpace(data.BuddyReply.Message));
    }

    [Fact]
    public async Task ParentApi_AccessControl_And_InvalidDate()
    {
        var (parentClient, childClient, childId) = await CreateParentAndChildClientsAsync();

        // 1. Invalid date query string
        var invalidDateRes = await parentClient.GetAsync($"/api/parent/child/{childId}/daily-activity?date=invalid-date");
        Assert.Equal(HttpStatusCode.BadRequest, invalidDateRes.StatusCode);

        // 2. Non-existent child ID (e.g. 99999)
        var notFoundRes = await parentClient.GetAsync("/api/parent/child/99999/daily-activity");
        Assert.Equal(HttpStatusCode.NotFound, notFoundRes.StatusCode);
    }

    private async Task<(HttpClient ParentClient, HttpClient ChildClient, int ChildId)> CreateParentAndChildClientsAsync()
    {
        var parentClient = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = true,
            HandleCookies = true
        });

        var parentEmail = $"parent_{Guid.NewGuid():N}@example.com";
        var password = "SecurePass1!";

        await parentClient.PostAsync("/Account/Register", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["FullName"] = "Parent User",
            ["Email"] = parentEmail,
            ["Password"] = password,
            ["ConfirmPassword"] = password
        }));

        var childEmail = $"child_{Guid.NewGuid():N}@example.com";
        await parentClient.PostAsync("/Parent/AddChild", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["ChildName"] = "Alex Smith",
            ["DateOfBirth"] = "2014-03-10",
            ["Email"] = childEmail,
            ["Password"] = password,
            ["ConfirmPassword"] = password,
            ["Gender"] = "Male",
            ["GradeOrAgeGroup"] = "Grade 7"
        }));

        int childId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var profile = await db.ChildProfiles.FirstAsync(c => c.ChildUser.Email == childEmail);
            childId = profile.Id;
        }

        var childClient = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = true,
            HandleCookies = true
        });
        await childClient.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["UserNameOrEmail"] = childEmail,
            ["Password"] = password
        }));

        await childClient.PostAsync("/Account/ChangePassword", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["CurrentPassword"] = password,
            ["NewPassword"] = "NewSecurePass2!",
            ["ConfirmNewPassword"] = "NewSecurePass2!"
        }));

        return (parentClient, childClient, childId);
    }
}
