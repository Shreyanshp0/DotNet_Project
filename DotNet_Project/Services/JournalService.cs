using DotNet_Project.Data;
using DotNet_Project.DTOs;
using DotNet_Project.Models;
using Microsoft.EntityFrameworkCore;

namespace DotNet_Project.Services;

public interface IJournalService
{
    Task<(JournalSubmitResponse? Response, string? ConflictError, string? ValidationError)> SubmitAsync(int childProfileId, JournalSubmitRequest request);
    Task<JournalHistoryResponse> GetHistoryAsync(int childProfileId, int page = 1, int size = 10);
}

public class JournalService(ApplicationDbContext db) : IJournalService
{
    private static readonly HashSet<string> ValidEmotions = new(StringComparer.OrdinalIgnoreCase)
    {
        "Calm", "Happy", "Tired", "Worried"
    };

    public async Task<(JournalSubmitResponse? Response, string? ConflictError, string? ValidationError)> SubmitAsync(int childProfileId, JournalSubmitRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Emotion) && !ValidEmotions.Contains(request.Emotion.Trim()))
        {
            return (null, null, "Emotion must be one of: Calm, Happy, Tired, Worried.");
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var existing = await db.JournalEntries
            .AnyAsync(j => j.ChildProfileId == childProfileId && j.EntryDate == today);

        if (existing)
        {
            return (null, "A journal entry for today already exists.", null);
        }

        var entry = new JournalEntry
        {
            ChildProfileId = childProfileId,
            EntryDate = today,
            BestPart = request.BestPart.Trim(),
            DifficultPart = request.DifficultPart.Trim(),
            DayDescription = request.DayDescription.Trim(),
            HelpRequired = string.IsNullOrWhiteSpace(request.HelpRequired) ? null : request.HelpRequired.Trim(),
            Emotion = string.IsNullOrWhiteSpace(request.Emotion) ? null : request.Emotion.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        db.JournalEntries.Add(entry);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return (null, "A journal entry for today already exists.", null);
        }

        return (new JournalSubmitResponse
        {
            Id = entry.Id,
            EntryDate = entry.EntryDate.ToString("yyyy-MM-dd"),
            CreatedAt = entry.CreatedAt
        }, null, null);
    }

    public async Task<JournalHistoryResponse> GetHistoryAsync(int childProfileId, int page = 1, int size = 10)
    {
        if (page < 1) page = 1;
        if (size < 1) size = 10;
        if (size > 100) size = 100;

        var query = db.JournalEntries
            .Where(j => j.ChildProfileId == childProfileId)
            .OrderByDescending(j => j.EntryDate)
            .ThenByDescending(j => j.CreatedAt);

        var total = await query.CountAsync();
        var entries = await query
            .Skip((page - 1) * size)
            .Take(size)
            .Select(j => new JournalItemDto
            {
                Id = j.Id,
                EntryDate = j.EntryDate.ToString("yyyy-MM-dd"),
                BestPart = j.BestPart,
                DifficultPart = j.DifficultPart,
                DayDescription = j.DayDescription,
                HelpRequired = j.HelpRequired,
                Emotion = j.Emotion,
                CreatedAt = j.CreatedAt
            })
            .ToListAsync();

        return new JournalHistoryResponse
        {
            TotalCount = total,
            Page = page,
            Size = size,
            Entries = entries
        };
    }
}
