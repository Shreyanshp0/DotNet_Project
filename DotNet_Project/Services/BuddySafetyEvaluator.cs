namespace DotNet_Project.Services;

public enum BuddySafetyTier
{
    Ordinary = 0,
    ConcerningNonImmediate = 1,
    ImmediateDanger = 2
}

public static class BuddySafetyEvaluator
{
    private static readonly string[] ImmediateDangerPhrases =
    [
        "kill myself", "end my life", "want to die", "wanna die", "going to die",
        "suicide", "suicidal", "hurt myself", "harm myself", "cutting myself",
        "cut myself", "self harm", "self-harm", "overdose", "take all the pills",
        "not safe at home", "unsafe at home", "someone is hurting me", "hitting me",
        "immediate danger", "in danger right now", "don't want to live", "dont want to live"
    ];

    private static readonly string[] ConcerningPhrases =
    [
        "hopeless", "worthless", "nobody cares", "no one cares", "everyone hates me",
        "bully", "bullied", "bullying", "scared at school", "afraid to go",
        "crying every day", "can't stop crying", "cant stop crying",
        "panic attack", "overwhelmed and alone", "feel completely alone",
        "hate myself", "depressed", "trapped", "cannot cope", "can't cope"
    ];

    private static readonly string[] UnsafeOutputPhrases =
    [
        "i diagnose", "you have depression", "you have anxiety disorder",
        "prescribe", "take medication", "i am your therapist", "replace a therapist",
        "100% confidential", "promise to keep this secret", "i am monitoring you",
        "emergency services have been alerted", "support request has been sent"
    ];

    public const string ImmediateDangerResponse =
        "I'm really glad you reached out, and your safety matters most right now. Please move to a safe place and tell a trusted adult nearby—like a parent, guardian, teacher, or family member—immediately. If you are in immediate danger, please call India's emergency number 112 (or Childline 1098) right away. I am an AI companion and cannot monitor or protect you in real time, so connecting with a real person nearby is the most important step.";

    public const string ConcerningFallbackResponse =
        "Thank you for sharing something so heavy with me. You shouldn't have to carry these feelings all by yourself—please consider talking with your parent, guardian, teacher, or another trusted adult about what you're going through. You can also use the Support Request option on your Child Dashboard whenever you want to let your parent know you'd like to talk.";

    public static BuddySafetyTier Classify(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) return BuddySafetyTier.Ordinary;
        var lower = message.Trim().ToLowerInvariant();

        foreach (var phrase in ImmediateDangerPhrases)
        {
            if (lower.Contains(phrase, StringComparison.Ordinal))
            {
                return BuddySafetyTier.ImmediateDanger;
            }
        }

        // Single-word high-risk fallback checks when combined with first-person or distress context
        if (lower.Contains("suicide", StringComparison.Ordinal) ||
            (lower.Contains("hurt", StringComparison.Ordinal) && lower.Contains("myself", StringComparison.Ordinal)) ||
            (lower.Contains("die", StringComparison.Ordinal) && (lower.Contains("want", StringComparison.Ordinal) || lower.Contains("wish", StringComparison.Ordinal))))
        {
            return BuddySafetyTier.ImmediateDanger;
        }

        foreach (var phrase in ConcerningPhrases)
        {
            if (lower.Contains(phrase, StringComparison.Ordinal))
            {
                return BuddySafetyTier.ConcerningNonImmediate;
            }
        }

        return BuddySafetyTier.Ordinary;
    }

    public static string EnsureSafeReply(BuddySafetyTier tier, string? candidateReply)
    {
        if (tier == BuddySafetyTier.ImmediateDanger)
        {
            return ImmediateDangerResponse;
        }

        if (string.IsNullOrWhiteSpace(candidateReply))
        {
            return tier == BuddySafetyTier.ConcerningNonImmediate
                ? ConcerningFallbackResponse
                : "I hear you, and thank you for sharing with me. Taking a few slow breaths or talking with a trusted adult can help when things feel like a lot. How else has your day been going?";
        }

        var trimmed = candidateReply.Trim();
        var lower = trimmed.ToLowerInvariant();
        foreach (var unsafePhrase in UnsafeOutputPhrases)
        {
            if (lower.Contains(unsafePhrase, StringComparison.Ordinal))
            {
                return tier == BuddySafetyTier.ConcerningNonImmediate
                    ? ConcerningFallbackResponse
                    : "Thank you for sharing how you feel. Remember I'm an AI listening companion, not a therapist or doctor—if you'd like extra support, talking with a parent, guardian, or trusted adult is always a good step.";
            }
        }

        if (tier == BuddySafetyTier.ConcerningNonImmediate &&
            !lower.Contains("parent", StringComparison.Ordinal) &&
            !lower.Contains("trusted adult", StringComparison.Ordinal) &&
            !lower.Contains("guardian", StringComparison.Ordinal) &&
            !lower.Contains("teacher", StringComparison.Ordinal))
        {
            return $"{trimmed} Whenever you feel ready, sharing this with a parent, guardian, teacher, or another trusted adult can really help.";
        }

        return trimmed;
    }
}
