using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace DotNet_Project.DTOs;

public class ApiErrorResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; } = false;

    [JsonPropertyName("errors")]
    public Dictionary<string, List<string>> Errors { get; set; } = new();

    public static ApiErrorResponse WithError(string message, string field = "")
    {
        return new ApiErrorResponse
        {
            Success = false,
            Errors = new Dictionary<string, List<string>>
            {
                [field] = new() { message }
            }
        };
    }
}

public class MoodCheckInRequest
{
    [Required(ErrorMessage = "The mood field is required.")]
    public string Mood { get; set; } = string.Empty;

    [StringLength(300, ErrorMessage = "Reason cannot exceed 300 characters.")]
    public string? Reason { get; set; }
}

public class MoodResponse
{
    public int Id { get; set; }
    public string Mood { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class MoodTodayResponse
{
    public string Date { get; set; } = string.Empty;
    public List<MoodResponse> Entries { get; set; } = new();
}

public class JournalSubmitRequest
{
    [Required(ErrorMessage = "The bestPart field is required.")]
    [StringLength(2000, ErrorMessage = "BestPart cannot exceed 2000 characters.")]
    public string BestPart { get; set; } = string.Empty;

    [Required(ErrorMessage = "The difficultPart field is required.")]
    [StringLength(2000, ErrorMessage = "DifficultPart cannot exceed 2000 characters.")]
    public string DifficultPart { get; set; } = string.Empty;

    [Required(ErrorMessage = "The dayDescription field is required.")]
    [StringLength(2000, ErrorMessage = "DayDescription cannot exceed 2000 characters.")]
    public string DayDescription { get; set; } = string.Empty;

    [StringLength(2000, ErrorMessage = "HelpRequired cannot exceed 2000 characters.")]
    public string? HelpRequired { get; set; }

    [StringLength(50, ErrorMessage = "Emotion cannot exceed 50 characters.")]
    public string? Emotion { get; set; }
}

public class JournalSubmitResponse
{
    public int Id { get; set; }
    public string EntryDate { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class JournalItemDto
{
    public int Id { get; set; }
    public string EntryDate { get; set; } = string.Empty;
    public string BestPart { get; set; } = string.Empty;
    public string DifficultPart { get; set; } = string.Empty;
    public string DayDescription { get; set; } = string.Empty;
    public string? HelpRequired { get; set; }
    public string? Emotion { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class JournalHistoryResponse
{
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int Size { get; set; }
    public List<JournalItemDto> Entries { get; set; } = new();
}

public class AssessmentQuestionItem
{
    public int QuestionId { get; set; }
    public string Category { get; set; } = string.Empty;
    public string QuestionText { get; set; } = string.Empty;
    public string? HelperText { get; set; }
    public string[] Options { get; set; } = ["Never", "Rarely", "Sometimes", "Often", "Always"];
}

public class AssessmentAnswerItem
{
    public int QuestionId { get; set; }
    public string? Category { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
}

public class AssessmentSubmitRequest
{
    [Required]
    public List<AssessmentAnswerItem> Answers { get; set; } = new();
}

public class AssessmentSubmitResponse
{
    public int AssessmentId { get; set; }
    public string AssessmentDate { get; set; } = string.Empty;
    public string Status { get; set; } = "Submitted";
    public DateTime CreatedAt { get; set; }
}

public class AssessmentListItemDto
{
    public int Id { get; set; }
    public string AssessmentDate { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool HasReport { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AssessmentHistoryResponse
{
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int Size { get; set; }
    public List<AssessmentListItemDto> Assessments { get; set; } = new();
}

public class AssessmentAnswersResponse
{
    public int AssessmentId { get; set; }
    public string AssessmentDate { get; set; } = string.Empty;
    public string? ChildName { get; set; }
    public List<AssessmentAnswerItem> Answers { get; set; } = new();
}

public class CategoryScoresDto
{
    public int EmotionalWellbeing { get; set; }
    public int AcademicStress { get; set; }
    public int SocialRelationships { get; set; }
    public int SleepAndLifestyle { get; set; }
    public int SelfConfidence { get; set; }
}

public class AIReportResponse
{
    public int Id { get; set; }
    public int AssessmentId { get; set; }
    public string AssessmentDate { get; set; } = string.Empty;
    public string ChildName { get; set; } = string.Empty;
    public int ChildAge { get; set; }
    public string? GradeOrAgeGroup { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string Strengths { get; set; } = string.Empty;
    public string AreasOfConcern { get; set; } = string.Empty;
    public string Suggestions { get; set; } = string.Empty;
    public CategoryScoresDto CategoryScores { get; set; } = new();
    public string OverallPattern { get; set; } = "Positive";
    public DateTime GeneratedAt { get; set; }
}

public class ChatMessageRequest
{
    [Required(ErrorMessage = "Message cannot be empty.")]
    [StringLength(500, ErrorMessage = "Message cannot exceed 500 characters.")]
    public string Message { get; set; } = string.Empty;

    public string? ConversationId { get; set; }
}

public class ChatMessageDto
{
    public int Id { get; set; }
    public string Sender { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string ConversationId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class ChatSendMessageResponse
{
    public string ConversationId { get; set; } = string.Empty;
    public ChatMessageDto UserMessage { get; set; } = new();
    public ChatMessageDto BuddyReply { get; set; } = new();
}

public class ChatHistoryResponse
{
    public string ConversationId { get; set; } = string.Empty;
    public List<ChatMessageDto> Messages { get; set; } = new();
}

public class ParentChildChatHistoryResponse
{
    public int ChildProfileId { get; set; }
    public string ChildName { get; set; } = string.Empty;
    public List<ChatMessageDto> Messages { get; set; } = new();
}

public class ParentDailyJournalDto
{
    public int Id { get; set; }
    public string EntryDate { get; set; } = string.Empty;
    public string BestPart { get; set; } = string.Empty;
    public string DifficultPart { get; set; } = string.Empty;
    public string DayDescription { get; set; } = string.Empty;
    public string? HelpRequired { get; set; }
    public string? Emotion { get; set; }
    public DateTime SubmittedAt { get; set; }
}

public class ParentLatestAssessmentDto
{
    public int Id { get; set; }
    public string AssessmentDate { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool HasReport { get; set; }
}

public class ParentDailyActivityResponse
{
    public string Date { get; set; } = string.Empty;
    public string ChildName { get; set; } = string.Empty;
    public List<MoodResponse> MoodEntries { get; set; } = new();
    public ParentDailyJournalDto? Journal { get; set; }
    public ParentLatestAssessmentDto? LatestAssessment { get; set; }
}

public class ChildDashboardDto
{
    public int ChildProfileId { get; set; }
    public string ChildName { get; set; } = string.Empty;
    public int TodayMoodCount { get; set; }
    public bool JournalSubmittedToday { get; set; }
    public string? LastAssessmentDate { get; set; }
    public int DailyProgressCount { get; set; }
    public int DailyProgressTotal { get; set; } = 2;
}
