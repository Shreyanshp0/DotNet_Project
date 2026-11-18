namespace DotNet_Project.Models;

public class Assessment
{
    public int Id { get; set; }
    public int ChildProfileId { get; set; }
    public DateOnly AssessmentDate { get; set; }
    public string Status { get; set; } = "InProgress";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ChildProfile ChildProfile { get; set; } = null!;
    public ICollection<AssessmentAnswer> Answers { get; set; } = new List<AssessmentAnswer>();
    public AIReport? AIReport { get; set; }
}
