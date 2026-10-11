using System;
using System.Collections.Generic;
using UnityEngine;

namespace CodeEscape.Programming
{
    /// <summary>All predefined questions. Lookups always filter by programming language.</summary>
    [CreateAssetMenu(fileName = "QuestionDatabase", menuName = "Code Escape/Question Database")]
    public class QuestionDatabase : ScriptableObject
    {
        [SerializeField] List<QuestionData> questions = new List<QuestionData>();

        public IReadOnlyList<QuestionData> Questions => questions;

        public List<QuestionData> GetQuestions(ProgrammingLanguage language)
        {
            var result = new List<QuestionData>();
            foreach (QuestionData question in questions)
            {
                if (question != null && question.Language == language)
                    result.Add(question);
            }
            return result;
        }

        public QuestionData GetById(string id)
        {
            foreach (QuestionData question in questions)
            {
                if (question != null && question.Id == id)
                    return question;
            }
            return null;
        }

        /// <summary>
        /// Finds a question in the given language, optionally by topic and difficulty. Prefers questions
        /// not listed in <paramref name="excludeIds"/>, but falls back to one of them rather than nothing.
        /// </summary>
        public QuestionData FindQuestion(ProgrammingLanguage language, string topic = null,
            QuestionDifficulty? difficulty = null, ICollection<string> excludeIds = null)
        {
            QuestionData fallback = null;
            foreach (QuestionData question in questions)
            {
                if (question == null || question.Language != language)
                    continue;
                if (!string.IsNullOrEmpty(topic) && !string.Equals(question.Topic, topic, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (difficulty.HasValue && question.Difficulty != difficulty.Value)
                    continue;

                if (excludeIds == null || !excludeIds.Contains(question.Id))
                    return question;
                if (fallback == null)
                    fallback = question;
            }
            return fallback;
        }

        /// <summary>Lists authoring problems (empty entries, invalid questions, duplicate IDs).</summary>
        public List<string> Validate()
        {
            var problems = new List<string>();
            var ids = new HashSet<string>();
            for (int i = 0; i < questions.Count; i++)
            {
                QuestionData question = questions[i];
                if (question == null)
                {
                    problems.Add($"Entry {i} is empty");
                    continue;
                }

                string error = question.Validate();
                if (error != null)
                    problems.Add($"{question.name}: {error}");
                if (!string.IsNullOrEmpty(question.Id) && !ids.Add(question.Id))
                    problems.Add($"{question.name}: duplicate ID '{question.Id}'");
            }
            return problems;
        }
    }
}
