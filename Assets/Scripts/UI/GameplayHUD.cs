using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using CodeEscape.Clues;
using CodeEscape.Cutscenes;
using CodeEscape.Game;
using CodeEscape.Story;

namespace CodeEscape.UI
{
    /// <summary>Shows the current location, objective and clue count on the gameplay HUD.</summary>
    public class GameplayHUD : MonoBehaviour
    {
        [SerializeField] TMP_Text locationText;
        [SerializeField] TMP_Text objectiveText;
        [SerializeField] TMP_Text cluesText;
        [SerializeField, Tooltip("Faded out while a cutscene plays.")]
        CanvasGroup group;
        [SerializeField, Min(0.01f)] float fadeSpeed = 4f;

        void Update()
        {
            if (group == null)
                return;
            float target = CutsceneManager.IsActive ? 0f : 1f;
            group.alpha = Mathf.MoveTowards(group.alpha, target, Time.unscaledDeltaTime * fadeSpeed);
        }

        void OnEnable()
        {
            ObjectiveManager.ObjectiveChanged += HandleObjective;
            ClueManager.ClueCollected += HandleClue;
            Refresh();
        }

        void OnDisable()
        {
            ObjectiveManager.ObjectiveChanged -= HandleObjective;
            ClueManager.ClueCollected -= HandleClue;
        }

        void Start()
        {
            Refresh();
        }

        public void Refresh()
        {
            if (locationText != null && LevelManager.Instance != null)
            {
                LevelInfo info = LevelManager.Instance.GetInfo(SceneManager.GetActiveScene().name);
                if (info != null)
                    locationText.text = info.displayName;
            }
            if (objectiveText != null && ObjectiveManager.Instance != null)
                objectiveText.text = ObjectiveManager.Instance.CurrentObjective;
            if (cluesText != null && ClueManager.Instance != null)
                cluesText.text = $"Clues: {ClueManager.Instance.CollectedCount}/{ClueManager.Instance.TotalClues}";
        }

        void HandleObjective(string objective) => Refresh();
        void HandleClue(ClueData clue) => Refresh();
    }
}
