namespace DotNet_Project.Models;

public class ExerciseSession
{
    public int Id { get; set; }
    public int ChildProfileId { get; set; }
    public string ExerciseType { get; set; } = string.Empty;
    public int DurationSeconds { get; set; }
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
    public bool WasCompleted { get; set; } = true;
    public ChildProfile ChildProfile { get; set; } = null!;
}
