namespace DotNet_Project.Models;

public class WeeklyInsight
{
    public int Id { get; set; }
    public int ChildProfileId { get; set; }
    public DateOnly WeekStartDate { get; set; }
    public DateOnly WeekEndDate { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string ObservationsJson { get; set; } = "[]";
    public string AreasForSupportJson { get; set; } = "[]";
    public string RecommendationsJson { get; set; } = "[]";
    public string DataLimitationsJson { get; set; } = "[]";
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public ChildProfile ChildProfile { get; set; } = null!;
}
