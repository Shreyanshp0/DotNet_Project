namespace DotNet_Project.ViewModels;

public class ChildCredentialsViewModel
{
    public int ChildProfileId { get; set; }
    public string ChildName { get; set; } = string.Empty;
    public string ChildId { get; set; } = string.Empty;
    public string TemporaryPassword { get; set; } = string.Empty;
    public bool IsPasswordReset { get; set; }
}
