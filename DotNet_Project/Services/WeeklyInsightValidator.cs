using System.Text.Json;
using DotNet_Project.DTOs;

namespace DotNet_Project.Services;

public static class WeeklyInsightValidator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static bool TryParseAndValidate(string? rawJson, out WeeklyInsightAiResult? result, out string? error)
    {
        result = null;
        error = null;

        if (string.IsNullOrWhiteSpace(rawJson))
        {
            error = "Empty weekly insight response.";
            return false;
        }

        var cleaned = rawJson.Trim();
        if (cleaned.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewline = cleaned.IndexOf('\n');
            if (firstNewline >= 0) cleaned = cleaned[(firstNewline + 1)..];
            if (cleaned.EndsWith("```", StringComparison.Ordinal)) cleaned = cleaned[..^3];
            cleaned = cleaned.Trim();
        }

        WeeklyInsightAiResult? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<WeeklyInsightAiResult>(cleaned, JsonOptions);
        }
        catch (JsonException)
        {
            error = "Malformed JSON response.";
            return false;
        }

        if (parsed is null || string.IsNullOrWhiteSpace(parsed.Summary))
        {
            error = "Summary is required.";
            return false;
        }

        var observations = (parsed.Observations ?? new List<string>())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .Take(6)
            .ToList();

        var areas = (parsed.AreasForSupport ?? new List<string>())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .Take(4)
            .ToList();

        var limitations = (parsed.DataLimitations ?? new List<string>())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .Take(4)
            .ToList();

        var recommendations = new List<WeeklyRecommendationItem>();
        foreach (var rec in (parsed.Recommendations ?? new List<WeeklyRecommendationItem>()).Take(3))
        {
            if (string.IsNullOrWhiteSpace(rec.Activity) || string.IsNullOrWhiteSpace(rec.Reason))
                continue;

            var (canonicalName, url) = MapActivity(rec.Activity.Trim());
            recommendations.Add(new WeeklyRecommendationItem
            {
                Activity = canonicalName,
                Reason = rec.Reason.Trim(),
                ActivityUrl = url
            });
        }

        result = new WeeklyInsightAiResult
        {
            Summary = parsed.Summary.Trim(),
            Observations = observations,
            AreasForSupport = areas,
            Recommendations = recommendations,
            DataLimitations = limitations
        };
        return true;
    }

    public static (string Name, string Url) MapActivity(string activityName)
    {
        var lower = activityName.ToLowerInvariant();
        if (lower.Contains("ground"))
        {
            return ("5-4-3-2-1 Grounding", "/Child/Grounding");
        }
        if (lower.Contains("affirm"))
        {
            return ("Positive Affirmations", "/Child/Affirmations");
        }
        if (lower.Contains("no hold") || lower.Contains("without hold") || lower.Contains("comfortable") || lower.Contains("gentle"))
        {
            return ("Comfortable Breathing (No Holds)", "/Child/Breathing");
        }
        if (lower.Contains("breath"))
        {
            return ("Box Breathing", "/Child/Breathing");
        }
        return ("Relaxation Corner", "/Child/Relaxation");
    }
}
