using System.ComponentModel.DataAnnotations;

namespace DotNet_Project.DTOs;

public class RecordExerciseSessionRequest
{
    [Required, StringLength(40)]
    public string ExerciseType { get; set; } = string.Empty;

    [Range(0, 3600)]
    public int DurationSeconds { get; set; }

    public bool WasCompleted { get; set; } = true;
}

public class ExerciseSessionDto
{
    public int Id { get; set; }
    public string ExerciseType { get; set; } = string.Empty;
    public int DurationSeconds { get; set; }
    public DateTime CompletedAt { get; set; }
    public bool WasCompleted { get; set; }
}
