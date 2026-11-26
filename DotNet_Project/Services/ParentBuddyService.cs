using DotNet_Project.Data;
using DotNet_Project.DTOs;
using DotNet_Project.Models;
using Microsoft.EntityFrameworkCore;

namespace DotNet_Project.Services;

public interface IParentBuddyService
{
    Task<ChatSendMessageResponse> SendMessageAsync(string parentUserId, string message, string? conversationId);
    Task<ChatHistoryResponse> GetHistoryAsync(string parentUserId, string conversationId);
    Task<List<ChatConversationSummaryDto>> GetConversationsAsync(string parentUserId);
}

public class ParentBuddyService(ApplicationDbContext db, IGeminiAiService geminiService) : IParentBuddyService
{
    public async Task<ChatSendMessageResponse> SendMessageAsync(string parentUserId, string message, string? conversationId)
    {
        var parentUser = await db.Users.FirstOrDefaultAsync(u => u.Id == parentUserId)
            ?? throw new KeyNotFoundException("Parent user not found.");

        var activeConvId = string.IsNullOrWhiteSpace(conversationId)
            ? $"pconv-{Guid.NewGuid():N}"
            : conversationId.Trim();

        var parentMsg = new ParentChatMessage
        {
            ParentUserId = parentUserId,
            ConversationId = activeConvId,
            Sender = "parent",
            Message = message.Trim(),
            CreatedAt = DateTime.UtcNow
        };
        db.ParentChatMessages.Add(parentMsg);
        await db.SaveChangesAsync();

        var recentHistory = await db.ParentChatMessages
            .Where(m => m.ParentUserId == parentUserId && m.ConversationId == activeConvId && m.Id != parentMsg.Id)
            .OrderByDescending(m => m.CreatedAt)
            .Take(6)
            .ToListAsync();
        recentHistory.Reverse();

        var replyText = await geminiService.GenerateParentBuddyReplyAsync(parentUser, message.Trim(), recentHistory);

        var buddyMsg = new ParentChatMessage
        {
            ParentUserId = parentUserId,
            ConversationId = activeConvId,
            Sender = "buddy",
            Message = replyText,
            CreatedAt = DateTime.UtcNow.AddSeconds(1)
        };
        db.ParentChatMessages.Add(buddyMsg);
        await db.SaveChangesAsync();

        return new ChatSendMessageResponse
        {
            ConversationId = activeConvId,
            UserMessage = new ChatMessageDto
            {
                Id = parentMsg.Id,
                Sender = parentMsg.Sender,
                Message = parentMsg.Message,
                ConversationId = parentMsg.ConversationId,
                CreatedAt = parentMsg.CreatedAt
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

    public async Task<ChatHistoryResponse> GetHistoryAsync(string parentUserId, string conversationId)
    {
        var messages = await db.ParentChatMessages
            .Where(m => m.ParentUserId == parentUserId && m.ConversationId == conversationId)
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

    public async Task<List<ChatConversationSummaryDto>> GetConversationsAsync(string parentUserId)
    {
        var messages = await db.ParentChatMessages
            .Where(m => m.ParentUserId == parentUserId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();

        return messages
            .GroupBy(m => m.ConversationId)
            .Select(g =>
            {
                var ordered = g.OrderBy(x => x.CreatedAt).ToList();
                var firstParent = ordered.FirstOrDefault(x => x.Sender == "parent") ?? ordered.First();
                var preview = firstParent.Message.Length > 60
                    ? firstParent.Message[..57] + "..."
                    : firstParent.Message;

                return new ChatConversationSummaryDto
                {
                    ConversationId = g.Key,
                    LastMessagePreview = preview,
                    MessageCount = ordered.Count,
                    StartedAt = ordered.First().CreatedAt,
                    LastMessageAt = ordered.Last().CreatedAt
                };
            })
            .OrderByDescending(c => c.LastMessageAt)
            .ToList();
    }
}
