using DotNet_Project.DTOs;
using DotNet_Project.Services;

namespace DotNet_Project.Tests;

public class AssessmentServiceTests
{
    [Fact]
    public async Task SubmitAsync_WithValid10Answers_SucceedsAndGeneratesAIReport()
    {
        var (db, conn, parent, child, profile) = await TestDbContextFactory.CreateSeededTestContextAsync();
        using var _ = conn;
        using var __ = db;

        var reportService = new AIReportService(db, TestGeminiFactory.CreateTestService());
        var assessmentService = new AssessmentService(db, reportService);

        var request = new AssessmentSubmitRequest
        {
            Answers = Enumerable.Range(1, 10).Select(i => new AssessmentAnswerItem
            {
                QuestionId = i,
                QuestionText = AssessmentQuestions.Definitions[i].Text,
                Answer = "Often"
            }).ToList()
        };

        var (response, errors) = await assessmentService.SubmitAsync(profile.Id, request);

        Assert.Null(errors);
        Assert.NotNull(response);
        Assert.True(response.AssessmentId > 0);
        Assert.Equal("Submitted", response.Status);

        // Verify history
        var history = await assessmentService.GetHistoryAsync(profile.Id);
        Assert.Equal(1, history.TotalCount);
        Assert.True(history.Assessments[0].HasReport);

        // Verify answers
        var answers = await assessmentService.GetAnswersAsync(response.AssessmentId, profile.Id);
        Assert.NotNull(answers);
        Assert.Equal(10, answers.Answers.Count);
    }

    [Fact]
    public async Task SubmitAsync_WithLessThan10Answers_ReturnsValidationError()
    {
        var (db, conn, parent, child, profile) = await TestDbContextFactory.CreateSeededTestContextAsync();
        using var _ = conn;
        using var __ = db;

        var reportService = new AIReportService(db, TestGeminiFactory.CreateTestService());
        var assessmentService = new AssessmentService(db, reportService);

        var request = new AssessmentSubmitRequest
        {
            Answers =
            [
                new() { QuestionId = 1, QuestionText = "Q1", Answer = "Often" },
                new() { QuestionId = 2, QuestionText = "Q2", Answer = "Always" }
            ]
        };

        var (response, errors) = await assessmentService.SubmitAsync(profile.Id, request);

        Assert.Null(response);
        Assert.NotNull(errors);
        Assert.True(errors.ContainsKey("answers"));
    }

    [Fact]
    public async Task SubmitAsync_WithInvalidAnswerChoice_ReturnsValidationError()
    {
        var (db, conn, parent, child, profile) = await TestDbContextFactory.CreateSeededTestContextAsync();
        using var _ = conn;
        using var __ = db;

        var reportService = new AIReportService(db, TestGeminiFactory.CreateTestService());
        var assessmentService = new AssessmentService(db, reportService);

        var answers = Enumerable.Range(1, 10).Select(i => new AssessmentAnswerItem
        {
            QuestionId = i,
            QuestionText = AssessmentQuestions.Definitions[i].Text,
            Answer = i == 3 ? "InvalidAnswer" : "Often"
        }).ToList();

        var (response, errors) = await assessmentService.SubmitAsync(profile.Id, new AssessmentSubmitRequest { Answers = answers });

        Assert.Null(response);
        Assert.NotNull(errors);
        Assert.True(errors.ContainsKey("answers[2].answer"));
    }
}
