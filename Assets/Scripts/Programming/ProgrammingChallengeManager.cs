using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using CodeEscape.Game;
using CodeEscape.Obstacles;
using CodeEscape.UI;

namespace CodeEscape.Programming
{
    /// <summary>
    /// Runs programming challenges: picks the obstacle's question for the selected language, pauses the
    /// game, drives ChallengeUI, checks answers against the stored solution, keeps score and unlocks
    /// the obstacle. Hazards that catch Alex open a mandatory challenge through the same path: it can't
    /// be closed until answered correctly, gives no points and doesn't count as a solved obstacle.
    /// Lives on the persistent GameSystems object, so score carries across scenes.
    /// </summary>
    public class ProgrammingChallengeManager : MonoBehaviour
    {
        public const int PointsPerCorrectAnswer = 100;
        public const int HintPenalty = 25;

        public static ProgrammingChallengeManager Instance { get; private set; }

        /// <summary>Raised with (score, hints used).</summary>
        public static event Action<int, int> ScoreChanged;
        public static event Action<QuestionData> ChallengeOpened;
        public static event Action ChallengeClosed;
        /// <summary>Raised with (obstacle ID, question) when a solved challenge closes and the obstacle unlocks.</summary>
        public static event Action<string, QuestionData> ChallengeCompleted;
        /// <summary>Raised with (clue ID, clue text) when an obstacle with a clue reward unlocks.</summary>
        public static event Action<string, string> ClueRewarded;
        /// <summary>Raised with true/false each time an answer is checked.</summary>
        public static event Action<bool> AnswerChecked;
        /// <summary>Raised whenever a hint is shown.</summary>
        public static event Action HintShown;
        /// <summary>Raised with the hazard ID when a hazard catches Alex (its challenge has just opened).</summary>
        public static event Action<string> HazardCaught;
        /// <summary>Raised with the hazard ID when Alex escapes (the caught challenge was solved and closed).</summary>
        public static event Action<string> HazardEscaped;

        [SerializeField] QuestionDatabase questionDatabase;
        [SerializeField] ChallengeUI challengeUI;

        readonly HashSet<string> completedObstacleIds = new HashSet<string>();
        readonly HashSet<string> completedQuestionIds = new HashSet<string>();
        readonly HashSet<string> hintedQuestionIds = new HashSet<string>();

        int score;
        int hintsUsed;
        IChallengeSource activeSource;
        QuestionData activeQuestion;
        bool activeSolved;

        public int Score => score;
        public int HintsUsed => hintsUsed;
        public bool IsChallengeOpen => activeQuestion != null;
        public bool IsActiveChallengeSolved => activeSolved;
        public QuestionData ActiveQuestion => activeQuestion;
        public IChallengeSource ActiveSource => activeSource;
        /// <summary>True while a hazard's challenge is open: it can only end with a correct answer.</summary>
        public bool IsActiveChallengeMandatory => activeSource != null && activeSource.IsMandatory;
        public QuestionDatabase Database => questionDatabase;
        public IEnumerable<string> CompletedObstacleIds => completedObstacleIds;
        public IEnumerable<string> CompletedQuestionIds => completedQuestionIds;
        public IEnumerable<string> HintedQuestionIds => hintedQuestionIds;
        public int CompletedObstacleCount => completedObstacleIds.Count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
            ScoreChanged = null;
            ChallengeOpened = null;
            ChallengeClosed = null;
            ChallengeCompleted = null;
            ClueRewarded = null;
            AnswerChecked = null;
            HintShown = null;
            HazardCaught = null;
            HazardEscaped = null;
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (questionDatabase == null)
                Debug.LogError("[Challenge] No QuestionDatabase assigned.");
            else
            {
                foreach (string problem in questionDatabase.Validate())
                    Debug.LogWarning($"[Challenge] Question problem: {problem}");
            }

            // Restore score, hints and solved challenges from the saved playthrough.
            Load(SaveSystem.SaveManager.LoadProgress());
        }

