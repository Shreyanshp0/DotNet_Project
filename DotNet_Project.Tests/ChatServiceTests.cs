using DotNet_Project.Services;

namespace DotNet_Project.Tests;

public class ChatServiceTests
{
    [Fact]
    public async Task SendMessageAsync_PersistsUserAndBuddyMessage_ReturnsEmpatheticReply()
    {
        var (db, conn, parent, child, profile) = await TestDbContextFactory.CreateSeededTestContextAsync();
        using var _ = conn;
        using var __ = db;

        var gemini = TestGeminiFactory.CreateTestService();
        var service = new ChatService(db, gemini);
        var result = await service.SendMessageAsync(profile.Id, "I feel stressed about my school exams", "conv-test");

        Assert.NotNull(result);
        Assert.Equal("conv-test", result.ConversationId);
        Assert.Equal("child", result.UserMessage.Sender);
        Assert.Equal("buddy", result.BuddyReply.Sender);
        Assert.NotEmpty(result.BuddyReply.Message);

        var history = await service.GetHistoryAsync(profile.Id, "conv-test");
        Assert.Equal(2, history.Messages.Count);
    }

    [Fact]
    public async Task SendMessageAsync_WhenSafetyKeywordsDetected_ProvidesSafetyResources()
    {
        var (db, conn, parent, child, profile) = await TestDbContextFactory.CreateSeededTestContextAsync();
        using var _ = conn;
        using var __ = db;

        var gemini = TestGeminiFactory.CreateTestService();
        var service = new ChatService(db, gemini);
        var result = await service.SendMessageAsync(profile.Id, "I want to hurt myself", null);

        Assert.NotNull(result);
        Assert.Contains("safety", result.BuddyReply.Message);
        Assert.Contains("988", result.BuddyReply.Message);
    }
}
