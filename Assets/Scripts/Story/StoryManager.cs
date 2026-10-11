using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using CodeEscape.Clues;
using CodeEscape.Game;
using CodeEscape.Programming;
using CodeEscape.SaveSystem;

namespace CodeEscape.Story
{
    /// <summary>One step of the story: its objective and the event that completes it.</summary>
    [Serializable]
    public class StoryStep
    {
        public string id;
        [TextArea(1, 3)] public string objective;
        [Tooltip("Event that completes this step, e.g. clue:C01_FOLLOW_THE_CODE, challenge:home_hallway_door, enter:04_Neighborhood, event:child_found. Empty = final step.")]
        public string completeOnEvent;
        [Tooltip("Scene unlocked when this step is completed (optional).")]
        public string unlockLevel;
        [Tooltip("Cutscene to request when this step is completed (played by CutsceneManager).")]
        public string cutsceneId;
    }

    /// <summary>
    /// Tracks story state only: the current step, completed story events, and what each step
    /// unlocks. Story events come from clues ("clue:ID"), solved challenges ("challenge:ID"),
    /// entering scenes ("enter:Scene") and story objects ("event:name").
    /// Lives on the persistent GameSystems object.
    /// </summary>
    public class StoryManager : MonoBehaviour
    {
        public static StoryManager Instance { get; private set; }

        public const string ClueEventPrefix = "clue:";
        public const string EnterEventPrefix = "enter:";

        /// <summary>Raised with (new step index, new step) when the story moves on.</summary>
        public static event Action<int, StoryStep> StoryStageChanged;
        /// <summary>Raised with a cutscene ID when a step asks for one (CutsceneManager plays it).</summary>
        public static event Action<string> CutsceneRequested;
        public static event Action<string> StoryEventCompleted;

        [SerializeField] List<StoryStep> steps = new List<StoryStep>();
        [SerializeField, Tooltip("Shown for debugging.")]
        int currentStepIndex;

        readonly HashSet<string> completedEvents = new HashSet<string>();

        public int CurrentStageIndex => currentStepIndex;
        public int StageCount => steps.Count;
        public StoryStep CurrentStep => currentStepIndex >= 0 && currentStepIndex < steps.Count ? steps[currentStepIndex] : null;
        public bool IsStoryComplete => CurrentStep == null || string.IsNullOrEmpty(CurrentStep.completeOnEvent);
        public IEnumerable<string> CompletedEvents => completedEvents;
        public string CurrentLocation => LevelManager.Instance != null ? LevelManager.Instance.CurrentLevelDisplayName : string.Empty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
            StoryStageChanged = null;
            CutsceneRequested = null;
            StoryEventCompleted = null;
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            Load(SaveManager.LoadProgress());
        }

        void Load(ProgressData progress)
        {
            currentStepIndex = 0;
            completedEvents.Clear();
            if (progress == null)
                return;
            currentStepIndex = Mathf.Clamp(progress.storyStage, 0, Mathf.Max(0, steps.Count - 1));
            foreach (string e in progress.completedEvents)
                completedEvents.Add(e);
        }

        void OnEnable()
        {
            ClueManager.ClueCollected += HandleClueCollected;
            ProgrammingChallengeManager.ChallengeCompleted += HandleChallengeCompleted;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            LanguageManager.LanguageChanged += HandleNewGame;
        }

        void OnDisable()
        {
            ClueManager.ClueCollected -= HandleClueCollected;
            ProgrammingChallengeManager.ChallengeCompleted -= HandleChallengeCompleted;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            LanguageManager.LanguageChanged -= HandleNewGame;
        }

        void Start()
        {
            PushObjective();
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public bool HasCompletedEvent(string storyEvent)
        {
            return !string.IsNullOrEmpty(storyEvent) && completedEvents.Contains(storyEvent);
        }

        /// <summary>Records a story event and advances the story if it completes the current step.</summary>
        public void CompleteEvent(string storyEvent)
        {
            if (string.IsNullOrEmpty(storyEvent))
                return;
            if (completedEvents.Add(storyEvent))
            {
                Debug.Log($"[StoryManager] Event: {storyEvent}");
                StoryEventCompleted?.Invoke(storyEvent);
            }
            TryAdvance();
        }

        public void ResetProgress()
        {
            currentStepIndex = 0;
            completedEvents.Clear();
            PushObjective();
        }

        /// <summary>Restores the story from a saved state (a level started from the level map).</summary>
        public void ApplyProgress(ProgressData progress)
        {
            Load(progress);
            PushObjective();
        }

        /// <summary>
        /// The story just before the step completed by <paramref name="storyEvent"/> (e.g. "enter:05_Warehouse"):
        /// that step's index, with the events of every earlier step and their cutscenes added to
        /// <paramref name="eventsBefore"/>. False if no step completes on that event.
        /// </summary>
        public bool TryGetStateBefore(string storyEvent, out int stageIndex, List<string> eventsBefore)
        {
            stageIndex = steps.FindIndex(step => step.completeOnEvent == storyEvent);
            if (stageIndex < 0)
                return false;
            for (int i = 0; i < stageIndex; i++)
            {
                if (!string.IsNullOrEmpty(steps[i].completeOnEvent))
                    eventsBefore.Add(steps[i].completeOnEvent);
                if (!string.IsNullOrEmpty(steps[i].cutsceneId))
                    eventsBefore.Add(Cutscenes.CutsceneManager.PlayedEventPrefix + steps[i].cutsceneId);
            }
            return true;
        }

        void TryAdvance()
        {
            bool advanced = false;
            // A loop, so steps whose events already happened (e.g. a clue found early) are skipped.
            while (CurrentStep != null && !string.IsNullOrEmpty(CurrentStep.completeOnEvent) && completedEvents.Contains(CurrentStep.completeOnEvent))
            {
                StoryStep finished = CurrentStep;
                if (!string.IsNullOrEmpty(finished.unlockLevel) && LevelManager.Instance != null)
                    LevelManager.Instance.UnlockLevel(finished.unlockLevel);
                if (!string.IsNullOrEmpty(finished.cutsceneId))
                {
                    Debug.Log($"[StoryManager] Cutscene requested: {finished.cutsceneId}");
                    CutsceneRequested?.Invoke(finished.cutsceneId);
                }

                currentStepIndex++;
                advanced = true;
                Debug.Log($"[StoryManager] Story stage {currentStepIndex}: {(CurrentStep != null ? CurrentStep.id : "end")}");
                StoryStageChanged?.Invoke(currentStepIndex, CurrentStep);
            }
            if (advanced)
                PushObjective();
        }

        void PushObjective()
        {
            if (ObjectiveManager.Instance != null)
                ObjectiveManager.Instance.SetObjective(CurrentStep != null ? CurrentStep.objective : string.Empty);
        }

        void HandleClueCollected(ClueData clue)
        {
            CompleteEvent(ClueEventPrefix + clue.Id);
        }

        void HandleChallengeCompleted(string obstacleId, QuestionData question)
        {
            CompleteEvent("challenge:" + obstacleId);
        }

        void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (LevelManager.Instance != null && LevelManager.Instance.GetInfo(scene.name) != null)
                CompleteEvent(EnterEventPrefix + scene.name);
        }

        void HandleNewGame(ProgrammingLanguage language)
        {
            ResetProgress();
        }
    }
}
