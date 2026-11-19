using DotNet_Project.Data;
using DotNet_Project.DTOs;
using DotNet_Project.Models;
using Microsoft.EntityFrameworkCore;

namespace DotNet_Project.Services;

public interface IAssessmentService
{
    Task<(AssessmentSubmitResponse? Response, Dictionary<string, List<string>>? Errors)> SubmitAsync(int childProfileId, AssessmentSubmitRequest request);
    Task<AssessmentHistoryResponse> GetHistoryAsync(int childProfileId, int page = 1, int size = 10);
    Task<AssessmentAnswersResponse?> GetAnswersAsync(int assessmentId, int? childProfileId = null, string? parentUserId = null);
}

public class AssessmentService(ApplicationDbContext db, IAIReportService reportService) : IAssessmentService
{
    public async Task<(AssessmentSubmitResponse? Response, Dictionary<string, List<string>>? Errors)> SubmitAsync(int childProfileId, AssessmentSubmitRequest request)
    {
        var errors = new Dictionary<string, List<string>>();

        if (request.Answers == null || request.Answers.Count != 10)
        {
            errors["answers"] = ["Exactly 10 answers are required."];
            return (null, errors);
        }

        var seenQuestions = new HashSet<int>();
        for (var i = 0; i < request.Answers.Count; i++)
        {
            var item = request.Answers[i];
            if (item.QuestionId < 1 || item.QuestionId > 10)
            {
                errors[$"answers[{i}].questionId"] = ["QuestionId must be between 1 and 10."];
            }
            else if (!seenQuestions.Add(item.QuestionId))
            {
                errors[$"answers[{i}].questionId"] = [$"Duplicate answer for question {item.QuestionId}."];
            }

            if (string.IsNullOrWhiteSpace(item.Answer) || !AssessmentQuestions.ValidAnswers.Contains(item.Answer.Trim(), StringComparer.OrdinalIgnoreCase))
            {
                errors[$"answers[{i}].answer"] = ["Answer must be one of: Never, Rarely, Sometimes, Often, Always."];
            }
        }

        if (errors.Count > 0)
        {
            return (null, errors);
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var assessment = new Assessment
        {
            ChildProfileId = childProfileId,
            AssessmentDate = today,
            Status = "Submitted",
            CreatedAt = DateTime.UtcNow
        };

        db.Assessments.Add(assessment);
        await db.SaveChangesAsync();

        foreach (var item in request.Answers)
        {
            var def = AssessmentQuestions.Definitions[item.QuestionId];
            db.AssessmentAnswers.Add(new AssessmentAnswer
            {
                AssessmentId = assessment.Id,
                QuestionId = item.QuestionId,
                QuestionText = string.IsNullOrWhiteSpace(item.QuestionText) ? def.Text : item.QuestionText.Trim(),
                Answer = item.Answer.Trim()
            });
        }

        await db.SaveChangesAsync();

        // Generate the AI Report for the assessment
        try
        {
            await reportService.GenerateReportAsync(assessment.Id);
        }
        catch
        {
            // Even if AI report generation fails or is delayed, the assessment submission succeeds
        }

        return (new AssessmentSubmitResponse
        {
            AssessmentId = assessment.Id,
            AssessmentDate = assessment.AssessmentDate.ToString("yyyy-MM-dd"),
            Status = assessment.Status,
            CreatedAt = assessment.CreatedAt
        }, null);
    }

    public async Task<AssessmentHistoryResponse> GetHistoryAsync(int childProfileId, int page = 1, int size = 10)
    {
        if (page < 1) page = 1;
        if (size < 1) size = 10;
        if (size > 100) size = 100;

        var query = db.Assessments
            .Where(a => a.ChildProfileId == childProfileId)
            .OrderByDescending(a => a.AssessmentDate)
            .ThenByDescending(a => a.CreatedAt);

        var total = await query.CountAsync();
        var assessments = await query
            .Skip((page - 1) * size)
            .Take(size)
            .Select(a => new AssessmentListItemDto
            {
                Id = a.Id,
                AssessmentDate = a.AssessmentDate.ToString("yyyy-MM-dd"),
                Status = a.Status,
                HasReport = a.AIReport != null,
                CreatedAt = a.CreatedAt
            })
            .ToListAsync();

        return new AssessmentHistoryResponse
        {
            TotalCount = total,
            Page = page,
            Size = size,
            Assessments = assessments
        };
    }

    public async Task<AssessmentAnswersResponse?> GetAnswersAsync(int assessmentId, int? childProfileId = null, string? parentUserId = null)
    {
        var assessment = await db.Assessments
            .Include(a => a.Answers)
            .Include(a => a.ChildProfile)
            .FirstOrDefaultAsync(a => a.Id == assessmentId);

        if (assessment is null) return null;

        // Authorization check: either caller is the child who owns the assessment, or the parent of the child
        if (childProfileId.HasValue && assessment.ChildProfileId != childProfileId.Value)
        {
            return null;
        }

        if (!string.IsNullOrEmpty(parentUserId) && assessment.ChildProfile.ParentUserId != parentUserId)
        {
            return null;
        }

        var answers = assessment.Answers
            .OrderBy(a => a.QuestionId)
            .Select(a =>
            {
                var category = AssessmentQuestions.Definitions.TryGetValue(a.QuestionId, out var def) ? def.Category : null;
                return new AssessmentAnswerItem
                {
                    QuestionId = a.QuestionId,
                    Category = category,
                    QuestionText = a.QuestionText,
                    Answer = a.Answer
                };
            })
            .ToList();

        return new AssessmentAnswersResponse
        {
            AssessmentId = assessment.Id,
            AssessmentDate = assessment.AssessmentDate.ToString("yyyy-MM-dd"),
            ChildName = assessment.ChildProfile.ChildName,
            Answers = answers
        };
    }
}
