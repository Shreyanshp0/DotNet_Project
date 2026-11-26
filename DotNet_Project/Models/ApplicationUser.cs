using Microsoft.AspNetCore.Identity;

namespace DotNet_Project.Models;

public class ApplicationUser : IdentityUser
{
    public bool MustChangePassword { get; set; } = false;
    public string FullName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<ChildProfile> ParentChildProfiles { get; set; } = new List<ChildProfile>();
    public ChildProfile? ChildProfile { get; set; }
}
