using DotNet_Project.Models;
using DotNet_Project.Services;

namespace DotNet_Project.Tests;

public class ParentActivityServiceTests
{
    [Fact]
    public async Task GetDailyActivityAsync_OnlyReturnsDataForOwnedChild()
    {
        var (db, conn, parentA, childA, child1) = await TestDbContextFactory.CreateSeededTestContextAsync();
        using var _ = conn;
        using var __ = db;

        var parentB = new ApplicationUser { Id = "parent-B", UserName = "b@test.com", Email = "b@test.com", FullName = "Parent B" };
        var childUserB = new ApplicationUser { Id = "child-B", UserName = "cb@test.com", Email = "cb@test.com", FullName = "Robin" };
        db.Users.AddRange(parentB, childUserB);

        var child2 = new ChildProfile
        {
            Id = 999,
            ChildUserId = childUserB.Id,
            ParentUserId = parentB.Id,
            ChildName = "Robin",
            DateOfBirth = new DateOnly(2015, 2, 2)
        };
        db.ChildProfiles.Add(child2);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        db.MoodEntries.Add(new MoodEntry
        {
            ChildProfileId = child1.Id,
            Mood = "Great",
            Reason = "Art club",
            CreatedAt = DateTime.UtcNow
        });
        db.JournalEntries.Add(new JournalEntry
        {
            ChildProfileId = child1.Id,
            EntryDate = today,
            BestPart = "Lunch",
            DifficultPart = "None",
            DayDescription = "Okay",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var service = new ParentActivityService(db);

        // Parent A accesses Child 1 (Authorized)
        var activityA = await service.GetDailyActivityAsync(parentA.Id, child1.Id, today);
        Assert.NotNull(activityA);
        Assert.Equal(child1.ChildName, activityA.ChildName);
        Assert.Single(activityA.MoodEntries);
        Assert.NotNull(activityA.Journal);

        // Parent B attempts to access Child 1 (Forbidden/Unauthorized isolation)
        var activityB = await service.GetDailyActivityAsync(parentB.Id, child1.Id, today);
        Assert.Null(activityB);
    }

    [Fact]
    public async Task GetChildChatHistoryAsync_OnlyReturnsDataForOwnedChild_AndPreservesChronology()
    {
        var (db, conn, parentA, childA, child1) = await TestDbContextFactory.CreateSeededTestContextAsync();
        using var _ = conn;
        using var __ = db;

        var parentB = new ApplicationUser { Id = "parent-B-chat", UserName = "b2@test.com", Email = "b2@test.com", FullName = "Parent B" };
        db.Users.Add(parentB);

        db.ChatMessages.AddRange(
            new ChatMessage
            {
                ChildProfileId = child1.Id,
                ConversationId = "conv-101",
                Sender = "child",
                Message = "Hello Buddy!",
                CreatedAt = DateTime.UtcNow.AddMinutes(-5)
            },
            new ChatMessage
            {
                ChildProfileId = child1.Id,
                ConversationId = "conv-101",
                Sender = "buddy",
                Message = "Hi Alex! How are you feeling today?",
                CreatedAt = DateTime.UtcNow.AddMinutes(-4)
            }
        );
        await db.SaveChangesAsync();

        var service = new ParentActivityService(db);

        // Parent A accesses Child 1 chat history (Authorized)
        var historyA = await service.GetChildChatHistoryAsync(parentA.Id, child1.Id);
        Assert.NotNull(historyA);
        Assert.Equal(child1.ChildName, historyA.ChildName);
        Assert.Equal(2, historyA.Messages.Count);
        Assert.Equal("Hello Buddy!", historyA.Messages[0].Message);
        Assert.Equal("child", historyA.Messages[0].Sender);
        Assert.Equal("conv-101", historyA.Messages[0].ConversationId);

        // Parent B attempts to access Child 1 chat history (Forbidden/Isolation)
        var historyB = await service.GetChildChatHistoryAsync(parentB.Id, child1.Id);
        Assert.Null(historyB);
    }
}
