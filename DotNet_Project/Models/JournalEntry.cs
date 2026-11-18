namespace DotNet_Project.Models;

public class JournalEntry
{
    public int Id { get; set; }
    public int ChildProfileId { get; set; }
    public DateOnly EntryDate { get; set; }
    public string BestPart { get; set; } = string.Empty;
    public string DifficultPart { get; set; } = string.Empty;
    public string DayDescription { get; set; } = string.Empty;
    public string? HelpRequired { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ChildProfile ChildProfile { get; set; } = null!;
}
