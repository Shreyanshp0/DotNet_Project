using DotNet_Project.Data;
using DotNet_Project.DTOs;
using DotNet_Project.Models;
using Microsoft.EntityFrameworkCore;

namespace DotNet_Project.Services;

public interface IChatService
{
    Task<ChatSendMessageResponse> SendMessageAsync(int childProfileId, string message, string? conversationId);
    Task<ChatHistoryResponse> GetHistoryAsync(int childProfileId, string conversationId);
}

public class ChatService(ApplicationDbContext db, IGeminiAiService geminiService) : IChatService
{
    public async Task<ChatSendMessageResponse> SendMessageAsync(int childProfileId, string message, string? conversationId)
    {
        var childProfile = await db.ChildProfiles.FirstOrDefaultAsync(c => c.Id == childProfileId);
        if (childProfile is null)
        {
            throw new KeyNotFoundException($"Child profile with ID {childProfileId} was not found.");
        }

        var activeConvId = string.IsNullOrWhiteSpace(conversationId)
            ? $"conv-{Guid.NewGuid():N}"
            : conversationId.Trim();

        var userMsg = new ChatMessage
        {
            ChildProfileId = childProfileId,
            ConversationId = activeConvId,
            Sender = "child",
            Message = message.Trim(),
            CreatedAt = DateTime.UtcNow
        };
        db.ChatMessages.Add(userMsg);
        await db.SaveChangesAsync();

        var recentHistory = await db.ChatMessages
            .Where(m => m.ChildProfileId == childProfileId && m.ConversationId == activeConvId && m.Id != userMsg.Id)
            .OrderByDescending(m => m.CreatedAt)
            .Take(6)
            .ToListAsync();
        recentHistory.Reverse();

        var replyText = await geminiService.GenerateBuddyReplyAsync(childProfile, message.Trim(), recentHistory);

        var buddyMsg = new ChatMessage
        {
            ChildProfileId = childProfileId,
            ConversationId = activeConvId,
            Sender = "buddy",
            Message = replyText,
            CreatedAt = DateTime.UtcNow.AddSeconds(1)
        };
        db.ChatMessages.Add(buddyMsg);
        await db.SaveChangesAsync();

        return new ChatSendMessageResponse
        {
            ConversationId = activeConvId,
            UserMessage = new ChatMessageDto
            {
                Id = userMsg.Id,
                Sender = userMsg.Sender,
                Message = userMsg.Message,
                ConversationId = userMsg.ConversationId,
                CreatedAt = userMsg.CreatedAt
            },
            BuddyReply = new ChatMessageDto
            {
                Id = buddyMsg.Id,
                Sender = buddyMsg.Sender,
                Message = buddyMsg.Message,
                ConversationId = buddyMsg.ConversationId,
                CreatedAt = buddyMsg.CreatedAt
            }
        };
    }

    public async Task<ChatHistoryResponse> GetHistoryAsync(int childProfileId, string conversationId)
    {
        var messages = await db.ChatMessages
            .Where(m => m.ChildProfileId == childProfileId && m.ConversationId == conversationId)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new ChatMessageDto
            {
                Id = m.Id,
                Sender = m.Sender,
                Message = m.Message,
                ConversationId = m.ConversationId,
                CreatedAt = m.CreatedAt
            })
            .ToListAsync();

        return new ChatHistoryResponse
        {
            ConversationId = conversationId,
            Messages = messages
        };
    }
}
