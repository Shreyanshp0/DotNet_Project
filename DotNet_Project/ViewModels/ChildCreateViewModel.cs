using System.ComponentModel.DataAnnotations;

namespace DotNet_Project.ViewModels;

public class ChildCreateViewModel
{
    [Required, StringLength(200, MinimumLength = 2)] public string ChildName { get; set; } = string.Empty;
    [Required, DataType(DataType.Date)] public DateOnly? DateOfBirth { get; set; }
    [Required, EmailAddress, StringLength(256)] public string Email { get; set; } = string.Empty;
    [Required, StringLength(100, MinimumLength = 8), DataType(DataType.Password)] public string Password { get; set; } = string.Empty;
    [Required, Compare(nameof(Password)), DataType(DataType.Password)] public string ConfirmPassword { get; set; } = string.Empty;
    [StringLength(50)] public string? Gender { get; set; }
    [StringLength(100)] public string? GradeOrAgeGroup { get; set; }
}
