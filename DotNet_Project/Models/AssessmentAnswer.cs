namespace DotNet_Project.Models;

public class AssessmentAnswer
{
    public int Id { get; set; }
    public int AssessmentId { get; set; }
    public int QuestionId { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public Assessment Assessment { get; set; } = null!;
}
