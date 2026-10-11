using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;
using PlayerData = Unity.Services.CloudSave.Models.Data.Player;
using Unity.Services.Core;
using CodeEscape.Game;
using CodeEscape.Programming;
using CodeEscape.SaveSystem;

namespace CodeEscape.Cloud
{
    public enum CloudState
    {
        Starting,
        SigningIn,
        Loading,
        /// <summary>Services initialised, player signed in and the progression loaded. Gameplay may start.</summary>
        Ready,
        Failed
    }

    public enum CloudSaveStatus
    {
        /// <summary>Everything the game knows is confirmed stored in Cloud Save.</summary>
        Synced,
        /// <summary>There are changes that have not been sent (or not confirmed) yet.</summary>
        Unsaved,
        Saving,
        Failed
    }

    /// <summary>
    /// Stores and retrieves progression in Unity Cloud Save (the only cloud storage used). On start it
    /// initialises Unity Gaming Services, signs the player in anonymously (no login UI), loads the saved
    /// progression and restores it into the existing save system. Afterwards it saves automatically, in the
    /// background, when levels are cleared or unlocked, points are earned, achievements unlock and the main menu
    /// is reached. The existing local save (PlayerPrefs, SaveManager) is still what the game runs on; the cloud
    /// copy is only reported as saved once Cloud Save has confirmed it.
    /// </summary>
    public class CloudProgressService : MonoBehaviour
    {
        public const string KeyProgression = "code_escape_progression";
        public const string KeySnapshot = "code_escape_snapshot";

        const string PrefUnsynced = "CodeEscape.Cloud.Unsynced";
        const string PrefPlayerId = "CodeEscape.Cloud.PlayerId";
        const float SaveDebounceSeconds = 3f;
        const int TimeoutMilliseconds = 20000;

        static readonly ProgrammingLanguage[] Languages = { ProgrammingLanguage.Python, ProgrammingLanguage.PHP, ProgrammingLanguage.SQL };

        public static CloudProgressService Instance { get; private set; }

        /// <summary>Raised whenever the connection state, save status or confirmed record changes.</summary>
        public static event Action Changed;

        public CloudState State { get; private set; } = CloudState.Starting;
        public string ConnectionError { get; private set; } = string.Empty;
        public CloudSaveStatus SaveStatus { get; private set; } = CloudSaveStatus.Synced;
        public string SaveError { get; private set; } = string.Empty;
        public DateTime? LastConfirmedSaveUtc { get; private set; }
        /// <summary>The progression last loaded from, or confirmed saved to, Cloud Save. Null until loaded.</summary>
        public PlayerProgressData Record { get; private set; }
        /// <summary>True when the last load replaced the local game state with the cloud one.</summary>
        public bool RestoredFromCloud { get; private set; }
        /// <summary>Achievements unlocked by the latest save, for a short on-screen message.</summary>
        public string LastUnlockedAchievement { get; private set; } = string.Empty;

        bool connecting;
        bool applying;
        bool saving;
        bool pending;
        float nextFlushTime;
        string confirmedFingerprint = string.Empty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            // Domain reload is disabled in this project, so statics must be cleared manually.
            Instance = null;
            Changed = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            if (Instance != null)
                return;
            var go = new GameObject("CloudProgress");
            DontDestroyOnLoad(go);
            go.AddComponent<CloudProgressService>();
            go.AddComponent<ProgressionUI>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        void Start()
        {
            _ = ConnectAsync();
        }

