using System;
using System.Collections.Generic;
using UnityEngine;
using CodeEscape.Programming;
using CodeEscape.SaveSystem;
using CodeEscape.Story;

namespace CodeEscape.Game
{
    /// <summary>How a level looks and behaves on the level map.</summary>
    public enum LevelMapState
    {
        /// <summary>The previous level hasn't been cleared yet. Can't be entered.</summary>
        Locked,
        /// <summary>Unlocked but not cleared yet: can be played (or continued).</summary>
        Available,
        /// <summary>Cleared: can be replayed or skipped.</summary>
        Completed
    }

    /// <summary>
    /// Level map progress, kept separately for each programming language: which levels are cleared and
    /// therefore which are unlocked (level 1, and every level after a cleared one), plus the saved state
    /// each unlocked level starts from, so any of them can be replayed or skipped to. Starts levels from
    /// the map, records cleared levels (once) and sends the player back to the map.
    /// Lives on the persistent GameSystems object.
    /// </summary>
    public class LevelProgressManager : MonoBehaviour
    {
        /// <summary>A cleared level the level map hasn't announced yet.</summary>
        public class ClearedNotice
        {
            public ProgrammingLanguage language;
            public string levelId;
            public string unlockedLevelId;
            public bool firstClear;
        }

        public static LevelProgressManager Instance { get; private set; }

        /// <summary>Raised with (language, cleared level, newly playable next level or null) when a level is cleared.</summary>
        public static event Action<ProgrammingLanguage, LevelInfo, LevelInfo> LevelCleared;

        [SerializeField, Tooltip("Shown for debugging: ID of the level being played (started from the level map).")]
        string activeLevelId = string.Empty;

        readonly Dictionary<ProgrammingLanguage, LevelProgressData> cache = new Dictionary<ProgrammingLanguage, LevelProgressData>();

        public string ActiveLevelId => activeLevelId;
        public ClearedNotice PendingNotice { get; private set; }

        static IReadOnlyList<LevelInfo> Levels => LevelManager.Instance != null ? LevelManager.Instance.Levels : Array.Empty<LevelInfo>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
            LevelCleared = null;
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            ProgressData save = SaveManager.LoadProgress();
            activeLevelId = save != null && save.activeLevel != null ? save.activeLevel : string.Empty;
        }

        void Start()
        {
            // Runs after every manager's Awake, so the level list and the story are loaded.
            AdoptSaveFromBeforeLevelMap();
        }

        void OnEnable()
        {
            LanguageManager.LanguageChanged += HandleNewGame;
        }

