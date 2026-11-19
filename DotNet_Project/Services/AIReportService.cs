using System.Text.Json;
using DotNet_Project.Data;
using DotNet_Project.DTOs;
using DotNet_Project.Models;
using Microsoft.EntityFrameworkCore;

namespace DotNet_Project.Services;

public interface IAIReportService
{
    Task<AIReport> GenerateReportAsync(int assessmentId);
    AIReportResponse FormatResponse(AIReport report, Assessment assessment, ChildProfile child);
}

public class AIReportService(ApplicationDbContext db, IGeminiAiService geminiService) : IAIReportService
{
    public async Task<AIReport> GenerateReportAsync(int assessmentId)
    {
        var assessment = await db.Assessments
            .Include(a => a.Answers)
            .Include(a => a.ChildProfile)
            .FirstOrDefaultAsync(a => a.Id == assessmentId)
            ?? throw new KeyNotFoundException($"Assessment {assessmentId} not found.");

        var existing = await db.AIReports.FirstOrDefaultAsync(r => r.AssessmentId == assessmentId);
        if (existing is not null) return existing;

        var report = await geminiService.GenerateReportAsync(assessment, assessment.ChildProfile, assessment.Answers.ToList());
        report.AssessmentId = assessmentId;

        db.AIReports.Add(report);
        await db.SaveChangesAsync();
        return report;
    }

    public AIReportResponse FormatResponse(AIReport report, Assessment assessment, ChildProfile child)
    {
        CategoryScoresDto categoryScores;
        try
        {
            categoryScores = !string.IsNullOrEmpty(report.CategoryScoresJson)
                ? JsonSerializer.Deserialize<CategoryScoresDto>(report.CategoryScoresJson) ?? new()
                : new();
        }
        catch
        {
            categoryScores = new();
        }

        var age = DateTime.UtcNow.Year - child.DateOfBirth.Year;
        if (child.DateOfBirth > DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-age))) age--;

        return new AIReportResponse
        {
            Id = report.Id,
            AssessmentId = report.AssessmentId,
            AssessmentDate = assessment.AssessmentDate.ToString("yyyy-MM-dd"),
            ChildName = child.ChildName,
            ChildAge = Math.Max(1, age),
            GradeOrAgeGroup = child.GradeOrAgeGroup,
            Summary = report.Summary,
            Strengths = report.Strengths,
            AreasOfConcern = report.AreasOfConcern,
            Suggestions = report.Suggestions,
            CategoryScores = categoryScores,
            OverallPattern = report.OverallPattern,
            GeneratedAt = report.GeneratedAt
        };
    }
}