        void OnEnable()
        {
            LevelProgressManager.LevelCleared += OnLevelCleared;
            LevelManager.LevelUnlocked += OnLevelUnlocked;
            ProgrammingChallengeManager.ScoreChanged += OnScoreChanged;
            ProgrammingChallengeManager.ChallengeCompleted += OnChallengeCompleted;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDisable()
        {
            LevelProgressManager.LevelCleared -= OnLevelCleared;
            LevelManager.LevelUnlocked -= OnLevelUnlocked;
            ProgrammingChallengeManager.ScoreChanged -= OnScoreChanged;
            ProgrammingChallengeManager.ChallengeCompleted -= OnChallengeCompleted;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void Update()
        {
            if (pending && !saving && !applying && State == CloudState.Ready && Time.unscaledTime >= nextFlushTime)
                _ = FlushAsync();
        }

        void OnApplicationPause(bool paused)
        {
            // A last chance to send changes; not guaranteed to finish, so the status stays Unsaved until confirmed.
            if (paused)
                RequestSave(true);
        }

        // ---------- Game events ----------

        void OnLevelCleared(ProgrammingLanguage language, LevelInfo cleared, LevelInfo next) => RequestSave(true);
        void OnLevelUnlocked(LevelInfo level) => RequestSave(false);
        void OnScoreChanged(int score, int hints) => RequestSave(false);
        void OnChallengeCompleted(string obstacleId, QuestionData question) => RequestSave(false);

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Returning to the main menu: send anything not yet confirmed (a no-op when nothing changed).
            if (scene.name == SceneNames.MainMenu)
                RequestSave(true);
        }

        // ---------- Connect and load ----------

        /// <summary>Starts (or retries) the initialise, sign-in and load sequence.</summary>
        public void RetryConnect()
        {
            if (State == CloudState.Failed)
                _ = ConnectAsync();
        }

        /// <summary>Retries a failed save.</summary>
        public void RetrySave()
        {
            if (State != CloudState.Ready || saving)
                return;
            pending = true;
            nextFlushTime = 0f;
        }

        async Task ConnectAsync()
        {
            if (connecting)
                return;
            connecting = true;
            ConnectionError = string.Empty;
            try
            {
                if (Application.internetReachability == NetworkReachability.NotReachable)
                    throw new InvalidOperationException("No internet connection. Code Escape needs to be online to load and save progress.");

                SetState(CloudState.Starting);
                await InitializeServicesAsync();

                SetState(CloudState.SigningIn);
                if (!AuthenticationService.Instance.IsSignedIn)
                    await WithTimeout(AuthenticationService.Instance.SignInAnonymouslyAsync(), "Signing in");

                SetState(CloudState.Loading);
                Dictionary<string, Unity.Services.CloudSave.Models.Item> items = await WithTimeout(
                    CloudSaveService.Instance.Data.Player.LoadAsync(
                        new HashSet<string> { KeyProgression, KeySnapshot },
                        new PlayerData.LoadOptions(new PlayerData.DefaultReadAccessClassOptions())),
                    "Loading progress");

                await HandleLoadedAsync(items);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CloudProgress] Connect failed: {e}");
                ConnectionError = Describe(e);
                SetState(CloudState.Failed);
            }
            finally
            {
                connecting = false;
            }
        }

        static async Task InitializeServicesAsync()
        {
            if (UnityServices.State == ServicesInitializationState.Initialized)
                return;
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                await WithTimeout(UnityServices.InitializeAsync(), "Initialising Unity Gaming Services");
                return;
            }
            // A previous attempt timed out and is still initialising.
            float until = Time.realtimeSinceStartup + TimeoutMilliseconds / 1000f;
            while (UnityServices.State == ServicesInitializationState.Initializing && Time.realtimeSinceStartup < until)
                await Task.Delay(200);
            if (UnityServices.State != ServicesInitializationState.Initialized)
                throw new TimeoutException("Initialising Unity Gaming Services timed out.");
        }

