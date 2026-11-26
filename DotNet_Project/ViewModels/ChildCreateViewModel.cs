using System.ComponentModel.DataAnnotations;

namespace DotNet_Project.ViewModels;

public class ChildCreateViewModel
{
    [Required, StringLength(200, MinimumLength = 2)]
    [Display(Name = "Child's full name")]
    public string ChildName { get; set; } = string.Empty;

    [Required, DataType(DataType.Date)]
    [Display(Name = "Date of birth")]
    public DateOnly? DateOfBirth { get; set; }

    [StringLength(50)]
    [Display(Name = "Child ID / Username (optional, auto-generated if blank)")]
    public string? UserName { get; set; }

    [EmailAddress, StringLength(256)]
    [Display(Name = "Login email (optional)")]
    public string? Email { get; set; }

    [StringLength(100, MinimumLength = 8), DataType(DataType.Password)]
    [Display(Name = "Initial password (optional, auto-generated if blank)")]
    public string? Password { get; set; }

    [Compare(nameof(Password), ErrorMessage = "Passwords do not match."), DataType(DataType.Password)]
    [Display(Name = "Confirm password")]
    public string? ConfirmPassword { get; set; }

    [StringLength(50)]
    public string? Gender { get; set; }

    [StringLength(100)]
    [Display(Name = "Grade or age group (optional)")]
    public string? GradeOrAgeGroup { get; set; }
}
