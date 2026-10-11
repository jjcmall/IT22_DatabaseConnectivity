using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using CodeEscape.Programming;
using CodeEscape.SaveSystem;

namespace CodeEscape.Game
{
    /// <summary>A playable location and the challenges that must be solved before leaving it.</summary>
    [Serializable]
    public class LevelInfo
    {
        [Tooltip("Stable ID used by the level map save (e.g. \"warehouse\"). Never change it once released.")]
        public string levelId;
        public string sceneName;
        public string displayName;
        [TextArea(1, 3), Tooltip("Short briefing shown on the level map.")]
        public string mapDescription;
        [Tooltip("One short line under the name on the level map card.")]
        public string mapTagline;
        [Tooltip("Picture of the location shown on its level map card.")]
        public Sprite mapPicture;
        public bool unlockedAtStart;
        [Tooltip("Obstacle IDs that must be solved before the player may leave this level.")]
        public string[] requiredChallenges = new string[0];
    }

    /// <summary>
    /// Tracks the current level and which levels are unlocked or completed, and refuses travel to a
    /// locked level or out of a level whose required challenges aren't solved yet.
    /// Lives on the persistent GameSystems object.
    /// </summary>
    public class LevelManager : MonoBehaviour
    {
        public static LevelManager Instance { get; private set; }

        public static event Action<LevelInfo> LevelUnlocked;

        [SerializeField] List<LevelInfo> levels = new List<LevelInfo>();
        [SerializeField, Tooltip("Shown for debugging.")]
        string currentLevel = SceneNames.Home;

        readonly HashSet<string> unlocked = new HashSet<string>();
        readonly HashSet<string> completed = new HashSet<string>();

        public string CurrentLevel => currentLevel;
        public LevelInfo CurrentLevelInfo => GetInfo(currentLevel);
        public string CurrentLevelDisplayName => CurrentLevelInfo != null ? CurrentLevelInfo.displayName : string.Empty;
        public IEnumerable<string> UnlockedLevels => unlocked;
        public IEnumerable<string> CompletedLevels => completed;
        public int LevelCount => levels.Count;
        /// <summary>The levels in play order (the order shown on the level map).</summary>
        public IReadOnlyList<LevelInfo> Levels => levels;

        /// <summary>Number of challenges the whole story requires (every level's required challenges).</summary>
        public int TotalRequiredChallenges
        {
            get
            {
                int total = 0;
                foreach (LevelInfo level in levels)
                    total += level.requiredChallenges != null ? level.requiredChallenges.Length : 0;
                return total;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
            LevelUnlocked = null;
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            ApplyProgress(SaveManager.LoadProgress());
        }

        /// <summary>Restores unlocked/completed levels and the current level from a saved state (reset when null).</summary>
        public void ApplyProgress(ProgressData progress)
        {
            if (progress == null)
            {
                ResetProgress();
                return;
            }
            unlocked.Clear();
            completed.Clear();
            foreach (string scene in progress.unlockedLevels) unlocked.Add(scene);
            foreach (string scene in progress.completedLevels) completed.Add(scene);
            foreach (LevelInfo level in levels) if (level.unlockedAtStart) unlocked.Add(level.sceneName);
            currentLevel = GetInfo(progress.currentLevel) != null ? progress.currentLevel : SceneNames.Home;
        }

        void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            LanguageManager.LanguageChanged += HandleNewGame;
        }

        void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            LanguageManager.LanguageChanged -= HandleNewGame;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public LevelInfo GetInfo(string sceneName)
        {
            foreach (LevelInfo level in levels)
            {
                if (level.sceneName == sceneName)
                    return level;
            }
            return null;
        }

        /// <summary>Finds a level by its stable level ID.</summary>
        public LevelInfo GetInfoById(string levelId)
        {
            if (string.IsNullOrEmpty(levelId))
                return null;
            foreach (LevelInfo level in levels)
            {
                if (level.levelId == levelId)
                    return level;
            }
            return null;
        }

        /// <summary>Position of the level in play order, or -1.</summary>
        public int IndexOf(LevelInfo level)
        {
            return level != null ? levels.IndexOf(level) : -1;
        }

