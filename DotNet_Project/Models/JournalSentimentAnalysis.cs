namespace DotNet_Project.Models;

public class JournalSentimentAnalysis
{
    public int Id { get; set; }
    public int JournalEntryId { get; set; }
    public string Sentiment { get; set; } = string.Empty;
    public string EmotionIndicators { get; set; } = "[]";
    public string AIInsight { get; set; } = string.Empty;
    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
    public JournalEntry JournalEntry { get; set; } = null!;
}
