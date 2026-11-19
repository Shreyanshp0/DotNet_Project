using DotNet_Project.DTOs;
using DotNet_Project.Services;

namespace DotNet_Project.Tests;

public class MoodServiceTests
{
    [Fact]
    public async Task CheckInAsync_WithValidMood_RecordsMoodSuccessfully()
    {
        var (db, conn, parent, child, profile) = await TestDbContextFactory.CreateSeededTestContextAsync();
        using var _ = conn;
        using var __ = db;

        var service = new MoodService(db);
        var (response, error) = await service.CheckInAsync(profile.Id, new MoodCheckInRequest
        {
            Mood = "Great",
            Reason = "Had fun in art class"
        });

        Assert.Null(error);
        Assert.NotNull(response);
        Assert.Equal("Great", response.Mood);
        Assert.Equal("Had fun in art class", response.Reason);

        var todayEntries = await service.GetTodayEntriesAsync(profile.Id);
        Assert.Single(todayEntries.Entries);
        Assert.Equal("Great", todayEntries.Entries[0].Mood);
    }

    [Fact]
    public async Task CheckInAsync_WithInvalidMood_ReturnsValidationError()
    {
        var (db, conn, parent, child, profile) = await TestDbContextFactory.CreateSeededTestContextAsync();
        using var _ = conn;
        using var __ = db;

        var service = new MoodService(db);
        var (response, error) = await service.CheckInAsync(profile.Id, new MoodCheckInRequest
        {
            Mood = "SuperAngry"
        });

        Assert.NotNull(error);
        Assert.Null(response);
        Assert.Contains("Mood must be one of", error);
    }
}
