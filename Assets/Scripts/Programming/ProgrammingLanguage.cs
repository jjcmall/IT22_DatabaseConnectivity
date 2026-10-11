namespace CodeEscape.Programming
{
    /// <summary>
    /// The programming language the player studies for the whole playthrough.
    /// Stored as an int in the save data, so never renumber existing values.
    /// </summary>
    public enum ProgrammingLanguage
    {
        None = 0,
        Python = 1,
        PHP = 2,
        SQL = 3
    }

    public static class ProgrammingLanguageExtensions
    {
        /// <summary>True for a real, selectable language (anything except <see cref="ProgrammingLanguage.None"/>).</summary>
        public static bool IsValid(this ProgrammingLanguage language)
        {
            return language == ProgrammingLanguage.Python
                || language == ProgrammingLanguage.PHP
                || language == ProgrammingLanguage.SQL;
        }

        /// <summary>Stable ID used in save keys ("python", "php", "sql"). Never change these.</summary>
        public static string ToId(this ProgrammingLanguage language)
        {
            switch (language)
            {
                case ProgrammingLanguage.Python: return "python";
                case ProgrammingLanguage.PHP: return "php";
                case ProgrammingLanguage.SQL: return "sql";
                default: return string.Empty;
            }
        }

        /// <summary>Player-facing name of the language.</summary>
        public static string ToDisplayName(this ProgrammingLanguage language)
        {
            switch (language)
            {
                case ProgrammingLanguage.Python: return "Python";
                case ProgrammingLanguage.PHP: return "PHP";
                case ProgrammingLanguage.SQL: return "SQL";
                default: return "Not selected";
            }
        }
    }
}
