using System.Text.Json.Serialization;

namespace DotNet_Project.DTOs;

public class JournalSentimentResult
{
    [JsonPropertyName("sentiment")]
    public string Sentiment { get; set; } = string.Empty;

    [JsonPropertyName("emotions")]
    public List<string> Emotions { get; set; } = new();

    [JsonPropertyName("insight")]
    public string Insight { get; set; } = string.Empty;
}

public class JournalInsightDto
{
    public int JournalEntryId { get; set; }
    public string EntryDate { get; set; } = string.Empty;
    public string? ChildSelfEmotion { get; set; }
    public bool HasAnalysis { get; set; }
    public string? Sentiment { get; set; }
    public List<string> EmotionIndicators { get; set; } = new();
    public string? AIInsight { get; set; }
    public DateTime? AnalyzedAt { get; set; }
}
