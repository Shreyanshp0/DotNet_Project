namespace DotNet_Project.Models;

public class ChatMessage
{
    public int Id { get; set; }
    public int ChildProfileId { get; set; }
    public string ConversationId { get; set; } = string.Empty;
    public string Sender { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ChildProfile ChildProfile { get; set; } = null!;
}