        void Load(SaveSystem.ProgressData progress)
        {
            completedObstacleIds.Clear();
            completedQuestionIds.Clear();
            hintedQuestionIds.Clear();
            score = 0;
            hintsUsed = 0;
            if (progress == null)
                return;
            score = Mathf.Max(0, progress.score);
            hintsUsed = Mathf.Max(0, progress.hintsUsed);
            foreach (string id in progress.completedChallenges) completedObstacleIds.Add(id);
            foreach (string id in progress.completedQuestions) completedQuestionIds.Add(id);
            foreach (string id in progress.hintedQuestions) hintedQuestionIds.Add(id);
        }

        void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            LanguageManager.LanguageChanged += HandleLanguageChanged;
        }

        void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            LanguageManager.LanguageChanged -= HandleLanguageChanged;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public bool IsObstacleCompleted(string obstacleId)
        {
            return !string.IsNullOrEmpty(obstacleId) && completedObstacleIds.Contains(obstacleId);
        }

        /// <summary>Opens the obstacle's challenge in the selected language. Returns false if it can't open now.</summary>
        public bool TryOpenChallenge(ProgrammingObstacle obstacle)
        {
            return TryOpenChallenge((IChallengeSource)obstacle);
        }

        /// <summary>Opens a challenge for an obstacle or a hazard. Returns false if it can't open now.</summary>
        public bool TryOpenChallenge(IChallengeSource obstacle)
        {
            if (obstacle == null || IsChallengeOpen || !CanStartChallenge())
                return false;
            if (challengeUI == null)
            {
                Debug.LogError("[Challenge] No ChallengeUI assigned.");
                return false;
            }

            ProgrammingLanguage language = LanguageManager.Current;
            if (!language.IsValid())
            {
                Debug.LogWarning("[Challenge] No programming language selected yet - choose one on the Language Select screen.");
                return false;
            }

            QuestionData question = obstacle.ResolveQuestion(language, questionDatabase, completedQuestionIds);
            if (question == null)
            {
                Debug.LogError($"[Challenge] '{obstacle.DisplayName}' has no {language.ToDisplayName()} question.");
                return false;
            }
            if (question.Language != language)
            {
                // Safety net: never show a question from another language.
                Debug.LogError($"[Challenge] Refused {question.Id}: it is {question.Language}, but {language} is selected.");
                return false;
            }

            activeSource = obstacle;
            activeQuestion = question;
            activeSolved = false;

            Time.timeScale = 0f;
            if (GameManager.Instance != null)
                GameManager.Instance.SetState(GameState.Challenge);

            challengeUI.Show(question, obstacle.DisplayName, hintedQuestionIds.Contains(question.Id), obstacle.IsMandatory);
            challengeUI.SetScore(score, hintsUsed);
            Debug.Log($"[Challenge] Opened {question.Id} ({language.ToDisplayName()}) at '{obstacle.DisplayName}'{(obstacle.IsMandatory ? " - caught" : string.Empty)}");
            ChallengeOpened?.Invoke(question);
            if (obstacle.IsMandatory)
                HazardCaught?.Invoke(obstacle.ObstacleId);
            return true;
        }

        public void SubmitChoice(int choiceIndex)
        {
            if (CanAnswer())
                Evaluate(activeQuestion.IsCorrectChoice(choiceIndex));
        }

        public void SubmitTypedAnswer(string answer)
        {
            if (CanAnswer())
                Evaluate(activeQuestion.IsCorrectTypedAnswer(answer));
        }

        /// <summary>Shows the hint. The penalty applies the first time each question's hint is used.</summary>
        public void UseHint()
        {
            if (!IsChallengeOpen || activeSolved || !activeQuestion.HasHint)
                return;

            if (hintedQuestionIds.Add(activeQuestion.Id))
            {
                hintsUsed++;
                AddScore(-HintPenalty);
                Debug.Log($"[Challenge] Hint used on {activeQuestion.Id} (-{HintPenalty}). Score {score}");
            }
            challengeUI.ShowHint(activeQuestion.Hint);
            HintShown?.Invoke();
        }

        /// <summary>Retry after a wrong answer: shows the same question again.</summary>
        public void Retry()
        {
            if (IsChallengeOpen && !activeSolved)
                challengeUI.ShowAnswerInput();
        }

        /// <summary>Continue after a correct answer: closes the challenge and unlocks the obstacle.</summary>
        public void Continue()
        {
            if (IsChallengeOpen && activeSolved)
                CloseChallenge();
        }

        /// <summary>
        /// Closes the challenge. A solved challenge unlocks its obstacle (or releases Alex from a hazard);
        /// an unsolved obstacle stays locked. A hazard's challenge can't be closed until it is solved.
        /// </summary>
        public void CloseChallenge()
        {
            if (!IsChallengeOpen)
                return;
            if (activeSource != null && activeSource.IsMandatory && !activeSolved)
            {
                Debug.Log("[Challenge] Caught - the challenge must be solved to escape.");
                return;
            }

            IChallengeSource obstacle = activeSource;
            QuestionData question = activeQuestion;
            bool solved = activeSolved;
            ClearActiveChallenge();

            if (solved && obstacle != null)
            {
                obstacle.OnChallengeSolved();
                if (obstacle.CountsAsObstacle)
                {
                    if (obstacle.HasClueReward)
                    {
                        Debug.Log($"[Challenge] Clue reward: {obstacle.ClueRewardId}");
                        ClueRewarded?.Invoke(obstacle.ClueRewardId, obstacle.ClueRewardText);
                    }
                    ChallengeCompleted?.Invoke(obstacle.ObstacleId, question);
                }
                else
                {
                    HazardEscaped?.Invoke(obstacle.ObstacleId);
                }
            }
            Debug.Log($"[Challenge] Closed {question.Id} (solved: {solved})");
            ChallengeClosed?.Invoke();
        }

        /// <summary>Clears score, hints and completed challenges (a new playthrough).</summary>
        public void ResetProgress()
        {
            completedObstacleIds.Clear();
            completedQuestionIds.Clear();
            hintedQuestionIds.Clear();
            score = 0;
            hintsUsed = 0;
            ScoreChanged?.Invoke(score, hintsUsed);
        }

        /// <summary>Restores score, hints and solved challenges from a saved state (a level started from the level map).</summary>
        public void ApplyProgress(SaveSystem.ProgressData progress)
        {
            if (IsChallengeOpen)
                ClearActiveChallenge();
            Load(progress);
            ScoreChanged?.Invoke(score, hintsUsed);
        }

        void Evaluate(bool correct)
        {
            int pointsAwarded = 0;
            if (correct)
            {
                activeSolved = true;
                // Escaping a hazard frees Alex but earns no points and isn't a solved obstacle.
                if (activeSource.CountsAsObstacle)
                {
                    if (completedQuestionIds.Add(activeQuestion.Id))
                        pointsAwarded = PointsPerCorrectAnswer;
                    if (!string.IsNullOrEmpty(activeSource.ObstacleId))
                        completedObstacleIds.Add(activeSource.ObstacleId);
                    AddScore(pointsAwarded);
                }
            }

            Debug.Log($"[Challenge] {activeQuestion.Id} answered {(correct ? "correctly" : "incorrectly")}. Score {score}");
            challengeUI.ShowResult(correct, activeQuestion.Explanation, pointsAwarded, correct ? activeSource.ClueRewardText : null, activeSource.IsMandatory);
            AnswerChecked?.Invoke(correct);
        }

        void AddScore(int delta)
        {
            score = Mathf.Max(0, score + delta);
            if (challengeUI != null)
                challengeUI.SetScore(score, hintsUsed);
            ScoreChanged?.Invoke(score, hintsUsed);
        }

        void ClearActiveChallenge()
        {
            activeSource = null;
            activeQuestion = null;
            activeSolved = false;
            if (challengeUI != null)
                challengeUI.Hide();

            Time.timeScale = 1f;
            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Challenge)
                GameManager.Instance.SetState(GameState.Playing);
        }

        bool CanStartChallenge()
        {
            if (SceneTransitionManager.Instance != null && SceneTransitionManager.Instance.IsTransitioning)
                return false;
            if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Playing)
                return false; // e.g. the pause menu is open
            return Time.timeScale > 0f;
        }

        bool CanAnswer()
        {
            return IsChallengeOpen && !activeSolved;
        }

        void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Defensive: a challenge never survives a scene change.
            if (IsChallengeOpen)
                ClearActiveChallenge();
        }

        void HandleLanguageChanged(ProgrammingLanguage language)
        {
            // Choosing a language starts a new playthrough.
            ResetProgress();
        }
    }
}
