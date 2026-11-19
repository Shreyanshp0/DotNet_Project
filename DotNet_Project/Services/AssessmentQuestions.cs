namespace DotNet_Project.Services;

public record QuestionDefinition(int Id, string Text, string Category, bool Inverted);

public static class AssessmentQuestions
{
    public static readonly string[] ValidAnswers = ["Never", "Rarely", "Sometimes", "Often", "Always"];

    public static readonly Dictionary<int, QuestionDefinition> Definitions = new()
    {
        [1] = new(1, "Do you feel happy most of the time?", "Emotional well-being", false),
        [2] = new(2, "Do you feel able to talk about your feelings?", "Emotional well-being", false),
        [3] = new(3, "Do you feel stressed about schoolwork?", "Academic stress", true),
        [4] = new(4, "Do you feel able to focus when you study?", "Academic stress", false),
        [5] = new(5, "Do you feel comfortable talking to your friends?", "Social relationships", false),
        [6] = new(6, "Do you feel included by other children?", "Social relationships", false),
        [7] = new(7, "Do you get enough sleep?", "Sleep and lifestyle", false),
        [8] = new(8, "Do you make time for things you enjoy?", "Sleep and lifestyle", false),
        [9] = new(9, "Do you feel confident about yourself?", "Self-confidence", false),
        [10] = new(10, "Do you feel proud when you learn something new?", "Self-confidence", false),
    };

    public static int ScoreAnswer(string answer, bool inverted)
    {
        var rawScore = answer switch
        {
            "Never" => 0,
            "Rarely" => 25,
            "Sometimes" => 50,
            "Often" => 75,
            "Always" => 100,
            _ => 50
        };

        return inverted ? (100 - rawScore) : rawScore;
    }
}
