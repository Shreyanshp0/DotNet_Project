namespace DotNet_Project.Models;

public enum SupportRequestStatus
{
    Pending = 0,
    Acknowledged = 1,
    Resolved = 2
}

public class SupportRequest
{
    public int Id { get; set; }
    public int ChildProfileId { get; set; }
    public string ChildUserId { get; set; } = string.Empty;
    public string ParentUserId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string? Message { get; set; }
    public SupportRequestStatus Status { get; set; } = SupportRequestStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? AcknowledgedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ParentResponse { get; set; }

    public ChildProfile ChildProfile { get; set; } = null!;
    public ApplicationUser ChildUser { get; set; } = null!;
    public ApplicationUser ParentUser { get; set; } = null!;
}
