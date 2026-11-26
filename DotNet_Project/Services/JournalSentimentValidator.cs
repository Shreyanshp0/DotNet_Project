using System.Text.Json;
using DotNet_Project.DTOs;

namespace DotNet_Project.Services;

public static class JournalSentimentValidator
{
    public static readonly IReadOnlyDictionary<string, string> AllowedSentiments =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Positive"] = "Positive",
            ["Neutral"] = "Neutral",
            ["Negative"] = "Negative",
            ["Mixed"] = "Mixed"
        };

    public static readonly IReadOnlyDictionary<string, string> AllowedEmotions =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Happiness"] = "Happiness",
            ["Sadness"] = "Sadness",
            ["Stress"] = "Stress",
            ["Anger"] = "Anger",
            ["Worry"] = "Worry",
            ["Fear"] = "Fear",
            ["Loneliness"] = "Loneliness",
            ["Excitement"] = "Excitement",
            ["Frustration"] = "Frustration",
            ["Gratitude"] = "Gratitude"
        };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static bool TryParseAndValidate(string? rawJson, out JournalSentimentResult? result, out string? error)
    {
        result = null;
        error = null;

        if (string.IsNullOrWhiteSpace(rawJson))
        {
            error = "Empty sentiment response.";
            return false;
        }

        var cleaned = rawJson.Trim();
        if (cleaned.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewline = cleaned.IndexOf('\n');
            if (firstNewline >= 0)
            {
                cleaned = cleaned[(firstNewline + 1)..];
            }
            if (cleaned.EndsWith("```", StringComparison.Ordinal))
            {
                cleaned = cleaned[..^3];
            }
            cleaned = cleaned.Trim();
        }

        JournalSentimentResult? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<JournalSentimentResult>(cleaned, JsonOptions);
        }
        catch (JsonException)
        {
            error = "Malformed JSON response.";
            return false;
        }

        if (parsed is null)
        {
            error = "Null sentiment payload.";
            return false;
        }

        var sentimentKey = parsed.Sentiment?.Trim() ?? string.Empty;
        if (!AllowedSentiments.TryGetValue(sentimentKey, out var canonicalSentiment))
        {
            error = $"Invalid sentiment category: '{sentimentKey}'.";
            return false;
        }

        var canonicalEmotions = new List<string>();
        if (parsed.Emotions is not null)
        {
            foreach (var rawEmotion in parsed.Emotions)
            {
                var emotionKey = rawEmotion?.Trim() ?? string.Empty;
                if (string.IsNullOrEmpty(emotionKey) || !AllowedEmotions.TryGetValue(emotionKey, out var canonicalEmotion))
                {
                    error = $"Invalid emotion indicator: '{emotionKey}'.";
                    return false;
                }
                if (!canonicalEmotions.Contains(canonicalEmotion, StringComparer.Ordinal))
                {
                    canonicalEmotions.Add(canonicalEmotion);
                }
            }
        }

        var insight = parsed.Insight?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(insight))
        {
            error = "Insight cannot be empty.";
            return false;
        }

        if (insight.Length > 800)
        {
            insight = insight[..797] + "...";
        }

        result = new JournalSentimentResult
        {
            Sentiment = canonicalSentiment,
            Emotions = canonicalEmotions,
            Insight = insight
        };
        return true;
    }
}
