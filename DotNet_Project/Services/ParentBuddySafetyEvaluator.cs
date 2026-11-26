namespace DotNet_Project.Services;

public static class ParentBuddySafetyEvaluator
{
    private static readonly string[] ImmediateDangerPhrases =
    [
        "kill themselves", "kill himself", "kill herself", "hurt themselves", "hurt himself",
        "hurt herself", "suicide", "suicidal", "self-harm", "self harm", "overdose",
        "immediate danger", "in danger right now", "abuse", "abusing"
    ];

    private static readonly string[] UnsafeOutputPhrases =
    [
        "i diagnose", "diagnose your child", "prescribe", "take medication",
        "i am a therapist", "replace a therapist", "physical punishment",
        "spank", "hit your child", "humiliate", "punish them severely"
    ];

    public const string ImmediateDangerResponse =
        "Your child's safety is the highest priority. If there is any immediate danger or risk of self-harm, please stay with your child in a safe place and contact a qualified healthcare professional or local emergency services right away—in India, you can call 112 (or Childline 1098). I am an AI informational assistant and cannot assess emergencies or replace professional care.";

    public static bool IsImmediateDanger(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) return false;
        var lower = message.Trim().ToLowerInvariant();
        foreach (var phrase in ImmediateDangerPhrases)
        {
            if (lower.Contains(phrase, StringComparison.Ordinal)) return true;
        }
        return false;
    }

    public static string EnsureSafeReply(string? candidateReply)
    {
        if (string.IsNullOrWhiteSpace(candidateReply))
        {
            return "Thank you for asking. Gentle, open-ended listening—without rushing to fix or judge—often helps children feel safe opening up. If a concern feels persistent or serious, reaching out to a qualified pediatrician, school counselor, or child psychologist is always a good step.";
        }

        var trimmed = candidateReply.Trim();
        var lower = trimmed.ToLowerInvariant();
        foreach (var unsafePhrase in UnsafeOutputPhrases)
        {
            if (lower.Contains(unsafePhrase, StringComparison.Ordinal))
            {
                return "Every child's situation is unique, and I'm an informational parenting companion rather than a clinician. Focusing on calm, respectful communication and open listening is a helpful starting point, and a qualified professional can offer personalized support if concerns persist.";
            }
        }

        return trimmed;
    }
}
