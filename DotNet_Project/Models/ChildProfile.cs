namespace DotNet_Project.Models;

public class ChildProfile
{
    public int Id { get; set; }
    public string ChildUserId { get; set; } = string.Empty;
    public string ParentUserId { get; set; } = string.Empty;
    public string ChildName { get; set; } = string.Empty;
    public DateOnly DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? GradeOrAgeGroup { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ApplicationUser ChildUser { get; set; } = null!;
    public ApplicationUser ParentUser { get; set; } = null!;
    public ICollection<MoodEntry> MoodEntries { get; set; } = new List<MoodEntry>();
    public ICollection<Assessment> Assessments { get; set; } = new List<Assessment>();
    public ICollection<JournalEntry> JournalEntries { get; set; } = new List<JournalEntry>();
    public ICollection<ChatMessage> ChatMessages { get; set; } = new List<ChatMessage>();
}
