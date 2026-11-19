using DotNet_Project.Models;
using DotNet_Project.Services;

namespace DotNet_Project.Tests;

public class AIReportServiceTests
{
    [Fact]
    public async Task GenerateReportAsync_CalculatesScoresAndGeneratesReport_Successfully()
    {
        var (db, conn, parent, child, profile) = await TestDbContextFactory.CreateSeededTestContextAsync();
        using var _ = conn;
        using var __ = db;

        var assessment = new Assessment
        {
            ChildProfileId = profile.Id,
            AssessmentDate = new DateOnly(2026, 11, 18),
            Status = "Submitted"
        };
        db.Assessments.Add(assessment);
        await db.SaveChangesAsync();

        var answers = new[]
        {
            ("Often", 1), ("Sometimes", 2),
            ("Sometimes", 3), ("Often", 4),
            ("Always", 5), ("Often", 6),
            ("Often", 7), ("Always", 8),
            ("Often", 9), ("Always", 10)
        };

        foreach (var (ans, qId) in answers)
        {
            db.AssessmentAnswers.Add(new AssessmentAnswer
            {
                AssessmentId = assessment.Id,
                QuestionId = qId,
                QuestionText = AssessmentQuestions.Definitions[qId].Text,
                Answer = ans
            });
        }
        await db.SaveChangesAsync();

        var service = new AIReportService(db, TestGeminiFactory.CreateTestService());
        var report = await service.GenerateReportAsync(assessment.Id);

        Assert.NotNull(report);
        Assert.Equal(assessment.Id, report.AssessmentId);
        Assert.Equal("Positive", report.OverallPattern);
        Assert.Contains("Alex Smith", report.Summary);
        Assert.False(string.IsNullOrWhiteSpace(report.Strengths));
        Assert.False(string.IsNullOrWhiteSpace(report.Suggestions));

        var formatted = service.FormatResponse(report, assessment, profile);
        Assert.Equal("Alex Smith", formatted.ChildName);
        Assert.Equal(87, formatted.CategoryScores.SocialRelationships);
        Assert.Equal(87, formatted.CategoryScores.SleepAndLifestyle);
        Assert.Equal(87, formatted.CategoryScores.SelfConfidence);
        Assert.Equal(62, formatted.CategoryScores.EmotionalWellbeing);
        Assert.Equal(62, formatted.CategoryScores.AcademicStress);
    }

    [Fact]
    public async Task GenerateReportAsync_WhenLowScores_AssignsNeedsAttentionPattern()
    {
        var (db, conn, parent, child, profile) = await TestDbContextFactory.CreateSeededTestContextAsync();
        using var _ = conn;
        using var __ = db;

        var assessment = new Assessment
        {
            ChildProfileId = profile.Id,
            AssessmentDate = new DateOnly(2026, 11, 18),
            Status = "Submitted"
        };
        db.Assessments.Add(assessment);
        await db.SaveChangesAsync();

        for (int q = 1; q <= 10; q++)
        {
            db.AssessmentAnswers.Add(new AssessmentAnswer
            {
                AssessmentId = assessment.Id,
                QuestionId = q,
                QuestionText = AssessmentQuestions.Definitions[q].Text,
                Answer = q == 3 ? "Always" : "Never"
            });
        }
        await db.SaveChangesAsync();

        var service = new AIReportService(db, TestGeminiFactory.CreateTestService());
        var report = await service.GenerateReportAsync(assessment.Id);

        Assert.NotNull(report);
        Assert.Equal("Needs Attention", report.OverallPattern);
        Assert.Contains("extra understanding", report.Summary);
    }
}
