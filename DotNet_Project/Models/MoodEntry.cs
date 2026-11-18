namespace DotNet_Project.Models;

public class MoodEntry
{
    public int Id { get; set; }
    public int ChildProfileId { get; set; }
    public string Mood { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ChildProfile ChildProfile { get; set; } = null!;
}