        public bool IsUnlocked(string sceneName) => unlocked.Contains(sceneName);
        public bool IsCompleted(string sceneName) => completed.Contains(sceneName);

        /// <summary>Sets the level the playthrough continues at (the next level once one is cleared).</summary>
        public void SetCurrentLevel(string sceneName)
        {
            if (GetInfo(sceneName) != null)
                currentLevel = sceneName;
        }

        public void UnlockLevel(string sceneName)
        {
            LevelInfo info = GetInfo(sceneName);
            if (info == null)
            {
                Debug.LogWarning($"[LevelManager] Unknown level '{sceneName}'.");
                return;
            }
            if (unlocked.Add(sceneName))
            {
                Debug.Log($"[LevelManager] Unlocked {info.displayName} ({sceneName})");
                LevelUnlocked?.Invoke(info);
            }
        }

        /// <summary>True when every required challenge of the level is solved. Lists the first missing one.</summary>
        public bool AreRequiredChallengesComplete(string sceneName, out string missing)
        {
            missing = null;
            LevelInfo info = GetInfo(sceneName);
            if (info == null || info.requiredChallenges == null)
                return true;
            ProgrammingChallengeManager challenges = ProgrammingChallengeManager.Instance;
            foreach (string id in info.requiredChallenges)
            {
                if (challenges == null || !challenges.IsObstacleCompleted(id))
                {
                    missing = id;
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Travels to a level if it is unlocked and the current level's required challenges are solved.
        /// Leaving clears the current level: with the level map, that is saved for the selected language
        /// and the player returns to the map (the next level is played from there).
        /// Returns false with a player-facing reason otherwise.
        /// </summary>
        public bool TryTravel(string targetScene, out string reason)
        {
            reason = null;
            // Already leaving (e.g. E pressed again during the fade): ignore quietly, nothing is recorded twice.
            if (SceneTransitionManager.Instance != null && SceneTransitionManager.Instance.IsTransitioning)
                return false;
            LevelInfo target = GetInfo(targetScene);
            if (target == null)
            {
                reason = "That way doesn't lead anywhere yet.";
                return false;
            }
            if (!IsUnlocked(targetScene))
            {
                reason = $"{target.displayName} is locked. Keep investigating here first.";
                return false;
            }
            if (!AreRequiredChallengesComplete(currentLevel, out _))
            {
                reason = "There's still a code lock to solve here.";
                return false;
            }

            completed.Add(currentLevel);
            LevelProgressManager progress = LevelProgressManager.Instance;
            if (progress != null && progress.CompleteLevelAndReturnToMap(currentLevel, targetScene))
                return true;
            // No level map available: go straight on to the next level as before.
            return SceneTransitionManager.Load(targetScene);
        }

        /// <summary>Loads the saved level (CONTINUE). A finished story continues with the ending.</summary>
        public bool ContinueGame()
        {
            string target = GetInfo(currentLevel) != null && IsUnlocked(currentLevel) ? currentLevel : SceneNames.Home;
            if (Story.StoryManager.Instance != null && Story.StoryManager.Instance.IsStoryComplete)
                target = SceneNames.Ending;
            Debug.Log($"[LevelManager] Continue at {target}");
            return SceneTransitionManager.Load(target);
        }

        public void ResetProgress()
        {
            unlocked.Clear();
            completed.Clear();
            foreach (LevelInfo level in levels)
            {
                if (level.unlockedAtStart)
                    unlocked.Add(level.sceneName);
            }
            currentLevel = SceneNames.Home;
        }

        void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (GetInfo(scene.name) != null)
            {
                currentLevel = scene.name;
                unlocked.Add(scene.name);
            }
            else if (scene.name == SceneNames.Ending && AreRequiredChallengesComplete(currentLevel, out _))
            {
                // The last level is left through the ending cutscene rather than an exit door.
                completed.Add(currentLevel);
                if (LevelProgressManager.Instance != null)
                    LevelProgressManager.Instance.RecordLevelCleared(currentLevel, null);
            }
        }

        void HandleNewGame(ProgrammingLanguage language)
        {
            ResetProgress();
        }
    }
}