        void OnDisable()
        {
            LanguageManager.LanguageChanged -= HandleNewGame;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        // ---------- Queries ----------

        public LevelProgressData GetProgress(ProgrammingLanguage language)
        {
            if (!language.IsValid())
                return new LevelProgressData();
            if (!cache.TryGetValue(language, out LevelProgressData data))
            {
                data = SaveManager.LoadLevelProgress(language) ?? new LevelProgressData { language = language.ToId() };
                data.Sanitize(id => GetLevel(id) != null);
                cache[language] = data;
            }
            return data;
        }

        public LevelInfo GetLevel(string levelId)
        {
            return LevelManager.Instance != null ? LevelManager.Instance.GetInfoById(levelId) : null;
        }

        /// <summary>Position of the level in play order (level number - 1), or -1.</summary>
        public int IndexOf(string levelId)
        {
            LevelInfo level = GetLevel(levelId);
            return level != null && LevelManager.Instance != null ? LevelManager.Instance.IndexOf(level) : -1;
        }

        public bool IsCompleted(ProgrammingLanguage language, string levelId)
        {
            return GetProgress(language).IsCompleted(levelId);
        }

        /// <summary>Level 1 is always unlocked; any other level once the level before it is cleared.</summary>
        public bool IsUnlocked(ProgrammingLanguage language, string levelId)
        {
            int index = IndexOf(levelId);
            if (index < 0)
                return false;
            return index == 0 || IsCompleted(language, levelId) || IsCompleted(language, Levels[index - 1].levelId);
        }

        public LevelMapState GetState(ProgrammingLanguage language, string levelId)
        {
            if (IsCompleted(language, levelId))
                return LevelMapState.Completed;
            return IsUnlocked(language, levelId) ? LevelMapState.Available : LevelMapState.Locked;
        }

        public int CompletedCount(ProgrammingLanguage language)
        {
            int count = 0;
            foreach (LevelInfo level in Levels)
            {
                if (IsCompleted(language, level.levelId))
                    count++;
            }
            return count;
        }

        /// <summary>The first level not cleared yet (the one to play next), or null when every level is cleared.</summary>
        public LevelInfo GetNextLevel(ProgrammingLanguage language)
        {
            foreach (LevelInfo level in Levels)
            {
                if (!IsCompleted(language, level.levelId))
                    return IsUnlocked(language, level.levelId) ? level : null;
            }
            return null;
        }

        /// <summary>
        /// Where "Skip to Next Level" goes from a cleared level: the first level after it that isn't cleared
        /// yet, or (when all of those are cleared) simply the next level. Null for a level that isn't cleared,
        /// so an uncleared level can never be skipped, and for the last level.
        /// </summary>
        public LevelInfo GetSkipTarget(ProgrammingLanguage language, string levelId)
        {
            int index = IndexOf(levelId);
            if (index < 0 || !IsCompleted(language, levelId))
                return null;
            IReadOnlyList<LevelInfo> levels = Levels;
            for (int i = index + 1; i < levels.Count; i++)
            {
                if (!IsCompleted(language, levels[i].levelId))
                    return IsUnlocked(language, levels[i].levelId) ? levels[i] : null;
            }
            return index + 1 < levels.Count ? levels[index + 1] : null;
        }

        /// <summary>
        /// True when the saved playthrough is part-way through this language's level (it has been entered,
        /// not just unlocked): Continue resumes it.
        /// </summary>
        public bool CanResume(ProgrammingLanguage language, string levelId)
        {
            LevelInfo level = GetLevel(levelId);
            if (!language.IsValid() || level == null)
                return false;
            ProgressData save = SaveManager.LoadProgress();
            return save != null && save.language == (int)language && save.activeLevel == levelId
                && save.completedEvents.Contains(StoryManager.EnterEventPrefix + level.sceneName)
                && IsUnlocked(language, levelId);
        }

        /// <summary>The level the saved playthrough can be continued at (main menu Continue), if any.</summary>
        public bool TryGetResumableLevel(out LevelInfo level)
        {
            level = null;
            ProgressData save = SaveManager.LoadProgress();
            if (save == null)
                return false;
            var language = (ProgrammingLanguage)save.language;
            if (language != LanguageManager.Current || !CanResume(language, save.activeLevel))
                return false;
            level = GetLevel(save.activeLevel);
            return level != null;
        }

        // ---------- Playing levels ----------

        /// <summary>Continues the saved level (main menu Continue).</summary>
        public bool ResumeSavedLevel(out string reason)
        {
            if (!TryGetResumableLevel(out LevelInfo level))
            {
                reason = "There is no saved level to continue.";
                return false;
            }
            return StartLevel(level.levelId, true, out reason);
        }

        /// <summary>
        /// Starts a level of the selected language from the level map. <paramref name="resume"/> continues
        /// the saved progress inside it (when there is any); otherwise the level starts from its beginning.
        /// Locked levels and missing scenes are refused with a player-facing reason.
        /// </summary>
        public bool StartLevel(string levelId, bool resume, out string reason)
        {
            reason = null;
            ProgrammingLanguage language = LanguageManager.Current;
            LevelInfo level = GetLevel(levelId);
            int index = IndexOf(levelId);
            if (!language.IsValid())
            {
                reason = "Choose a programming language first.";
                return false;
            }
            if (level == null)
            {
                reason = "That level doesn't exist.";
                return false;
            }
            if (!IsUnlocked(language, levelId))
            {
                reason = $"Level {index + 1} is locked. Clear Level {index} - {Levels[index - 1].displayName} first.";
                return false;
            }
            if (!Application.CanStreamedLevelBeLoaded(level.sceneName))
            {
                Debug.LogError($"[LevelProgress] Level '{levelId}' uses scene '{level.sceneName}', which is not in Build Settings.");
                reason = "This level can't be loaded right now.";
                return false;
            }
            if (SceneTransitionManager.Instance != null && SceneTransitionManager.Instance.IsTransitioning)
            {
                reason = "Please wait a moment.";
                return false;
            }

            bool continuing = resume && CanResume(language, levelId);
            ProgressData state = continuing ? SaveManager.LoadProgress() : BuildStartState(language, level, index);
            state.activeLevel = levelId;
            if (!continuing)
                state.currentLevel = level.sceneName;
            activeLevelId = levelId;

            if (ProgressSaveService.Instance != null)
                ProgressSaveService.Instance.Restore(state);
            else
                Debug.LogError("[LevelProgress] ProgressSaveService is missing - the level starts without its saved progress.");

            LevelProgressData data = GetProgress(language);
            data.lastPlayedLevel = levelId;
            SaveManager.SaveLevelProgress(language, data);

            // The story intro plays before level 1 the first time it is played.
            string scene = level.sceneName;
            if (!continuing && index == 0 && !IsCompleted(language, levelId) && Application.CanStreamedLevelBeLoaded(SceneNames.Intro))
                scene = SceneNames.Intro;

            Debug.Log($"[LevelProgress] {(continuing ? "Continue" : "Start")} {language.ToDisplayName()} level {index + 1} '{levelId}' ({scene})");
            return SceneTransitionManager.Load(scene);
        }

        /// <summary>Skips from a cleared level to the next eligible one. Never marks a level as cleared.</summary>
        public bool SkipFrom(string levelId, out string reason)
        {
            ProgrammingLanguage language = LanguageManager.Current;
            LevelInfo target = GetSkipTarget(language, levelId);
            if (target == null)
            {
                reason = IsCompleted(language, levelId)
                    ? "There is no level after this one."
                    : "Only cleared levels can be skipped.";
                return false;
            }
            return StartLevel(target.levelId, CanResume(language, target.levelId), out reason);
        }

        public bool ReturnToMap()
        {
            return SceneTransitionManager.Load(SceneNames.LevelMap);
        }

        /// <summary>Records the cleared level, then returns to the level map. False if the map can't be loaded.</summary>
        public bool CompleteLevelAndReturnToMap(string sceneName, string nextSceneName)
        {
            RecordLevelCleared(sceneName, nextSceneName);
            return ReturnToMap();
        }

        /// <summary>
        /// Records a cleared level for the selected language (once - duplicates are ignored), which unlocks
        /// the level after it, and saves the state that next level starts from. Called by LevelManager when
        /// a level's exit is used, and when the ending starts for the last level.
        /// </summary>
        public void RecordLevelCleared(string sceneName, string nextSceneName)
        {
            ProgrammingLanguage language = LanguageManager.Current;
            LevelManager levelManager = LevelManager.Instance;
            LevelInfo cleared = levelManager != null ? levelManager.GetInfo(sceneName) : null;
            if (!language.IsValid() || cleared == null || string.IsNullOrEmpty(cleared.levelId))
                return;

            LevelProgressData data = GetProgress(language);
            bool firstClear = data.MarkCompleted(cleared.levelId);
            // Levels this playthrough already cleared count as well (e.g. a save from before the level map).
            foreach (string scene in levelManager.CompletedLevels)
            {
                LevelInfo info = levelManager.GetInfo(scene);
                if (info != null)
                    data.MarkCompleted(info.levelId);
            }

            LevelInfo next = levelManager.GetInfo(nextSceneName);
            activeLevelId = next != null ? next.levelId : string.Empty;
            if (next != null && ProgressSaveService.Instance != null)
            {
                // The playthrough now continues at the start of the next level.
                levelManager.SetCurrentLevel(next.sceneName);
                ProgressData start = ProgressSaveService.Instance.Capture();
                data.SetCheckpoint(next.levelId, start);
                ProgressSaveService.Instance.SaveNow();
            }
            SaveManager.SaveLevelProgress(language, data);

            PendingNotice = new ClearedNotice
            {
                language = language,
                levelId = cleared.levelId,
                unlockedLevelId = next != null ? next.levelId : string.Empty,
                firstClear = firstClear
            };
            Debug.Log($"[LevelProgress] {language.ToDisplayName()} level {IndexOf(cleared.levelId) + 1} '{cleared.levelId}' cleared" +
                      $"{(firstClear ? string.Empty : " again")}{(next != null ? $" - next: '{next.levelId}'" : string.Empty)}");
            LevelCleared?.Invoke(language, cleared, next);
        }

        /// <summary>Returns and forgets the cleared-level notice (for the level map's message).</summary>
        public ClearedNotice ConsumeNotice(ProgrammingLanguage language)
        {
            ClearedNotice notice = PendingNotice;
            if (notice == null || notice.language != language)
                return null;
            PendingNotice = null;
            return notice;
        }

        /// <summary>
        /// Drops the cached level map progress and re-reads it (and the active level) from the save, after the
        /// cloud save has been written into it.
        /// </summary>
        public void ReloadFromSave()
        {
            cache.Clear();
            ProgressData save = SaveManager.LoadProgress();
            activeLevelId = save != null && save.activeLevel != null ? save.activeLevel : string.Empty;
            PendingNotice = null;
        }

        /// <summary>Deletes the level map progress of every language (Reset Progress).</summary>
        public void ResetAllLevelProgress()
        {
            SaveManager.DeleteAllLevelProgress();
            cache.Clear();
            activeLevelId = string.Empty;
            PendingNotice = null;
            Debug.Log("[LevelProgress] Level map progress reset for every language.");
        }

        // ---------- Helpers ----------

        // The state a level starts from: the checkpoint saved when the level before it was cleared, or a
        // fresh playthrough for level 1. Without a checkpoint (e.g. an old save) it is rebuilt from the story.
        ProgressData BuildStartState(ProgrammingLanguage language, LevelInfo level, int index)
        {
            if (index > 0)
            {
                LevelCheckpoint checkpoint = GetProgress(language).GetCheckpoint(level.levelId);
                if (checkpoint != null && checkpoint.state != null)
                    return JsonUtility.FromJson<ProgressData>(JsonUtility.ToJson(checkpoint.state));
            }

            var state = new ProgressData { language = (int)language, currentLevel = level.sceneName };
            IReadOnlyList<LevelInfo> levels = Levels;
            for (int i = 0; i <= index; i++)
            {
                state.unlockedLevels.Add(levels[i].sceneName);
                if (i == index)
                    break;
                state.completedLevels.Add(levels[i].sceneName);
                if (levels[i].requiredChallenges != null)
                    state.completedChallenges.AddRange(levels[i].requiredChallenges);
            }
            if (index > 0 && StoryManager.Instance != null &&
                StoryManager.Instance.TryGetStateBefore(StoryManager.EnterEventPrefix + level.sceneName, out int stage, state.completedEvents))
            {
                state.storyStage = stage;
                foreach (string storyEvent in state.completedEvents)
                {
                    if (storyEvent.StartsWith(StoryManager.ClueEventPrefix))
                        state.collectedClues.Add(storyEvent.Substring(StoryManager.ClueEventPrefix.Length));
                }
                Debug.Log($"[LevelProgress] No saved start for '{level.levelId}' - rebuilt it from the story (story step {stage}).");
            }
            return state;
        }

        // A playthrough saved before the level map existed: its cleared levels count for its language and
        // it can be continued at the level it was saved in.
        void AdoptSaveFromBeforeLevelMap()
        {
            ProgressData save = SaveManager.LoadProgress();
            if (save == null || save.version >= ProgressData.LevelMapVersion || LevelManager.Instance == null)
                return;
            var language = (ProgrammingLanguage)save.language;
            LevelInfo current = LevelManager.Instance.GetInfo(save.currentLevel);
            if (!language.IsValid() || current == null)
                return;

            if (!SaveManager.HasLevelProgress(language))
            {
                LevelProgressData data = GetProgress(language);
                foreach (string scene in save.completedLevels)
                {
                    LevelInfo info = LevelManager.Instance.GetInfo(scene);
                    if (info != null)
                        data.MarkCompleted(info.levelId);
                }
                SaveManager.SaveLevelProgress(language, data);
            }
            if (StoryManager.Instance == null || !StoryManager.Instance.IsStoryComplete)
                activeLevelId = current.levelId;
            Debug.Log($"[LevelProgress] Adopted a save from before the level map ({language.ToDisplayName()}, '{current.levelId}').");
        }

        void HandleNewGame(ProgrammingLanguage language)
        {
            // A new playthrough (another language was chosen) isn't inside any level yet.
            activeLevelId = string.Empty;
        }
    }
}
