using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using CodeEscape.Clues;
using CodeEscape.Programming;
using CodeEscape.Story;

namespace CodeEscape.Obstacles
{
    /// <summary>
    /// An obstacle (door, gate, ...) that stays locked until the player solves a programming challenge.
    /// The player walks into this object's trigger, presses E, and a correct answer unlocks it.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class ProgrammingObstacle : MonoBehaviour, IChallengeSource
    {
        public enum UnlockBehavior
        {
            DisableBlockers,
            DeactivateObstacle,
            EventOnly
        }

        [Header("Identity")]
        [SerializeField, Tooltip("Unique per obstacle. Used to remember that it has been solved.")]
        string obstacleId = "obstacle";
        [SerializeField] string displayName = "Locked Door";

        [Header("Challenge")]
        [SerializeField, Tooltip("Usually one question per language. The one matching the selected language is used.")]
        List<QuestionData> questions = new List<QuestionData>();
        [SerializeField, Tooltip("If no question above matches the selected language, one with this topic is taken from the database (empty = any topic).")]
        string fallbackTopic;

        [Header("Rewards (optional)")]
        [SerializeField, Tooltip("Clue added to the Clue Journal when this obstacle is solved.")]
        ClueData clueReward;
        [SerializeField, Tooltip("Story event completed when this obstacle is solved (\"challenge:<id>\" is always sent too).")]
        string completeStoryEvent;
        [SerializeField, Tooltip("Legacy text reward, used only when no ClueData is set.")]
        string clueRewardId;
        [SerializeField, TextArea(2, 4)] string clueRewardText;

        [Header("Unlock")]
        [SerializeField] UnlockBehavior unlockBehavior = UnlockBehavior.DisableBlockers;
        [SerializeField, Tooltip("Solid colliders that block the player while locked.")]
        Collider2D[] blockers = new Collider2D[0];
        [SerializeField] GameObject lockedVisual;
        [SerializeField] GameObject unlockedVisual;
        [SerializeField] UnityEvent onUnlocked = new UnityEvent();

        [Header("Interaction")]
        [SerializeField, Tooltip("Shown while the player is in range and the obstacle is locked.")]
        GameObject interactionPrompt;

        bool playerInRange;
        bool unlocked;

        public string ObstacleId => obstacleId;
        public string DisplayName => displayName;
        public bool IsUnlocked => unlocked;
        public bool PlayerInRange => playerInRange;
        public ClueData ClueReward => clueReward;
        public string ClueRewardId => clueReward != null ? clueReward.Id : clueRewardId;
        public string ClueRewardText => clueReward != null ? $"{clueReward.Title} - {clueReward.Description}" : clueRewardText;
        public bool HasClueReward => clueReward != null || !string.IsNullOrWhiteSpace(clueRewardText);
        // A locked obstacle is opened by choice (E) and can be closed and tried again later.
        public bool IsMandatory => false;
        public bool CountsAsObstacle => true;

        void IChallengeSource.OnChallengeSolved() => Unlock();

        void Reset()
        {
            GetComponent<Collider2D>().isTrigger = true;
        }

        void Start()
        {
            // Obstacles solved earlier in this playthrough stay open when the scene reloads.
            ProgrammingChallengeManager manager = ProgrammingChallengeManager.Instance;
            SetUnlocked(manager != null && manager.IsObstacleCompleted(obstacleId), false);
        }

        void Update()
        {
            if (unlocked || !playerInRange)
                return;

            RefreshPrompt();
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.eKey.wasPressedThisFrame)
                TryInteract();
        }

        /// <summary>Opens this obstacle's challenge (the E key).</summary>
        public bool TryInteract()
        {
            if (unlocked)
                return false;

            ProgrammingChallengeManager manager = ProgrammingChallengeManager.Instance;
            if (manager == null)
            {
                Debug.LogError("[Obstacle] No ProgrammingChallengeManager found.");
                return false;
            }

            bool opened = manager.TryOpenChallenge(this);
            RefreshPrompt();
            return opened;
        }

        /// <summary>The question to ask in the given language: a listed one first, otherwise one from the database.</summary>
        public QuestionData ResolveQuestion(ProgrammingLanguage language, QuestionDatabase database, ICollection<string> completedQuestionIds)
        {
            foreach (QuestionData question in questions)
            {
                if (question != null && question.Language == language)
                    return question;
            }
            return database != null ? database.FindQuestion(language, fallbackTopic, null, completedQuestionIds) : null;
        }

        public void Unlock()
        {
            if (unlocked)
                return;
            Debug.Log($"[Obstacle] '{displayName}' unlocked");
            SetUnlocked(true, true);

            if (clueReward != null && ClueManager.Instance != null)
                ClueManager.Instance.TryCollect(clueReward);
            if (!string.IsNullOrEmpty(completeStoryEvent) && StoryManager.Instance != null)
                StoryManager.Instance.CompleteEvent(completeStoryEvent);
        }

        void SetUnlocked(bool value, bool invokeEvents)
        {
            unlocked = value;
            if (unlockBehavior == UnlockBehavior.DisableBlockers)
            {
                foreach (Collider2D blocker in blockers)
                {
                    if (blocker != null)
                        blocker.enabled = !value;
                }
            }
            if (lockedVisual != null)
                lockedVisual.SetActive(!value);
            if (unlockedVisual != null)
                unlockedVisual.SetActive(value);
            RefreshPrompt();

            if (value && invokeEvents)
                onUnlocked.Invoke();
            if (value && unlockBehavior == UnlockBehavior.DeactivateObstacle)
                gameObject.SetActive(false);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player"))
                return;
            playerInRange = true;
            RefreshPrompt();
        }

        void OnTriggerExit2D(Collider2D other)
        {
            if (!other.CompareTag("Player"))
                return;
            playerInRange = false;
            RefreshPrompt();
        }

        void RefreshPrompt()
        {
            if (interactionPrompt == null)
                return;

            ProgrammingChallengeManager manager = ProgrammingChallengeManager.Instance;
            bool show = playerInRange && !unlocked && (manager == null || !manager.IsChallengeOpen);
            if (interactionPrompt.activeSelf != show)
                interactionPrompt.SetActive(show);
        }
    }
}