        async Task HandleLoadedAsync(Dictionary<string, Unity.Services.CloudSave.Models.Item> items)
        {
            string playerId = AuthenticationService.Instance.PlayerId;
            bool hasProgression = items.TryGetValue(KeyProgression, out var progressionItem);
            bool hasSnapshot = items.TryGetValue(KeySnapshot, out var snapshotItem);

            if (hasProgression != hasSnapshot)
                throw new InvalidOperationException("The saved progression in Cloud Save is incomplete. It was left untouched; ask the developer to check it.");

            if (!hasProgression)
            {
                // First launch for this player identity: no cloud record exists, so create one from the current game state.
                Debug.Log("[CloudProgress] No progression in Cloud Save for this player - creating a new record.");
                Record = new PlayerProgressData { player_id = playerId };
                RestoredFromCloud = false;
                confirmedFingerprint = string.Empty;
                SetState(CloudState.Loading);
                await SaveAsync(forceInitial: true);
                if (SaveStatus != CloudSaveStatus.Synced)
                    throw new InvalidOperationException("Could not create your progression in Cloud Save: " + SaveError);
                PlayerPrefs.SetString(PrefPlayerId, playerId);
                SetState(CloudState.Ready);
                return;
            }

            PlayerProgressData record;
            ProgressSnapshot snapshot;
            try
            {
                record = JsonUtility.FromJson<PlayerProgressData>(progressionItem.Value.GetAs<string>());
                snapshot = JsonUtility.FromJson<ProgressSnapshot>(snapshotItem.Value.GetAs<string>());
            }
            catch (Exception e)
            {
                throw new InvalidOperationException("The saved progression could not be read and was left untouched: " + e.Message);
            }
            if (record == null || snapshot == null)
                throw new InvalidOperationException("The saved progression is empty or unreadable and was left untouched.");
            record.Sanitize();
            if (record.version > PlayerProgressData.CurrentVersion)
                throw new InvalidOperationException("The saved progression was written by a newer version of Code Escape. Update the game.");

            // Changes this same player identity made but could not confirm last time (quit / network error) are
            // newer than the cloud copy, so keep them and send them again instead of overwriting them.
            bool localIsNewer = PlayerPrefs.GetInt(PrefUnsynced, 0) == 1 && PlayerPrefs.GetString(PrefPlayerId, string.Empty) == playerId;

            Record = record;
            if (localIsNewer)
            {
                RestoredFromCloud = false;
                confirmedFingerprint = Fingerprint(record, snapshot);
                SetState(CloudState.Ready);
                pending = true;
                nextFlushTime = 0f;
                SetSaveStatus(CloudSaveStatus.Unsaved, string.Empty);
                return;
            }

            ApplyToGame(snapshot);
            RestoredFromCloud = true;
            Debug.Log($"[CloudProgress] Loaded progression from Cloud Save (score {record.total_score}, {record.completed_levels.Count} levels cleared, " +
                      $"highest unlocked '{record.highest_unlocked_level}', last played {record.last_played}).");
            confirmedFingerprint = Fingerprint(record, snapshot);
            PlayerPrefs.SetString(PrefPlayerId, playerId);
            PlayerPrefs.SetInt(PrefUnsynced, 0);
            SetSaveStatus(CloudSaveStatus.Synced, string.Empty);
            SetState(CloudState.Ready);

            // Menus read the save when they open; any other scene already built itself from the old state.
            if (SceneManager.GetActiveScene().name != SceneNames.MainMenu)
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        // Replaces the local game state with the cloud one, through the existing save system.
        void ApplyToGame(ProgressSnapshot snapshot)
        {
            applying = true;
            try
            {
                ProgressSaveService saveService = ProgressSaveService.Instance;
                if (saveService != null)
                    saveService.ResetProgress();

                foreach (LanguageLevelProgress entry in snapshot.level_progress)
                {
                    var language = (ProgrammingLanguage)entry.language;
                    if (!language.IsValid() || string.IsNullOrEmpty(entry.json))
                        continue;
                    LevelProgressData data = JsonUtility.FromJson<LevelProgressData>(entry.json);
                    if (data != null)
                        SaveManager.SaveLevelProgress(language, data);
                }

                var selected = (ProgrammingLanguage)snapshot.language;
                if (LanguageManager.Instance != null)
                    LanguageManager.Instance.RestoreLanguage(selected);
                else if (selected.IsValid())
                    SaveManager.SaveLanguage(selected);

                if (saveService != null && !string.IsNullOrEmpty(snapshot.progress_json))
                {
                    ProgressData progress = JsonUtility.FromJson<ProgressData>(snapshot.progress_json);
                    if (progress != null)
                        saveService.Restore(progress);
                }

                if (LevelProgressManager.Instance != null)
                    LevelProgressManager.Instance.ReloadFromSave();
            }
            finally
            {
                applying = false;
            }
        }

        // ---------- Save ----------

        /// <summary>Queues a save. Updates are batched: a burst of events becomes one request.</summary>
        public void RequestSave(bool immediate)
        {
            if (State != CloudState.Ready || applying)
                return;
            pending = true;
            float due = Time.unscaledTime + (immediate ? 0f : SaveDebounceSeconds);
            nextFlushTime = immediate ? due : Mathf.Max(nextFlushTime, due);
            if (SaveStatus == CloudSaveStatus.Synced)
            {
                // Only claim "unsaved" if the game state really differs from what the cloud confirmed.
                BuildRecord(out PlayerProgressData record, out ProgressSnapshot snapshot);
                if (Fingerprint(record, snapshot) == confirmedFingerprint)
                {
                    pending = false;
                    return;
                }
                MarkUnsynced();
                SetSaveStatus(CloudSaveStatus.Unsaved, string.Empty);
            }
        }

        async Task FlushAsync()
        {
            pending = false;
            await SaveAsync(forceInitial: false);
        }

        async Task SaveAsync(bool forceInitial)
        {
            if (saving)
                return;
            saving = true;
            try
            {
                BuildRecord(out PlayerProgressData record, out ProgressSnapshot snapshot);
                string fingerprint = Fingerprint(record, snapshot);
                if (!forceInitial && fingerprint == confirmedFingerprint)
                {
                    SetSaveStatus(CloudSaveStatus.Synced, string.Empty);
                    return;
                }

                record.last_played = DateTime.UtcNow.ToString("o");
                string recordJson = JsonUtility.ToJson(record);
                string snapshotJson = JsonUtility.ToJson(snapshot);

                MarkUnsynced();
                SetSaveStatus(CloudSaveStatus.Saving, string.Empty);
                await WithTimeout(CloudSaveService.Instance.Data.Player.SaveAsync(
                    new Dictionary<string, object>
                    {
                        { KeyProgression, recordJson },
                        { KeySnapshot, snapshotJson }
                    },
                    new PlayerData.SaveOptions(new PlayerData.DefaultWriteAccessClassOptions())), "Saving progress");

                // Only now has Cloud Save confirmed the data.
                Record = record;
                confirmedFingerprint = fingerprint;
                LastConfirmedSaveUtc = DateTime.UtcNow;
                PlayerPrefs.SetInt(PrefUnsynced, 0);
                PlayerPrefs.SetString(PrefPlayerId, record.player_id);
                SetSaveStatus(CloudSaveStatus.Synced, string.Empty);
                Debug.Log($"[CloudProgress] Saved to Cloud Save (score {record.total_score}, {record.completed_levels.Count} levels cleared).");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CloudProgress] Save failed: {e}");
                SetSaveStatus(CloudSaveStatus.Failed, Describe(e));
            }
            finally
            {
                saving = false;
            }
        }

