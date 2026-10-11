using UnityEngine;
using CodeEscape.Game;
using CodeEscape.Story;

namespace CodeEscape.Cutscenes
{
    /// <summary>
    /// Starts a cutscene when the scene starts, when Alex walks into this trigger, when a story event
    /// happens, or when Trigger() is called. Play-once cutscenes are remembered in the save.
    /// </summary>
    public class CutsceneTrigger : MonoBehaviour
    {
        public enum TriggerMode
        {
            OnSceneStart,
            OnPlayerEnter,
            OnStoryEvent,
            Manual
        }

        [SerializeField] string cutsceneId;
        [SerializeField] TriggerMode mode = TriggerMode.OnPlayerEnter;
        [SerializeField, Tooltip("Skip it if it has already played in this playthrough.")]
        bool playOnce = true;
        [SerializeField, Tooltip("OnStoryEvent: the event to wait for, e.g. clue:C05_SMALL_FOOTPRINTS.")]
        string storyEvent;
        [SerializeField, Tooltip("Optional: only trigger once this story event has happened.")]
        string requiredEvent;
        [SerializeField, Tooltip("Optional: scene loaded if the cutscene can't play, so a story scene never gets stuck.")]
        string fallbackScene;

        bool fired;

        public string CutsceneId => cutsceneId;
        public bool HasFired => fired;

        void OnEnable()
        {
            if (mode == TriggerMode.OnStoryEvent)
                StoryManager.StoryEventCompleted += HandleStoryEvent;
        }

        void OnDisable()
        {
            StoryManager.StoryEventCompleted -= HandleStoryEvent;
        }

        void Start()
        {
            if (mode == TriggerMode.OnSceneStart)
                Trigger();
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (mode == TriggerMode.OnPlayerEnter && other.CompareTag("Player"))
                Trigger();
        }

        /// <summary>Queues the cutscene if its conditions are met. Returns true if it was queued.</summary>
        public bool Trigger()
        {
            if (fired)
                return false;

            CutsceneManager cutscenes = CutsceneManager.Instance;
            if (cutscenes == null || !cutscenes.HasCutscene(cutsceneId))
            {
                Debug.LogWarning($"[CutsceneTrigger] Cutscene '{cutsceneId}' is not available.");
                fired = true;
                if (!string.IsNullOrEmpty(fallbackScene))
                    SceneTransitionManager.Load(fallbackScene);
                return false;
            }
            if (playOnce && cutscenes.HasPlayed(cutsceneId))
                return false;
            StoryManager story = StoryManager.Instance;
            if (!string.IsNullOrEmpty(requiredEvent) && (story == null || !story.HasCompletedEvent(requiredEvent)))
                return false;

            fired = cutscenes.Play(cutsceneId);
            return fired;
        }

        void HandleStoryEvent(string completedEvent)
        {
            if (completedEvent == storyEvent)
                Trigger();
        }
    }
}
