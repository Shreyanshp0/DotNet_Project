using DotNet_Project.Models;
using DotNet_Project.Services;

namespace DotNet_Project.Tests;

public class GeminiAiServiceTests
{
    [Fact]
    public async Task GenerateQuestionsAsync_Returns10QuestionsCoveringAllDomains()
    {
        var service = TestGeminiFactory.CreateTestService();
        var profile = new ChildProfile
        {
            ChildName = "Alex Jenkins",
            DateOfBirth = new DateOnly(2014, 3, 10),
            GradeOrAgeGroup = "Grade 7"
        };

        var questions = await service.GenerateQuestionsAsync(profile);

        Assert.NotNull(questions);
        Assert.Equal(10, questions.Count);
        Assert.All(questions, q =>
        {
            Assert.True(q.QuestionId >= 1 && q.QuestionId <= 10);
            Assert.False(string.IsNullOrWhiteSpace(q.QuestionText));
            Assert.False(string.IsNullOrWhiteSpace(q.Category));
            Assert.NotEmpty(q.Options);
        });

        // Verify all 5 domains are represented
        var categories = questions.Select(q => q.Category).Distinct().ToList();
        Assert.Contains(categories, c => c.Contains("Emotional", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(categories, c => c.Contains("Academic", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(categories, c => c.Contains("Social", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(categories, c => c.Contains("Sleep", StringComparison.OrdinalIgnoreCase) || c.Contains("lifestyle", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(categories, c => c.Contains("Confidence", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GenerateReportAsync_GeneratesCompleteAssessmentReport()
    {
        var service = TestGeminiFactory.CreateTestService();
        var profile = new ChildProfile
        {
            Id = 1,
            ChildName = "Alex Jenkins",
            DateOfBirth = new DateOnly(2014, 3, 10),
            GradeOrAgeGroup = "Grade 7"
        };
        var assessment = new Assessment
        {
            Id = 42,
            ChildProfileId = profile.Id,
            AssessmentDate = new DateOnly(2026, 11, 19),
            Status = "Submitted"
        };

        var answers = new List<AssessmentAnswer>
        {
            new() { AssessmentId = 42, QuestionId = 1, QuestionText = "Do you feel happy?", Answer = "Often" },
            new() { AssessmentId = 42, QuestionId = 2, QuestionText = "Can you share feelings?", Answer = "Sometimes" },
            new() { AssessmentId = 42, QuestionId = 3, QuestionText = "Feel school stress?", Answer = "Rarely" },
            new() { AssessmentId = 42, QuestionId = 4, QuestionText = "Able to focus?", Answer = "Often" },
            new() { AssessmentId = 42, QuestionId = 5, QuestionText = "Comfortable with friends?", Answer = "Always" },
            new() { AssessmentId = 42, QuestionId = 6, QuestionText = "Feel included?", Answer = "Often" },
            new() { AssessmentId = 42, QuestionId = 7, QuestionText = "Get good sleep?", Answer = "Often" },
            new() { AssessmentId = 42, QuestionId = 8, QuestionText = "Make time for fun?", Answer = "Always" },
            new() { AssessmentId = 42, QuestionId = 9, QuestionText = "Feel confident?", Answer = "Often" },
            new() { AssessmentId = 42, QuestionId = 10, QuestionText = "Proud when learning?", Answer = "Always" }
        };

        var report = await service.GenerateReportAsync(assessment, profile, answers);

        Assert.NotNull(report);
        Assert.Equal(assessment.Id, report.AssessmentId);
        Assert.False(string.IsNullOrWhiteSpace(report.Summary));
        Assert.False(string.IsNullOrWhiteSpace(report.Strengths));
        Assert.False(string.IsNullOrWhiteSpace(report.Suggestions));
        Assert.NotNull(report.CategoryScoresJson);
        Assert.Equal("Positive", report.OverallPattern);
    }
}
