using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using CodeEscape.Clues;
using CodeEscape.Game;
using CodeEscape.UI;

namespace CodeEscape.Story
{
    /// <summary>
    /// Something Alex can inspect with E: gives a clue, completes a story event and/or loads a scene.
    /// Can require an earlier story event first. Collecting the same clue twice is ignored.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class StoryInteractable : MonoBehaviour
    {
        [SerializeField] string displayName = "Object";
        [SerializeField, Tooltip("Prompt wording before the name, e.g. \"inspect the\" or \"talk to\".")]
        string promptVerb = "inspect the";
        [SerializeField, Tooltip("Optional clue added to the journal.")]
        ClueData clue;
        [SerializeField, Tooltip("Optional story event completed on use, e.g. event:child_found.")]
        string completeEvent;
        [SerializeField, Tooltip("Optional story event that must have happened first.")]
        string requiredEvent;
        [SerializeField, TextArea(1, 3)] string lockedMessage = "Nothing useful here yet.";
        [SerializeField, Tooltip("Optional scene loaded after use (e.g. the ending).")]
        string loadSceneOnUse;

        [Header("Visuals")]
        [SerializeField] GameObject interactionPrompt;
        [SerializeField] TMP_Text promptText;
        [SerializeField, Tooltip("Optional highlight shown until the object has been used.")]
        GameObject highlight;

        bool playerInRange;
        bool used;

        public bool PlayerInRange => playerInRange;
        public bool HasBeenUsed => used;

        void Reset()
        {
            GetComponent<Collider2D>().isTrigger = true;
        }

        void Start()
        {
            used = (clue != null && ClueManager.Instance != null && ClueManager.Instance.HasClue(clue.Id))
                || (!string.IsNullOrEmpty(completeEvent) && StoryManager.Instance != null && StoryManager.Instance.HasCompletedEvent(completeEvent));
            Refresh();
        }

        void Update()
        {
            if (!playerInRange)
                return;
            Refresh();
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.eKey.wasPressedThisFrame && CanInteractNow())
                Use();
        }

        /// <summary>Inspect the object (the E key).</summary>
        public bool Use()
        {
            StoryManager story = StoryManager.Instance;
            if (!string.IsNullOrEmpty(requiredEvent) && (story == null || !story.HasCompletedEvent(requiredEvent)))
            {
                if (NotificationUI.Instance != null)
                    NotificationUI.Instance.ShowWarning(displayName.ToUpperInvariant(), lockedMessage);
                return false;
            }

            if (clue != null && ClueManager.Instance != null && !ClueManager.Instance.TryCollect(clue) && NotificationUI.Instance != null)
                NotificationUI.Instance.Show("ALREADY IN YOUR JOURNAL", clue.Title, new Color(0.55f, 0.6f, 0.65f), 2f);

            if (!string.IsNullOrEmpty(completeEvent) && story != null)
                story.CompleteEvent(completeEvent);

            used = true;
            Refresh();

            if (!string.IsNullOrEmpty(loadSceneOnUse))
                SceneTransitionManager.Load(loadSceneOnUse);
            return true;
        }

        static bool CanInteractNow()
        {
            return GameManager.Instance == null || GameManager.Instance.CurrentState == GameState.Playing;
        }

        void Refresh()
        {
            if (highlight != null && highlight.activeSelf == used)
                highlight.SetActive(!used);
            if (interactionPrompt == null)
                return;
            bool show = playerInRange && CanInteractNow();
            if (interactionPrompt.activeSelf != show)
                interactionPrompt.SetActive(show);
            if (show && promptText != null)
                promptText.text = used ? $"Press  E  to look at the {displayName.ToLowerInvariant()} again" : $"Press  E  to {promptVerb} {displayName.ToLowerInvariant()}";
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            playerInRange = true;
            Refresh();
        }

        void OnTriggerExit2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            playerInRange = false;
            Refresh();
        }
    }
}
