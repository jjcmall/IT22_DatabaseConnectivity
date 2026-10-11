using System;
using UnityEngine;
using CodeEscape.Clues;
using CodeEscape.Programming;
using CodeEscape.SaveSystem;
using CodeEscape.Story;

namespace CodeEscape.Game
{
    /// <summary>The numbers shown on the results screen.</summary>
    [Serializable]
    public struct ResultsData
    {
        public ProgrammingLanguage language;
        public int score;
        public int challengesCompleted;
        public int totalChallenges;
        public int cluesCollected;
        public int totalClues;
        public int hintsUsed;
        public float timePlayed;
        public bool storyComplete;

        public string Rank
        {
            get
            {
                float best = Mathf.Max(1, totalChallenges) * ProgrammingChallengeManager.PointsPerCorrectAnswer;
                float ratio = score / best;
                return ratio >= 0.9f ? "MASTER CODER" : ratio >= 0.7f ? "SKILLED DETECTIVE" : ratio >= 0.4f ? "JUNIOR DETECTIVE" : "ROOKIE";
            }
        }

        public string CompletionStatus => storyComplete ? "CASE SOLVED" : "CASE OPEN";

        public string TimeText => $"{(int)(timePlayed / 60f):00}:{(int)(timePlayed % 60f):00}";
    }

    /// <summary>
    /// Results screen logic: gathers the playthrough's results when the scene opens, clears the
    /// finished save (a finished story has nothing left to continue) and handles the screen's
    /// navigation. ResultsUI only displays what this provides.
    /// </summary>
    public class ResultsManager : MonoBehaviour
    {
        public ResultsData Results { get; private set; }

        void Awake()
        {
            Results = Capture();
        }

        void Start()
        {
            Debug.Log($"[ResultsManager] {Results.CompletionStatus}: {Results.language.ToDisplayName()}, score {Results.score}, " +
                      $"challenges {Results.challengesCompleted}/{Results.totalChallenges}, clues {Results.cluesCollected}/{Results.totalClues}, " +
                      $"hints {Results.hintsUsed}, time {Results.TimeText}");

            // Only a finished story is cleared - an unfinished one can still be continued.
            if (Results.storyComplete && ProgressSaveService.Instance != null)
                ProgressSaveService.Instance.ClearSave();
        }

        /// <summary>Reads the current results from the persistent managers.</summary>
        public static ResultsData Capture()
        {
            ProgrammingChallengeManager challenges = ProgrammingChallengeManager.Instance;
            ClueManager clues = ClueManager.Instance;
            LevelManager levels = LevelManager.Instance;
            return new ResultsData
            {
                language = LanguageManager.Current,
                score = challenges != null ? challenges.Score : 0,
                hintsUsed = challenges != null ? challenges.HintsUsed : 0,
                challengesCompleted = challenges != null ? challenges.CompletedObstacleCount : 0,
                totalChallenges = levels != null ? levels.TotalRequiredChallenges : 0,
                cluesCollected = clues != null ? clues.CollectedCount : 0,
                totalClues = clues != null ? clues.TotalClues : 0,
                timePlayed = GameManager.Instance != null ? GameManager.Instance.PlayTime : 0f,
                storyComplete = StoryManager.Instance != null && StoryManager.Instance.IsStoryComplete
            };
        }

        /// <summary>Return to Main Menu button.</summary>
        public void ReturnToMainMenu()
        {
            SceneTransitionManager.Load(SceneNames.MainMenu);
        }

        /// <summary>Play Again button: choosing a language starts a new playthrough.</summary>
        public void PlayAgain()
        {
            SceneTransitionManager.Load(SceneNames.LanguageSelect);
        }
    }
}