        void MarkUnsynced()
        {
            PlayerPrefs.SetInt(PrefUnsynced, 1);
            PlayerPrefs.Save();
        }

        // ---------- Building the record ----------

        // Reads the current game state (flushed to the local save first) and merges it into the last known record.
        void BuildRecord(out PlayerProgressData record, out ProgressSnapshot snapshot)
        {
            ProgressSaveService saveService = ProgressSaveService.Instance;
            if (saveService != null)
                saveService.SaveNow();

            record = Record != null ? Record.Clone() : new PlayerProgressData();
            record.Sanitize();
            record.version = PlayerProgressData.CurrentVersion;
            record.player_id = AuthenticationService.Instance.PlayerId;

            ProgrammingLanguage current = LanguageManager.Current;
            ProgressData progress = SaveManager.LoadProgress();
            snapshot = new ProgressSnapshot
            {
                language = (int)current,
                progress_json = progress != null ? JsonUtility.ToJson(progress) : string.Empty
            };

            LevelManager levels = LevelManager.Instance;
            LevelProgressManager levelProgress = LevelProgressManager.Instance;
            // Cleared levels mirror the current save (a progress reset clears them); score and achievements are lifetime totals.
            var completed = new HashSet<string>();
            int maxClearedInOneLanguage = 0;
            foreach (ProgrammingLanguage language in Languages)
            {
                LevelProgressData data = SaveManager.LoadLevelProgress(language);
                if (data == null)
                    continue;
                data.Sanitize(id => levels == null || levels.GetInfoById(id) != null);
                snapshot.level_progress.Add(new LanguageLevelProgress { language = (int)language, json = JsonUtility.ToJson(data) });
                foreach (string id in data.completedLevels)
                    completed.Add(id);
                maxClearedInOneLanguage = Mathf.Max(maxClearedInOneLanguage, data.completedLevels.Count);
            }
            record.completed_levels = OrderedLevelIds(completed, levels);

            // Current level: the level being played, else the last one the record named.
            string currentLevel = progress != null ? progress.activeLevel : string.Empty;
            if (string.IsNullOrEmpty(currentLevel) && progress != null && levels != null)
            {
                LevelInfo info = levels.GetInfo(progress.currentLevel);
                currentLevel = info != null ? info.levelId : string.Empty;
            }
            if (!string.IsNullOrEmpty(currentLevel))
                record.current_level = currentLevel;
            else if (string.IsNullOrEmpty(record.current_level) && levels != null && levels.Levels.Count > 0)
                record.current_level = levels.Levels[0].levelId;

            // Highest unlocked level in any language.
            if (levels != null && levelProgress != null)
            {
                int best = -1;
                for (int i = 0; i < levels.Levels.Count; i++)
                {
                    foreach (ProgrammingLanguage language in Languages)
                    {
                        if (levelProgress.IsUnlocked(language, levels.Levels[i].levelId))
                            best = Mathf.Max(best, i);
                    }
                }
                if (best >= 0)
                    record.highest_unlocked_level = levels.Levels[best].levelId;
            }

            // Score: every solved challenge and every hinted question is counted once, however often events fire.
            ProgrammingChallengeManager challenges = ProgrammingChallengeManager.Instance;
            if (challenges != null && current.IsValid())
            {
                var scored = new HashSet<string>(record.scored_challenges);
                foreach (string obstacleId in challenges.CompletedObstacleIds)
                    scored.Add($"{current.ToId()}:{obstacleId}");
                var hinted = new HashSet<string>(record.penalised_hints);
                foreach (string questionId in challenges.HintedQuestionIds)
                    hinted.Add($"{current.ToId()}:{questionId}");
                record.scored_challenges = SortedList(scored);
                record.penalised_hints = SortedList(hinted);
            }
            record.total_score = Mathf.Max(0, record.scored_challenges.Count * ProgrammingChallengeManager.PointsPerCorrectAnswer
                                              - record.penalised_hints.Count * ProgrammingChallengeManager.HintPenalty);

            UnlockAchievements(record, maxClearedInOneLanguage, levels != null ? levels.LevelCount : 0);
        }

