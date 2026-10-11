using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using CodeEscape.Clues;
using CodeEscape.Cutscenes;
using CodeEscape.Game;
using CodeEscape.Programming;
using CodeEscape.Story;

namespace CodeEscape.SaveSystem
{
    /// <summary>
    /// Saves the playthrough automatically: after clues, challenges, story steps, unlocks and scene
    /// changes, every few seconds during play (for time played) and on quit. Each manager restores
    /// its own part of the save when the game starts. Lives on the persistent GameSystems object.
    /// </summary>
    public class ProgressSaveService : MonoBehaviour
    {
        public static ProgressSaveService Instance { get; private set; }

        [SerializeField, Min(5f)] float autosaveInterval = 30f;

        bool dirty;
        bool sessionActive;
        float nextAutosave;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            // Only an existing save (or a newly started game) is ever written to.
            sessionActive = SaveManager.HasProgress;
        }

        void OnEnable()
        {
            ClueManager.ClueCollected += OnClue;
            ProgrammingChallengeManager.ChallengeCompleted += OnChallenge;
            ProgrammingChallengeManager.ScoreChanged += OnScore;
            StoryManager.StoryStageChanged += OnStory;
            StoryManager.StoryEventCompleted += OnStoryEvent;
            LevelManager.LevelUnlocked += OnLevelUnlocked;
            LanguageManager.LanguageChanged += OnNewGame;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDisable()
        {
            ClueManager.ClueCollected -= OnClue;
            ProgrammingChallengeManager.ChallengeCompleted -= OnChallenge;
            ProgrammingChallengeManager.ScoreChanged -= OnScore;
            StoryManager.StoryStageChanged -= OnStory;
            StoryManager.StoryEventCompleted -= OnStoryEvent;
            LevelManager.LevelUnlocked -= OnLevelUnlocked;
            LanguageManager.LanguageChanged -= OnNewGame;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void LateUpdate()
        {
            // Saving at the end of the frame lets every manager react to an event first.
            if (dirty || (IsInGameplay() && Time.unscaledTime >= nextAutosave))
                SaveNow();
        }

        void OnApplicationQuit()
        {
            SaveNow();
        }

        void OnApplicationPause(bool paused)
        {
            // The app being sent to the background may be the last chance to save.
            if (paused)
                SaveNow();
        }

        public void SaveNow()
        {
            dirty = false;
            nextAutosave = Time.unscaledTime + autosaveInterval;
            if (!sessionActive || !LanguageManager.Current.IsValid())
                return;
            SaveManager.SaveProgress(Capture());
        }

        /// <summary>
        /// Resets the playthrough - story stage, objective, level progress, challenges, clues, score,
        /// hints and time played - and deletes its save, along with the level map progress of every
        /// language. Settings and the chosen language are kept.
        /// </summary>
        public void ResetProgress()
        {
            if (LevelProgressManager.Instance != null)
                LevelProgressManager.Instance.ResetAllLevelProgress();
            else
                SaveManager.DeleteAllLevelProgress();
            if (ProgrammingChallengeManager.Instance != null)
                ProgrammingChallengeManager.Instance.ResetProgress();
            if (ClueManager.Instance != null)
                ClueManager.Instance.ResetProgress();
            if (StoryManager.Instance != null)
                StoryManager.Instance.ResetProgress();
            if (LevelManager.Instance != null)
                LevelManager.Instance.ResetProgress();
            if (GameManager.Instance != null)
                GameManager.Instance.ResetPlayTime();
            if (CutsceneManager.Instance != null)
                CutsceneManager.Instance.ClearQueue();
            ClearSave();
            Debug.Log("[ProgressSaveService] Progress reset (settings kept).");
        }

        /// <summary>
        /// Replaces the playthrough with a saved state - the start of a level picked on the level map, or
        /// the level being continued - and saves it. Fires no story or clue events, so nothing replays.
        /// </summary>
        public void Restore(ProgressData data)
        {
            if (data == null)
                return;
            data.language = (int)LanguageManager.Current;

            if (ProgrammingChallengeManager.Instance != null)
                ProgrammingChallengeManager.Instance.ApplyProgress(data);
            if (ClueManager.Instance != null)
                ClueManager.Instance.ApplyProgress(data);
            if (StoryManager.Instance != null)
                StoryManager.Instance.ApplyProgress(data);
            if (LevelManager.Instance != null)
                LevelManager.Instance.ApplyProgress(data);
            if (GameManager.Instance != null)
                GameManager.Instance.SetPlayTime(data.timePlayed);
            if (CutsceneManager.Instance != null)
                CutsceneManager.Instance.ClearQueue();

            sessionActive = true;
            SaveNow();
        }

        /// <summary>Deletes the save (story finished). Nothing is saved again until a new game starts.</summary>
        public void ClearSave()
        {
            SaveManager.DeleteProgress();
            sessionActive = false;
            dirty = false;
            Debug.Log("[ProgressSaveService] Save cleared.");
        }

        public ProgressData Capture()
        {
            var data = new ProgressData { language = (int)LanguageManager.Current };
            if (LevelProgressManager.Instance != null)
                data.activeLevel = LevelProgressManager.Instance.ActiveLevelId;

            if (StoryManager.Instance != null)
            {
                data.storyStage = StoryManager.Instance.CurrentStageIndex;
                data.completedEvents = new List<string>(StoryManager.Instance.CompletedEvents);
            }
            if (ObjectiveManager.Instance != null)
                data.currentObjective = ObjectiveManager.Instance.CurrentObjective;
            if (LevelManager.Instance != null)
            {
                data.currentLevel = LevelManager.Instance.CurrentLevel;
                data.unlockedLevels = new List<string>(LevelManager.Instance.UnlockedLevels);
                data.completedLevels = new List<string>(LevelManager.Instance.CompletedLevels);
            }
            if (ProgrammingChallengeManager.Instance != null)
            {
                ProgrammingChallengeManager challenges = ProgrammingChallengeManager.Instance;
                data.completedChallenges = new List<string>(challenges.CompletedObstacleIds);
                data.completedQuestions = new List<string>(challenges.CompletedQuestionIds);
                data.hintedQuestions = new List<string>(challenges.HintedQuestionIds);
                data.score = challenges.Score;
                data.hintsUsed = challenges.HintsUsed;
            }
            if (ClueManager.Instance != null)
                data.collectedClues = new List<string>(ClueManager.Instance.CollectedIds);
            if (GameManager.Instance != null)
                data.timePlayed = GameManager.Instance.PlayTime;
            return data;
        }

        static bool IsInGameplay()
        {
            if (GameManager.Instance == null)
                return false;
            GameState state = GameManager.Instance.CurrentState;
            return state == GameState.Playing || state == GameState.Challenge || state == GameState.Paused;
        }

        void MarkDirty() => dirty = true;
        void OnClue(ClueData clue) => MarkDirty();
        void OnChallenge(string obstacleId, QuestionData question) => MarkDirty();
        void OnScore(int score, int hints) => MarkDirty();
        void OnStory(int index, StoryStep step) => MarkDirty();
        void OnStoryEvent(string storyEvent) => MarkDirty();
        void OnLevelUnlocked(LevelInfo level) => MarkDirty();
        void OnSceneLoaded(Scene scene, LoadSceneMode mode) => MarkDirty();

        void OnNewGame(ProgrammingLanguage language)
        {
            sessionActive = true;
            MarkDirty();
        }
    }
}
