using System.Text;
using System.Text.Json;
using DotNet_Project.Data;
using DotNet_Project.DTOs;
using DotNet_Project.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNet_Project.Services;

public interface IJournalSentimentService
{
    Task<JournalSentimentAnalysis?> AnalyzeAndSaveAsync(int journalEntryId, CancellationToken cancellationToken = default);
    Task<List<JournalInsightDto>> GetInsightsForChildAsync(string parentUserId, int childProfileId);
}

public class JournalSentimentService(
    ApplicationDbContext db,
    IGeminiAiService geminiAiService,
    ILogger<JournalSentimentService> logger) : IJournalSentimentService
{
    public async Task<JournalSentimentAnalysis?> AnalyzeAndSaveAsync(int journalEntryId, CancellationToken cancellationToken = default)
    {
        var entry = await db.JournalEntries
            .Include(j => j.SentimentAnalysis)
            .FirstOrDefaultAsync(j => j.Id == journalEntryId, cancellationToken);

        if (entry is null)
        {
            return null;
        }

        var journalText = BuildAnalysisText(entry);
        var result = await geminiAiService.AnalyzeJournalSentimentAsync(journalText, cancellationToken);
        if (result is null)
        {
            return null;
        }

        var emotionsJson = JsonSerializer.Serialize(result.Emotions);
        var now = DateTime.UtcNow;

        if (entry.SentimentAnalysis is not null)
        {
            entry.SentimentAnalysis.Sentiment = result.Sentiment;
            entry.SentimentAnalysis.EmotionIndicators = emotionsJson;
            entry.SentimentAnalysis.AIInsight = result.Insight;
            entry.SentimentAnalysis.AnalyzedAt = now;
        }
        else
        {
            var analysis = new JournalSentimentAnalysis
            {
                JournalEntryId = entry.Id,
                Sentiment = result.Sentiment,
                EmotionIndicators = emotionsJson,
                AIInsight = result.Insight,
                AnalyzedAt = now
            };
            db.JournalSentimentAnalyses.Add(analysis);
            entry.SentimentAnalysis = analysis;
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return entry.SentimentAnalysis;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to persist sentiment analysis for journal entry {JournalEntryId}.", journalEntryId);
            return null;
        }
    }

    public async Task<List<JournalInsightDto>> GetInsightsForChildAsync(string parentUserId, int childProfileId)
    {
        var ownsChild = await db.ChildProfiles
            .AnyAsync(c => c.Id == childProfileId && c.ParentUserId == parentUserId);

        if (!ownsChild)
        {
            return new List<JournalInsightDto>();
        }

        var entries = await db.JournalEntries
            .Include(j => j.SentimentAnalysis)
            .Where(j => j.ChildProfileId == childProfileId)
            .OrderByDescending(j => j.EntryDate)
            .ThenByDescending(j => j.CreatedAt)
            .ToListAsync();

        return entries.Select(j =>
        {
            var emotions = new List<string>();
            if (j.SentimentAnalysis is not null && !string.IsNullOrWhiteSpace(j.SentimentAnalysis.EmotionIndicators))
            {
                try
                {
                    emotions = JsonSerializer.Deserialize<List<string>>(j.SentimentAnalysis.EmotionIndicators) ?? new List<string>();
                }
                catch (JsonException)
                {
                    emotions = new List<string>();
                }
            }

            return new JournalInsightDto
            {
                JournalEntryId = j.Id,
                EntryDate = j.EntryDate.ToString("yyyy-MM-dd"),
                ChildSelfEmotion = j.Emotion,
                HasAnalysis = j.SentimentAnalysis is not null,
                Sentiment = j.SentimentAnalysis?.Sentiment,
                EmotionIndicators = emotions,
                AIInsight = j.SentimentAnalysis?.AIInsight,
                AnalyzedAt = j.SentimentAnalysis?.AnalyzedAt
            };
        }).ToList();
    }

    private static string BuildAnalysisText(JournalEntry entry)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Best part of the day: {entry.BestPart}");
        sb.AppendLine($"Difficult part of the day: {entry.DifficultPart}");
        sb.AppendLine($"Overall day description: {entry.DayDescription}");
        if (!string.IsNullOrWhiteSpace(entry.HelpRequired))
        {
            sb.AppendLine($"Help requested: {entry.HelpRequired}");
        }
        if (!string.IsNullOrWhiteSpace(entry.Emotion))
        {
            sb.AppendLine($"Selected feeling: {entry.Emotion}");
        }
        return sb.ToString();
    }
}