        static List<string> SortedList(HashSet<string> set)
        {
            var list = new List<string>(set);
            list.Sort(StringComparer.Ordinal);
            return list;
        }

        static List<string> OrderedLevelIds(HashSet<string> ids, LevelManager levels)
        {
            var result = new List<string>();
            if (levels != null)
            {
                foreach (LevelInfo level in levels.Levels)
                {
                    if (ids.Remove(level.levelId))
                        result.Add(level.levelId);
                }
            }
            var rest = SortedList(ids); // IDs no longer in the level list are kept, not silently dropped.
            result.AddRange(rest);
            return result;
        }

        void UnlockAchievements(PlayerProgressData record, int maxClearedInOneLanguage, int levelCount)
        {
            LastUnlockedAchievement = string.Empty;
            void Unlock(string id, bool earned)
            {
                if (earned && !record.achievements.Contains(id))
                {
                    record.achievements.Add(id);
                    LastUnlockedAchievement = Achievements.NameOf(id);
                }
            }
            Unlock(Achievements.FirstChallenge, record.scored_challenges.Count >= 1);
            Unlock(Achievements.FirstLevel, record.completed_levels.Count >= 1);
            Unlock(Achievements.FiveChallenges, record.scored_challenges.Count >= 5);
            Unlock(Achievements.Halfway, levelCount > 0 && maxClearedInOneLanguage * 2 >= levelCount);
            Unlock(Achievements.CaseClosed, levelCount > 0 && maxClearedInOneLanguage >= levelCount);
        }

        // Identifies the saved content; last_played is excluded so an unchanged game state is never re-sent.
        static string Fingerprint(PlayerProgressData record, ProgressSnapshot snapshot)
        {
            PlayerProgressData copy = record.Clone();
            copy.last_played = string.Empty;
            return JsonUtility.ToJson(copy) + "|" + JsonUtility.ToJson(snapshot);
        }

        // ---------- Helpers ----------

        void SetState(CloudState state)
        {
            State = state;
            Changed?.Invoke();
        }

        void SetSaveStatus(CloudSaveStatus status, string error)
        {
            SaveStatus = status;
            SaveError = error ?? string.Empty;
            Changed?.Invoke();
        }

        static string Describe(Exception e)
        {
            if (e is InvalidOperationException || e is TimeoutException)
                return e.Message;
            if (Application.internetReachability == NetworkReachability.NotReachable)
                return "No internet connection. Check your network and retry.";
            return $"{e.GetType().Name}: {e.Message}";
        }

        static async Task WithTimeout(Task task, string what)
        {
            Task finished = await Task.WhenAny(task, Task.Delay(TimeoutMilliseconds));
            if (finished != task)
                throw new TimeoutException($"{what} timed out. Check your connection and retry.");
            await task;
        }

        static async Task<T> WithTimeout<T>(Task<T> task, string what)
        {
            Task finished = await Task.WhenAny(task, Task.Delay(TimeoutMilliseconds));
            if (finished != task)
                throw new TimeoutException($"{what} timed out. Check your connection and retry.");
            return await task;
        }
    }

    /// <summary>The achievements this prototype awards, derived from progression (not from gameplay triggers).</summary>
    public static class Achievements
    {
        public const string FirstChallenge = "first_challenge";
        public const string FirstLevel = "first_level";
        public const string FiveChallenges = "five_challenges";
        public const string Halfway = "halfway";
        public const string CaseClosed = "case_closed";

        public static string NameOf(string id)
        {
            switch (id)
            {
                case FirstChallenge: return "First Challenge Solved";
                case FirstLevel: return "First Level Cleared";
                case FiveChallenges: return "Five Challenges Solved";
                case Halfway: return "Halfway Through the Case";
                case CaseClosed: return "Case Closed";
                default: return id;
            }
        }
    }
}
