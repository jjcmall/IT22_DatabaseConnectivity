using System;
using System.Text;
using UnityEngine;

namespace CodeEscape.Programming
{
    public enum QuestionType
    {
        MultipleChoice = 0,
        PredictOutput = 1,
        FillMissingCode = 2,
        FixIncorrectCode = 3,
        ChooseCorrectCode = 4
    }

    public enum QuestionDifficulty
    {
        Easy = 0,
        Medium = 1,
        Hard = 2
    }

    /// <summary>How the player answers: by picking one of the choices, or by typing a short answer.</summary>
    public enum AnswerFormat
    {
        Choices = 0,
        TypedAnswer = 1
    }

    /// <summary>
    /// One predefined programming question. Answers are only compared with the stored correct answer:
    /// player input is never compiled or executed, and SQL questions use fictional sample tables.
    /// </summary>
    [CreateAssetMenu(fileName = "Question", menuName = "Code Escape/Question Data")]
    public class QuestionData : ScriptableObject
    {
        public const int MaxChoices = 4;

        [Header("Identity")]
        [SerializeField, Tooltip("Unique ID, e.g. PY_01_VARIABLES.")]
        string id;
        [SerializeField] ProgrammingLanguage language = ProgrammingLanguage.Python;
        [SerializeField, Tooltip("Beginner topic, e.g. Variables, Loops, SELECT.")]
        string topic;
        [SerializeField] QuestionDifficulty difficulty;
        [SerializeField] QuestionType questionType;

        [Header("Question")]
        [SerializeField, TextArea(2, 5)] string questionText;
        [SerializeField, TextArea(3, 14), Tooltip("Optional code snippet or sample table, shown in a monospaced box.")]
        string code;

        [Header("Answer")]
        [SerializeField] AnswerFormat answerFormat = AnswerFormat.Choices;
        [SerializeField, TextArea(1, 4), Tooltip("Choices format: 2 to 4 choices.")]
        string[] choices = new string[0];
        [SerializeField, Tooltip("Choices format: index of the correct choice (0 = first).")]
        int correctChoiceIndex;
        [SerializeField, Tooltip("Show the choices in a monospaced code font.")]
        bool monospaceChoices;
        [SerializeField, Tooltip("Typed format: every accepted answer. Spacing differences are ignored.")]
        string[] acceptedAnswers = new string[0];
        [SerializeField, Tooltip("Typed format: must upper/lower case match exactly?")]
        bool caseSensitive = true;

        [Header("Feedback")]
        [SerializeField, TextArea(2, 6)] string explanation;
        [SerializeField, TextArea(1, 4)] string hint;

        public string Id => id;
        public ProgrammingLanguage Language => language;
        public string Topic => topic;
        public QuestionDifficulty Difficulty => difficulty;
        public QuestionType QuestionType => questionType;
        public string QuestionText => questionText;
        public string Code => code;
        public AnswerFormat AnswerFormat => answerFormat;
        public int ChoiceCount => choices != null ? choices.Length : 0;
        public int CorrectChoiceIndex => correctChoiceIndex;
        public bool MonospaceChoices => monospaceChoices;
        public string Explanation => explanation;
        public string Hint => hint;
        public bool HasHint => !string.IsNullOrWhiteSpace(hint);

        public string GetChoice(int index)
        {
            return index >= 0 && index < ChoiceCount ? choices[index] : string.Empty;
        }

        public bool IsCorrectChoice(int index)
        {
            return answerFormat == AnswerFormat.Choices && index >= 0 && index < ChoiceCount && index == correctChoiceIndex;
        }

        public bool IsCorrectTypedAnswer(string answer)
        {
            if (answerFormat != AnswerFormat.TypedAnswer || string.IsNullOrWhiteSpace(answer) || acceptedAnswers == null)
                return false;

            string given = NormalizeAnswer(answer);
            StringComparison comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            foreach (string accepted in acceptedAnswers)
            {
                if (!string.IsNullOrWhiteSpace(accepted) && string.Equals(given, NormalizeAnswer(accepted), comparison))
                    return true;
            }
            return false;
        }

        /// <summary>Trims, collapses runs of whitespace and straightens curly quotes, so formatting isn't marked wrong.</summary>
        public static string NormalizeAnswer(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            var builder = new StringBuilder(text.Length);
            bool pendingSpace = false;
            foreach (char raw in text.Trim())
            {
                char c = raw;
                if (c == '‘' || c == '’') c = '\'';
                else if (c == '“' || c == '”') c = '"';

                if (char.IsWhiteSpace(c))
                {
                    pendingSpace = true;
                    continue;
                }
                if (pendingSpace)
                {
                    builder.Append(' ');
                    pendingSpace = false;
                }
                builder.Append(c);
            }
            return builder.ToString();
        }

        /// <summary>Checks for authoring mistakes. Returns null when the question is valid.</summary>
        public string Validate()
        {
            if (string.IsNullOrWhiteSpace(id)) return "missing ID";
            if (!language.IsValid()) return "language is not set";
            if (string.IsNullOrWhiteSpace(questionText)) return "missing question text";
            if (string.IsNullOrWhiteSpace(explanation)) return "missing explanation";

            if (answerFormat == AnswerFormat.Choices)
            {
                if (ChoiceCount < 2 || ChoiceCount > MaxChoices) return $"needs 2-{MaxChoices} choices (has {ChoiceCount})";
                if (correctChoiceIndex < 0 || correctChoiceIndex >= ChoiceCount) return "correct choice index is out of range";
                for (int i = 0; i < ChoiceCount; i++)
                {
                    if (string.IsNullOrWhiteSpace(choices[i])) return $"choice {i} is empty";
                }
            }
            else
            {
                bool any = false;
                if (acceptedAnswers != null)
                {
                    foreach (string accepted in acceptedAnswers)
                        any |= !string.IsNullOrWhiteSpace(accepted);
                }
                if (!any) return "no accepted typed answers";
            }
            return null;
        }
    }

    public static class QuestionTypeExtensions
    {
        public static string ToDisplayName(this QuestionType type)
        {
            switch (type)
            {
                case QuestionType.PredictOutput: return "Predict the Output";
                case QuestionType.FillMissingCode: return "Fill in the Missing Code";
                case QuestionType.FixIncorrectCode: return "Fix the Incorrect Code";
                case QuestionType.ChooseCorrectCode: return "Choose the Correct Code";
                default: return "Multiple Choice";
            }
        }
    }
}
