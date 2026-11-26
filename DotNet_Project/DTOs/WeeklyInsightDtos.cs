using System.Text.Json.Serialization;

namespace DotNet_Project.DTOs;

public class WeeklyAggregationResult
{
    public int ChildProfileId { get; set; }
    public DateOnly WeekStartDate { get; set; }
    public DateOnly WeekEndDate { get; set; }
    public bool IsPartialWeek { get; set; }
    public int TotalMoodCheckIns { get; set; }
    public Dictionary<string, int> MoodCounts { get; set; } = new();
    public int TotalJournalEntries { get; set; }
    public List<DateOnly> JournalDates { get; set; } = new();
    public Dictionary<string, int> JournalSentimentCounts { get; set; } = new();
    public Dictionary<string, int> EmotionIndicatorCounts { get; set; } = new();
    public int TotalRelaxationSessions { get; set; }
    public Dictionary<string, int> RelaxationCounts { get; set; } = new();

    public int TotalEmotionalRecords => TotalMoodCheckIns + TotalJournalEntries;
}

public class WeeklyRecommendationItem
{
    [JsonPropertyName("activity")]
    public string Activity { get; set; } = string.Empty;

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;

    [JsonIgnore]
    public string ActivityUrl { get; set; } = "/Child/Relaxation";
}

public class WeeklyInsightAiResult
{
    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("observations")]
    public List<string> Observations { get; set; } = new();

    [JsonPropertyName("areasForSupport")]
    public List<string> AreasForSupport { get; set; } = new();

    [JsonPropertyName("recommendations")]
    public List<WeeklyRecommendationItem> Recommendations { get; set; } = new();

    [JsonPropertyName("dataLimitations")]
    public List<string> DataLimitations { get; set; } = new();
}

public class WeeklyInsightSummaryItem
{
    public DateOnly WeekStartDate { get; set; }
    public DateOnly WeekEndDate { get; set; }
    public DateTime GeneratedAt { get; set; }
}

public class WeeklyInsightViewModel
{
    public int ChildProfileId { get; set; }
    public string ChildName { get; set; } = string.Empty;
    public DateOnly WeekStartDate { get; set; }
    public DateOnly WeekEndDate { get; set; }
    public bool IsPartialWeek { get; set; }
    public bool HasData { get; set; }
    public bool HasSufficientData { get; set; }
    public bool UsedAiGeneration { get; set; }
    public int TotalMoodCheckIns { get; set; }
    public int TotalJournalEntries { get; set; }
    public int TotalRelaxationSessions { get; set; }
    public Dictionary<string, int> MoodCounts { get; set; } = new();
    public Dictionary<string, int> JournalSentimentCounts { get; set; } = new();
    public string Summary { get; set; } = string.Empty;
    public List<string> Observations { get; set; } = new();
    public List<string> AreasForSupport { get; set; } = new();
    public List<WeeklyRecommendationItem> Recommendations { get; set; } = new();
    public List<string> DataLimitations { get; set; } = new();
    public DateTime? GeneratedAt { get; set; }
    public List<WeeklyInsightSummaryItem> PreviousWeeks { get; set; } = new();
}
