using DotNet_Project.DTOs;
using DotNet_Project.Services;

namespace DotNet_Project.Tests;

public class JournalServiceTests
{
    [Fact]
    public async Task SubmitAsync_FirstTimeToday_Succeeds()
    {
        var (db, conn, parent, child, profile) = await TestDbContextFactory.CreateSeededTestContextAsync();
        using var _ = conn;
        using var __ = db;

        var service = new JournalService(db);
        var (response, conflict, validation) = await service.SubmitAsync(profile.Id, new JournalSubmitRequest
        {
            BestPart = "Had lunch with friends",
            DifficultPart = "PREFER_NOT_TO_ANSWER",
            DayDescription = "Good day overall",
            Emotion = "Calm"
        });

        Assert.Null(conflict);
        Assert.Null(validation);
        Assert.NotNull(response);
        Assert.True(response.Id > 0);

        var history = await service.GetHistoryAsync(profile.Id);
        Assert.Equal(1, history.TotalCount);
        Assert.Equal("Calm", history.Entries[0].Emotion);
    }

    [Fact]
    public async Task SubmitAsync_SecondTimeToday_ReturnsConflict()
    {
        var (db, conn, parent, child, profile) = await TestDbContextFactory.CreateSeededTestContextAsync();
        using var _ = conn;
        using var __ = db;

        var service = new JournalService(db);
        var req = new JournalSubmitRequest
        {
            BestPart = "Part 1",
            DifficultPart = "Part 2",
            DayDescription = "Part 3"
        };

        var (res1, conflict1, val1) = await service.SubmitAsync(profile.Id, req);
        Assert.NotNull(res1);

        var (res2, conflict2, val2) = await service.SubmitAsync(profile.Id, req);
        Assert.Null(res2);
        Assert.NotNull(conflict2);
        Assert.Contains("already exists", conflict2);
    }

    [Fact]
    public async Task SubmitAsync_WithInvalidEmotion_ReturnsValidationError()
    {
        var (db, conn, parent, child, profile) = await TestDbContextFactory.CreateSeededTestContextAsync();
        using var _ = conn;
        using var __ = db;

        var service = new JournalService(db);
        var (res, conflict, val) = await service.SubmitAsync(profile.Id, new JournalSubmitRequest
        {
            BestPart = "Best",
            DifficultPart = "Difficult",
            DayDescription = "Day",
            Emotion = "Ecstatic"
        });

        Assert.Null(res);
        Assert.NotNull(val);
        Assert.Contains("Emotion must be one of", val);
    }
}
