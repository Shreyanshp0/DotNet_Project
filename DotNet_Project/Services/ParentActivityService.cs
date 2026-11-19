using DotNet_Project.Data;
using DotNet_Project.DTOs;
using DotNet_Project.Models;
using Microsoft.EntityFrameworkCore;

namespace DotNet_Project.Services;

public interface IParentActivityService
{
    Task<ParentDailyActivityResponse?> GetDailyActivityAsync(string parentUserId, int childProfileId, DateOnly? date);
    Task<JournalHistoryResponse?> GetChildJournalAsync(string parentUserId, int childProfileId, int page = 1, int size = 10);
    Task<AssessmentAnswersResponse?> GetChildAssessmentAnswersAsync(string parentUserId, int childProfileId, int assessmentId);
    Task<ParentChildChatHistoryResponse?> GetChildChatHistoryAsync(string parentUserId, int childProfileId, string? conversationId = null);
    Task<ChildProfile?> GetOwnedChildProfileAsync(string parentUserId, int childProfileId);
    Task<List<ChildProfile>> GetChildrenForParentAsync(string parentUserId);
}

public class ParentActivityService(ApplicationDbContext db) : IParentActivityService
{
    public async Task<ChildProfile?> GetOwnedChildProfileAsync(string parentUserId, int childProfileId)
    {
        return await db.ChildProfiles
            .FirstOrDefaultAsync(c => c.Id == childProfileId && c.ParentUserId == parentUserId);
    }

    public async Task<List<ChildProfile>> GetChildrenForParentAsync(string parentUserId)
    {
        return await db.ChildProfiles
            .Where(c => c.ParentUserId == parentUserId)
            .OrderBy(c => c.ChildName)
            .ToListAsync();
    }

    public async Task<ParentDailyActivityResponse?> GetDailyActivityAsync(string parentUserId, int childProfileId, DateOnly? date)
    {
        var child = await GetOwnedChildProfileAsync(parentUserId, childProfileId);
        if (child is null) return null;

        var targetDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var startUtc = targetDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var endUtc = targetDate.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        var moods = await db.MoodEntries
            .Where(m => m.ChildProfileId == childProfileId && m.CreatedAt >= startUtc && m.CreatedAt <= endUtc)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new MoodResponse
            {
                Id = m.Id,
                Mood = m.Mood,
                Reason = m.Reason,
                CreatedAt = m.CreatedAt
            })
            .ToListAsync();

        var journal = await db.JournalEntries
            .Where(j => j.ChildProfileId == childProfileId && j.EntryDate == targetDate)
            .Select(j => new ParentDailyJournalDto
            {
                Id = j.Id,
                EntryDate = j.EntryDate.ToString("yyyy-MM-dd"),
                BestPart = j.BestPart,
                DifficultPart = j.DifficultPart,
                DayDescription = j.DayDescription,
                HelpRequired = j.HelpRequired,
                Emotion = j.Emotion,
                SubmittedAt = j.CreatedAt
            })
            .FirstOrDefaultAsync();

        var latestAssessment = await db.Assessments
            .Where(a => a.ChildProfileId == childProfileId)
            .OrderByDescending(a => a.AssessmentDate)
            .ThenByDescending(a => a.CreatedAt)
            .Select(a => new ParentLatestAssessmentDto
            {
                Id = a.Id,
                AssessmentDate = a.AssessmentDate.ToString("yyyy-MM-dd"),
                Status = a.Status,
                HasReport = a.AIReport != null
            })
            .FirstOrDefaultAsync();

        return new ParentDailyActivityResponse
        {
            Date = targetDate.ToString("yyyy-MM-dd"),
            ChildName = child.ChildName,
            MoodEntries = moods,
            Journal = journal,
            LatestAssessment = latestAssessment
        };
    }

    public async Task<JournalHistoryResponse?> GetChildJournalAsync(string parentUserId, int childProfileId, int page = 1, int size = 10)
    {
        var child = await GetOwnedChildProfileAsync(parentUserId, childProfileId);
        if (child is null) return null;

        if (page < 1) page = 1;
        if (size < 1) size = 10;
        if (size > 100) size = 100;

        var query = db.JournalEntries
            .Where(j => j.ChildProfileId == childProfileId)
            .OrderByDescending(j => j.EntryDate)
            .ThenByDescending(j => j.CreatedAt);

        var total = await query.CountAsync();
        var entries = await query
            .Skip((page - 1) * size)
            .Take(size)
            .Select(j => new JournalItemDto
            {
                Id = j.Id,
                EntryDate = j.EntryDate.ToString("yyyy-MM-dd"),
                BestPart = j.BestPart,
                DifficultPart = j.DifficultPart,
                DayDescription = j.DayDescription,
                HelpRequired = j.HelpRequired,
                Emotion = j.Emotion,
                CreatedAt = j.CreatedAt
            })
            .ToListAsync();

        return new JournalHistoryResponse
        {
            TotalCount = total,
            Page = page,
            Size = size,
            Entries = entries
        };
    }

    public async Task<AssessmentAnswersResponse?> GetChildAssessmentAnswersAsync(string parentUserId, int childProfileId, int assessmentId)
    {
        var child = await GetOwnedChildProfileAsync(parentUserId, childProfileId);
        if (child is null) return null;

        var assessment = await db.Assessments
            .Include(a => a.Answers)
            .FirstOrDefaultAsync(a => a.Id == assessmentId && a.ChildProfileId == childProfileId);

        if (assessment is null) return null;

        var answers = assessment.Answers
            .OrderBy(a => a.QuestionId)
            .Select(a =>
            {
                var category = AssessmentQuestions.Definitions.TryGetValue(a.QuestionId, out var def) ? def.Category : "General";
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
            ChildName = child.ChildName,
            Answers = answers
        };
    }

    public async Task<ParentChildChatHistoryResponse?> GetChildChatHistoryAsync(string parentUserId, int childProfileId, string? conversationId = null)
    {
        var child = await GetOwnedChildProfileAsync(parentUserId, childProfileId);
        if (child is null) return null;

        var query = db.ChatMessages
            .Where(m => m.ChildProfileId == childProfileId);

        if (!string.IsNullOrWhiteSpace(conversationId))
        {
            query = query.Where(m => m.ConversationId == conversationId.Trim());
        }

        var messages = await query
            .OrderBy(m => m.CreatedAt)
            .Select(m => new ChatMessageDto
            {
                Id = m.Id,
                Sender = m.Sender,
                Message = m.Message,
                ConversationId = m.ConversationId,
                CreatedAt = m.CreatedAt
            })
            .ToListAsync();

        return new ParentChildChatHistoryResponse
        {
            ChildProfileId = child.Id,
            ChildName = child.ChildName,
            Messages = messages
        };
    }
}
