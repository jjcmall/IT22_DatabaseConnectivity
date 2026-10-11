using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using CodeEscape.Audio;
using CodeEscape.UI;

namespace CodeEscape.Game
{
    /// <summary>
    /// Door / path to another location. Press E to travel; LevelManager refuses while the location is
    /// locked or this level still has an unsolved required challenge.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class LevelExit : MonoBehaviour
    {
        [SerializeField] string targetScene;
        [SerializeField] GameObject interactionPrompt;
        [SerializeField] TMP_Text promptText;
        [SerializeField, Tooltip("Optional: shown while the destination is unlocked (e.g. a green light).")]
        GameObject openIndicator;

        bool playerInRange;

        public string TargetScene => targetScene;
        public bool PlayerInRange => playerInRange;

        void Reset()
        {
            GetComponent<Collider2D>().isTrigger = true;
        }

        void Start()
        {
            Refresh();
        }

        void Update()
        {
            Refresh();
            if (!playerInRange)
                return;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.eKey.wasPressedThisFrame && CanInteractNow())
                TryUse();
        }

        /// <summary>Travel (the E key). Shows a warning and returns false when travel isn't allowed.</summary>
        public bool TryUse()
        {
            LevelManager levels = LevelManager.Instance;
            if (levels == null)
                return false;
            if (levels.TryTravel(targetScene, out string reason))
            {
                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlaySfx(SfxType.Door);
                return true;
            }
            if (!string.IsNullOrEmpty(reason) && NotificationUI.Instance != null)
                NotificationUI.Instance.ShowWarning("LOCKED", reason);
            return false;
        }

        bool IsOpen()
        {
            LevelManager levels = LevelManager.Instance;
            return levels != null && levels.IsUnlocked(targetScene) && levels.AreRequiredChallengesComplete(levels.CurrentLevel, out _);
        }

        static bool CanInteractNow()
        {
            return GameManager.Instance == null || GameManager.Instance.CurrentState == GameState.Playing;
        }

        void Refresh()
        {
            bool open = IsOpen();
            if (openIndicator != null && openIndicator.activeSelf != open)
                openIndicator.SetActive(open);
            if (interactionPrompt == null)
                return;
            bool show = playerInRange && CanInteractNow();
            if (interactionPrompt.activeSelf != show)
                interactionPrompt.SetActive(show);
            if (show && promptText != null)
            {
                LevelInfo info = LevelManager.Instance != null ? LevelManager.Instance.GetInfo(targetScene) : null;
                string name = info != null ? info.displayName : targetScene;
                promptText.text = open ? $"Press  E  to go to {name}" : "Locked - keep investigating";
            }
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
                playerInRange = true;
        }

        void OnTriggerExit2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
                playerInRange = false;
        }
    }
}
