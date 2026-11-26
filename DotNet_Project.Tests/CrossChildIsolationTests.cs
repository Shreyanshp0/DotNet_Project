using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using DotNet_Project.Data;
using DotNet_Project.DTOs;
using DotNet_Project.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DotNet_Project.Tests;

[Collection("WebApp")]
public class CrossChildIsolationTests
{
    private readonly TestWebApplicationFactory _factory;

    public CrossChildIsolationTests(TestWebApplicationFactory factory)
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
    public async Task ChildBWithoutAssessment_NeverDisplaysChildAAssessmentOrReport()
    {
        var parentClient = CreateClient();
        await parentClient.PostAsync("/Account/Register", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["FullName"] = "Parent Isolation",
            ["Email"] = $"iso_parent_{Guid.NewGuid():N}@example.com",
            ["Password"] = "ParentPass1!",
            ["ConfirmPassword"] = "ParentPass1!"
        }));

        // Create Child A
        var addARes = await parentClient.PostAsync("/Parent/AddChild", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["ChildName"] = "Child Alpha",
            ["DateOfBirth"] = "2014-05-10"
        }));
        var htmlA = await addARes.Content.ReadAsStringAsync();
        var childAUsername = Regex.Match(htmlA, @"id=""credChildId""[^>]*>([^<]+)<").Groups[1].Value.Trim();
        var childAPwd = WebUtility.HtmlDecode(Regex.Match(htmlA, @"id=""credTempPassword""[^>]*>([^<]+)<").Groups[1].Value.Trim());

        // Create Child B
        var addBRes = await parentClient.PostAsync("/Parent/AddChild", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["ChildName"] = "Child Beta",
            ["DateOfBirth"] = "2016-09-12"
        }));
        var htmlB = await addBRes.Content.ReadAsStringAsync();
        var childBUsername = Regex.Match(htmlB, @"id=""credChildId""[^>]*>([^<]+)<").Groups[1].Value.Trim();

        int childAId, childBId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            childAId = (await db.ChildProfiles.FirstAsync(c => c.ChildUser.UserName == childAUsername)).Id;
            childBId = (await db.ChildProfiles.FirstAsync(c => c.ChildUser.UserName == childBUsername)).Id;
        }

        // Child A logs in, changes password, and submits an assessment
        var childAClient = CreateClient();
        await childAClient.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["UserNameOrEmail"] = childAUsername,
            ["Password"] = childAPwd
        }));
        await childAClient.PostAsync("/Account/ChangePassword", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["CurrentPassword"] = childAPwd,
            ["NewPassword"] = "ChildAlpha1!",
            ["ConfirmNewPassword"] = "ChildAlpha1!"
        }));

        var assessmentReq = new AssessmentSubmitRequest
        {
            Answers = Enumerable.Range(1, 10).Select(i => new AssessmentAnswerItem
            {
                QuestionId = i,
                QuestionText = AssessmentQuestions.Definitions[i].Text,
                Answer = "Often"
            }).ToList()
        };
        var submitRes = await childAClient.PostAsJsonAsync("/api/assessment/submit", assessmentReq);
        Assert.Equal(HttpStatusCode.Created, submitRes.StatusCode);
        var submitData = await submitRes.Content.ReadFromJsonAsync<AssessmentSubmitResponse>();
        Assert.NotNull(submitData);

        // 1. Parent opens Child A's Report -> shows Child Alpha's report
        var reportARes = await parentClient.GetAsync($"/Parent/Report?childId={childAId}");
        Assert.Equal(HttpStatusCode.OK, reportARes.StatusCode);
        var reportAHtml = await reportARes.Content.ReadAsStringAsync();
        Assert.Contains("Child Alpha", reportAHtml);
        Assert.Contains($"MC-{submitData!.AssessmentId}", reportAHtml);

        // 2. Parent opens Child B's Details -> Report link is scoped to childId=childBId
        var detailsBHtml = await (await parentClient.GetAsync($"/Parent/ChildDetails/{childBId}")).Content.ReadAsStringAsync();
        Assert.Contains($"childId={childBId}", detailsBHtml);

        // 3. Parent opens Child B's Latest Report -> must NOT show Child Alpha's assessment or report
        var reportBRes = await parentClient.GetAsync($"/Parent/Report?childId={childBId}");
        Assert.Equal(HttpStatusCode.OK, reportBRes.StatusCode);
        var reportBHtml = await reportBRes.Content.ReadAsStringAsync();
        Assert.Contains("No assessment submitted yet", reportBHtml);
        Assert.Contains("Child Beta has not completed a wellbeing assessment yet.", reportBHtml);
        Assert.DoesNotContain($"MC-{submitData.AssessmentId}", reportBHtml);
        Assert.DoesNotContain("Response pattern", reportBHtml, StringComparison.OrdinalIgnoreCase);

        // 4. Parent opens Child B's History -> must NOT show Child Alpha's assessment or sample MC-1042
        var historyBHtml = await (await parentClient.GetAsync($"/Parent/History?childId={childBId}")).Content.ReadAsStringAsync();
        Assert.Contains("No assessments submitted yet for Child Beta", historyBHtml);
        Assert.DoesNotContain($"MC-{submitData.AssessmentId}", historyBHtml);
        Assert.DoesNotContain("MC-1042", historyBHtml);

        // 5. Parent opens Child B's QuestionnaireDetails -> must NOT show Child Alpha's answers
        var answersBHtml = await (await parentClient.GetAsync($"/Parent/QuestionnaireDetails?childId={childBId}")).Content.ReadAsStringAsync();
        Assert.Contains("No assessment submitted yet for Child Beta", answersBHtml);
        Assert.DoesNotContain("Child Alpha", answersBHtml);

        // 6. Mismatched assessmentId + childId returns NotFound
        var mismatchRes = await parentClient.GetAsync($"/Parent/Report/{submitData.AssessmentId}?childId={childBId}");
        Assert.Equal(HttpStatusCode.NotFound, mismatchRes.StatusCode);

        // 7. Another parent cannot access Child A's Report
        var otherParent = CreateClient(allowAutoRedirect: false);
        await otherParent.PostAsync("/Account/Register", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["FullName"] = "Other Parent",
            ["Email"] = $"other_{Guid.NewGuid():N}@example.com",
            ["Password"] = "ParentPass1!",
            ["ConfirmPassword"] = "ParentPass1!"
        }));
        var otherReport = await otherParent.GetAsync($"/Parent/Report?childId={childAId}");
        Assert.True(otherReport.StatusCode == HttpStatusCode.Forbidden || otherReport.StatusCode == HttpStatusCode.Redirect);
    }
}
