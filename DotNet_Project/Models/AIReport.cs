namespace DotNet_Project.Models;

public class AIReport
{
    public int Id { get; set; }
    public int AssessmentId { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string Strengths { get; set; } = string.Empty;
    public string AreasOfConcern { get; set; } = string.Empty;
    public string Suggestions { get; set; } = string.Empty;
    public string? CategoryScoresJson { get; set; }
    public string OverallPattern { get; set; } = "Positive";
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public Assessment Assessment { get; set; } = null!;
}
