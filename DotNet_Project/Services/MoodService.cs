using DotNet_Project.Data;
using DotNet_Project.DTOs;
using DotNet_Project.Models;
using Microsoft.EntityFrameworkCore;

namespace DotNet_Project.Services;

public interface IMoodService
{
    Task<(MoodResponse? Response, string? Error)> CheckInAsync(int childProfileId, MoodCheckInRequest request);
    Task<MoodTodayResponse> GetTodayEntriesAsync(int childProfileId);
}

public class MoodService(ApplicationDbContext db) : IMoodService
{
    private static readonly HashSet<string> ValidMoods = new(StringComparer.OrdinalIgnoreCase)
    {
        "Great", "Good", "Okay", "Low", "Worried"
    };

    public async Task<(MoodResponse? Response, string? Error)> CheckInAsync(int childProfileId, MoodCheckInRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Mood) || !ValidMoods.Contains(request.Mood.Trim()))
        {
            return (null, "Mood must be one of: Great, Good, Okay, Low, Worried.");
        }

        var entry = new MoodEntry
        {
            ChildProfileId = childProfileId,
            Mood = request.Mood.Trim(),
            Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        db.MoodEntries.Add(entry);
        await db.SaveChangesAsync();

        return (new MoodResponse
        {
            Id = entry.Id,
            Mood = entry.Mood,
            Reason = entry.Reason,
            CreatedAt = entry.CreatedAt
        }, null);
    }

    public async Task<MoodTodayResponse> GetTodayEntriesAsync(int childProfileId)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var startUtc = today.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var endUtc = today.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        var entries = await db.MoodEntries
            .Where(m => m.ChildProfileId == childProfileId && m.CreatedAt >= startUtc && m.CreatedAt <= endUtc)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new MoodResponse
            {
                Id = m.Id,
                Mood = m.Mood,
                Reason = m.Reason,
                CreatedAt = m.CreatedAt
            })
            .ToListAsync();

        return new MoodTodayResponse
        {
            Date = today.ToString("yyyy-MM-dd"),
            Entries = entries
        };
    }
}
