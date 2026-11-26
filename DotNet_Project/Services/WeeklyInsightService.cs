using System.Text.Json;
using DotNet_Project.Data;
using DotNet_Project.DTOs;
using DotNet_Project.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNet_Project.Services;

public interface IWeeklyInsightService
{
    DateOnly GetWeekStartInKolkata(DateTime utcNow);
    Task<WeeklyAggregationResult> AggregateWeekAsync(int childProfileId, DateOnly weekStartDate, DateTime? utcNowOverride = null);
    Task<WeeklyInsightViewModel?> GetOrGenerateWeeklyInsightAsync(string parentUserId, int childProfileId, DateOnly? weekStartDate = null, bool forceRegenerate = false, DateTime? utcNowOverride = null);
    Task<List<WeeklyInsightSummaryItem>> GetPreviousWeeksAsync(string parentUserId, int childProfileId);
}

public class WeeklyInsightService(
    ApplicationDbContext db,
    IGeminiAiService geminiAiService,
    ILogger<WeeklyInsightService> logger) : IWeeklyInsightService
{
    public const int MinimumRecordsForPatterns = 3;

    public static TimeZoneInfo GetKolkataTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
        }
        catch (TimeZoneNotFoundException)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.CreateCustomTimeZone("Asia/Kolkata", TimeSpan.FromHours(5.5), "IST", "IST");
            }
        }
    }

    public DateOnly GetWeekStartInKolkata(DateTime utcNow)
    {
        var istZone = GetKolkataTimeZone();
        var istTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), istZone);
        var localDate = DateOnly.FromDateTime(istTime);
        var offset = ((int)localDate.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return localDate.AddDays(-offset);
    }

    public async Task<WeeklyAggregationResult> AggregateWeekAsync(int childProfileId, DateOnly weekStartDate, DateTime? utcNowOverride = null)
    {
        var istZone = GetKolkataTimeZone();
        var mondayOffset = ((int)weekStartDate.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        var monday = weekStartDate.AddDays(-mondayOffset);
        var sunday = monday.AddDays(6);

        var startUtc = TimeZoneInfo.ConvertTimeToUtc(monday.ToDateTime(TimeOnly.MinValue), istZone);
        var endExclusiveUtc = TimeZoneInfo.ConvertTimeToUtc(sunday.AddDays(1).ToDateTime(TimeOnly.MinValue), istZone);

        var nowIst = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(utcNowOverride ?? DateTime.UtcNow, DateTimeKind.Utc),
            istZone);
        var todayIst = DateOnly.FromDateTime(nowIst);
        var isPartialWeek = todayIst <= sunday;

        var moods = await db.MoodEntries
            .Where(m => m.ChildProfileId == childProfileId && m.CreatedAt >= startUtc && m.CreatedAt < endExclusiveUtc)
            .Select(m => m.Mood)
            .ToListAsync();

        var moodCounts = moods
            .GroupBy(m => m)
            .ToDictionary(g => g.Key, g => g.Count());

        var journals = await db.JournalEntries
            .Include(j => j.SentimentAnalysis)
            .Where(j => j.ChildProfileId == childProfileId && j.EntryDate >= monday && j.EntryDate <= sunday)
            .OrderBy(j => j.EntryDate)
            .ToListAsync();

        var sentimentCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var emotionCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var j in journals)
        {
            if (j.SentimentAnalysis is null) continue;
            var s = j.SentimentAnalysis.Sentiment;
            sentimentCounts[s] = sentimentCounts.GetValueOrDefault(s) + 1;

            try
            {
                var emos = JsonSerializer.Deserialize<List<string>>(j.SentimentAnalysis.EmotionIndicators) ?? new();
                foreach (var emo in emos)
                {
                    emotionCounts[emo] = emotionCounts.GetValueOrDefault(emo) + 1;
                }
            }
            catch (JsonException)
            {
                // Ignore malformed emotion arrays
            }
        }

        var relaxations = await db.ExerciseSessions
            .Where(e => e.ChildProfileId == childProfileId && e.WasCompleted && e.CompletedAt >= startUtc && e.CompletedAt < endExclusiveUtc)
            .Select(e => e.ExerciseType)
            .ToListAsync();

        var relaxationCounts = relaxations
            .GroupBy(r => r)
            .ToDictionary(g => g.Key, g => g.Count());

        return new WeeklyAggregationResult
        {
            ChildProfileId = childProfileId,
            WeekStartDate = monday,
            WeekEndDate = sunday,
            IsPartialWeek = isPartialWeek,
            TotalMoodCheckIns = moods.Count,
            MoodCounts = moodCounts,
            TotalJournalEntries = journals.Count,
            JournalDates = journals.Select(j => j.EntryDate).Distinct().ToList(),
            JournalSentimentCounts = sentimentCounts,
            EmotionIndicatorCounts = emotionCounts,
            TotalRelaxationSessions = relaxations.Count,
            RelaxationCounts = relaxationCounts
        };
    }

    public async Task<WeeklyInsightViewModel?> GetOrGenerateWeeklyInsightAsync(
        string parentUserId,
        int childProfileId,
        DateOnly? weekStartDate = null,
        bool forceRegenerate = false,
        DateTime? utcNowOverride = null)
    {
        var child = await db.ChildProfiles
            .FirstOrDefaultAsync(c => c.Id == childProfileId && c.ParentUserId == parentUserId);

        if (child is null) return null;

        var targetWeekStart = weekStartDate ?? GetWeekStartInKolkata(utcNowOverride ?? DateTime.UtcNow);
        var aggregation = await AggregateWeekAsync(child.Id, targetWeekStart, utcNowOverride);
        var previousWeeks = await GetPreviousWeeksAsync(parentUserId, child.Id);

        // Enforce minimum data sufficiency rules in application logic before Gemini
        if (aggregation.TotalEmotionalRecords == 0)
        {
            return BuildEmptyViewModel(child, aggregation, previousWeeks);
        }

        if (aggregation.TotalEmotionalRecords < MinimumRecordsForPatterns)
        {
            return await UpsertInsufficientDataInsightAsync(child, aggregation, previousWeeks);
        }

        var existing = await db.WeeklyInsights
            .FirstOrDefaultAsync(w => w.ChildProfileId == child.Id && w.WeekStartDate == aggregation.WeekStartDate);

        if (existing is not null && !forceRegenerate)
        {
            return MapEntityToViewModel(existing, child, aggregation, previousWeeks, usedAi: true);
        }

        var aiResult = await geminiAiService.GenerateWeeklyInsightAsync(aggregation);
        var finalResult = aiResult ?? BuildDeterministicSummary(aggregation);
        AppendSystemLimitations(finalResult.DataLimitations, aggregation);

        if (existing is null)
        {
            existing = new WeeklyInsight
            {
                ChildProfileId = child.Id,
                WeekStartDate = aggregation.WeekStartDate,
                WeekEndDate = aggregation.WeekEndDate
            };
            db.WeeklyInsights.Add(existing);
        }

        existing.Summary = finalResult.Summary;
        existing.ObservationsJson = JsonSerializer.Serialize(finalResult.Observations);
        existing.AreasForSupportJson = JsonSerializer.Serialize(finalResult.AreasForSupport);
        existing.RecommendationsJson = JsonSerializer.Serialize(finalResult.Recommendations);
        existing.DataLimitationsJson = JsonSerializer.Serialize(finalResult.DataLimitations);
        existing.GeneratedAt = DateTime.UtcNow;

        try
        {
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to save weekly insight for child profile {ChildProfileId}.", child.Id);
        }

        previousWeeks = await GetPreviousWeeksAsync(parentUserId, child.Id);
        return MapEntityToViewModel(existing, child, aggregation, previousWeeks, usedAi: aiResult is not null);
    }

    public async Task<List<WeeklyInsightSummaryItem>> GetPreviousWeeksAsync(string parentUserId, int childProfileId)
    {
        var owns = await db.ChildProfiles.AnyAsync(c => c.Id == childProfileId && c.ParentUserId == parentUserId);
        if (!owns) return new List<WeeklyInsightSummaryItem>();

        return await db.WeeklyInsights
            .Where(w => w.ChildProfileId == childProfileId)
            .OrderByDescending(w => w.WeekStartDate)
            .Select(w => new WeeklyInsightSummaryItem
            {
                WeekStartDate = w.WeekStartDate,
                WeekEndDate = w.WeekEndDate,
                GeneratedAt = w.GeneratedAt
            })
            .ToListAsync();
    }

    private async Task<WeeklyInsightViewModel> UpsertInsufficientDataInsightAsync(
        ChildProfile child,
        WeeklyAggregationResult aggregation,
        List<WeeklyInsightSummaryItem> previousWeeks)
    {
        var limitations = new List<string>
        {
            $"Only {aggregation.TotalEmotionalRecords} check-in or journal record(s) were recorded this week, which is not enough to identify recurring weekly patterns."
        };
        AppendSystemLimitations(limitations, aggregation);

        var observations = new List<string>();
        if (aggregation.TotalMoodCheckIns > 0)
            observations.Add($"{aggregation.TotalMoodCheckIns} mood check-in(s) recorded this week.");
        if (aggregation.TotalJournalEntries > 0)
            observations.Add($"{aggregation.TotalJournalEntries} journal entry(ies) recorded this week.");

        var summary = "There is currently insufficient data for meaningful weekly patterns. Isolated check-ins are shown as counts without inferring trends.";

        var existing = await db.WeeklyInsights
            .FirstOrDefaultAsync(w => w.ChildProfileId == child.Id && w.WeekStartDate == aggregation.WeekStartDate);

        if (existing is null)
        {
            existing = new WeeklyInsight
            {
                ChildProfileId = child.Id,
                WeekStartDate = aggregation.WeekStartDate,
                WeekEndDate = aggregation.WeekEndDate
            };
            db.WeeklyInsights.Add(existing);
        }

        existing.Summary = summary;
        existing.ObservationsJson = JsonSerializer.Serialize(observations);
        existing.AreasForSupportJson = "[]";
        existing.RecommendationsJson = "[]";
        existing.DataLimitationsJson = JsonSerializer.Serialize(limitations);
        existing.GeneratedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        previousWeeks = await GetPreviousWeeksAsync(child.ParentUserId, child.Id);

        var vm = MapEntityToViewModel(existing, child, aggregation, previousWeeks, usedAi: false);
        vm.HasSufficientData = false;
        return vm;
    }

    private static WeeklyInsightViewModel BuildEmptyViewModel(
        ChildProfile child,
        WeeklyAggregationResult aggregation,
        List<WeeklyInsightSummaryItem> previousWeeks)
    {
        var limitations = new List<string> { "No mood check-ins or journal entries were recorded during this week." };
        AppendSystemLimitations(limitations, aggregation);

        return new WeeklyInsightViewModel
        {
            ChildProfileId = child.Id,
            ChildName = child.ChildName,
            WeekStartDate = aggregation.WeekStartDate,
            WeekEndDate = aggregation.WeekEndDate,
            IsPartialWeek = aggregation.IsPartialWeek,
            HasData = false,
            HasSufficientData = false,
            UsedAiGeneration = false,
            TotalMoodCheckIns = aggregation.TotalMoodCheckIns,
            TotalJournalEntries = aggregation.TotalJournalEntries,
            TotalRelaxationSessions = aggregation.TotalRelaxationSessions,
            MoodCounts = aggregation.MoodCounts,
            JournalSentimentCounts = aggregation.JournalSentimentCounts,
            Summary = "No mood check-ins or journal entries have been recorded for this week yet.",
            DataLimitations = limitations,
            PreviousWeeks = previousWeeks
        };
    }

    private static WeeklyInsightAiResult BuildDeterministicSummary(WeeklyAggregationResult aggregation)
    {
        var observations = new List<string>();
        if (aggregation.TotalMoodCheckIns > 0)
        {
            var moodSummary = string.Join(", ", aggregation.MoodCounts.Select(kv => $"{kv.Key}: {kv.Value}"));
            observations.Add($"Recorded {aggregation.TotalMoodCheckIns} mood check-in(s) ({moodSummary}).");
        }
        if (aggregation.TotalJournalEntries > 0)
        {
            var sentSummary = aggregation.JournalSentimentCounts.Count > 0
                ? string.Join(", ", aggregation.JournalSentimentCounts.Select(kv => $"{kv.Key}: {kv.Value}"))
                : "pending sentiment analysis";
            observations.Add($"Submitted {aggregation.TotalJournalEntries} journal entry(ies) ({sentSummary}).");
        }

        return new WeeklyInsightAiResult
        {
            Summary = "This summary reflects the recorded mood check-ins and journal reflections for the selected week.",
            Observations = observations,
            AreasForSupport = new List<string>
            {
                "Consider gentle, open-ended check-ins to hear how the week felt from your child's perspective."
            },
            Recommendations = new List<WeeklyRecommendationItem>
            {
                new()
                {
                    Activity = "Box Breathing",
                    Reason = "An optional short breathing break your child can try anytime in Relaxation Corner.",
                    ActivityUrl = "/Child/Breathing"
                }
            },
            DataLimitations = new List<string>()
        };
    }

    private static void AppendSystemLimitations(List<string> limitations, WeeklyAggregationResult aggregation)
    {
        if (aggregation.IsPartialWeek)
        {
            var msg = "This week is still in progress (partial week), so patterns may change as more days are recorded.";
            if (!limitations.Contains(msg, StringComparer.OrdinalIgnoreCase))
                limitations.Add(msg);
        }
        if (aggregation.TotalJournalEntries == 0 && aggregation.TotalMoodCheckIns > 0)
        {
            var msg = "No journal entries were submitted this week; observations are based only on mood check-ins.";
            if (!limitations.Contains(msg, StringComparer.OrdinalIgnoreCase))
                limitations.Add(msg);
        }
    }

    private static WeeklyInsightViewModel MapEntityToViewModel(
        WeeklyInsight entity,
        ChildProfile child,
        WeeklyAggregationResult aggregation,
        List<WeeklyInsightSummaryItem> previousWeeks,
        bool usedAi)
    {
        var observations = DeserializeList<string>(entity.ObservationsJson);
        var areas = DeserializeList<string>(entity.AreasForSupportJson);
        var recommendations = DeserializeList<WeeklyRecommendationItem>(entity.RecommendationsJson);
        foreach (var rec in recommendations)
        {
            var (_, url) = WeeklyInsightValidator.MapActivity(rec.Activity);
            rec.ActivityUrl = url;
        }
        var limitations = DeserializeList<string>(entity.DataLimitationsJson);

        return new WeeklyInsightViewModel
        {
            ChildProfileId = child.Id,
            ChildName = child.ChildName,
            WeekStartDate = entity.WeekStartDate,
            WeekEndDate = entity.WeekEndDate,
            IsPartialWeek = aggregation.IsPartialWeek,
            HasData = aggregation.TotalEmotionalRecords > 0,
            HasSufficientData = aggregation.TotalEmotionalRecords >= MinimumRecordsForPatterns,
            UsedAiGeneration = usedAi,
            TotalMoodCheckIns = aggregation.TotalMoodCheckIns,
            TotalJournalEntries = aggregation.TotalJournalEntries,
            TotalRelaxationSessions = aggregation.TotalRelaxationSessions,
            MoodCounts = aggregation.MoodCounts,
            JournalSentimentCounts = aggregation.JournalSentimentCounts,
            Summary = entity.Summary,
            Observations = observations,
            AreasForSupport = areas,
            Recommendations = recommendations,
            DataLimitations = limitations,
            GeneratedAt = entity.GeneratedAt,
            PreviousWeeks = previousWeeks
        };
    }

    private static List<T> DeserializeList<T>(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<T>();
        try
        {
            return JsonSerializer.Deserialize<List<T>>(json) ?? new List<T>();
        }
        catch (JsonException)
        {
            return new List<T>();
        }
    }
}
