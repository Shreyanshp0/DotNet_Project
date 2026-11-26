namespace DotNet_Project.Models;

public class ParentChatMessage
{
    public int Id { get; set; }
    public string ParentUserId { get; set; } = string.Empty;
    public string ConversationId { get; set; } = string.Empty;
    public string Sender { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ApplicationUser ParentUser { get; set; } = null!;
}
