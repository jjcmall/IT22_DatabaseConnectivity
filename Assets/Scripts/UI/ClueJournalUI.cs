using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CodeEscape.Clues;

namespace CodeEscape.UI
{
    /// <summary>
    /// Lists the collected clues (title, location, description, story significance). Used in the
    /// pause menu's Clue Journal and the main menu's Investigation Logs.
    /// </summary>
    public class ClueJournalUI : MonoBehaviour
    {
        [SerializeField] RectTransform content;
        [SerializeField, Tooltip("Inactive entry with children Title, Meta, Description, Significance.")]
        GameObject entryTemplate;
        [SerializeField] GameObject emptyState;
        [SerializeField] TMP_Text counterText;
        [SerializeField] ScrollRect scrollRect;

        readonly List<GameObject> entries = new List<GameObject>();

        public int EntryCount => entries.Count;

        void OnEnable()
        {
            ClueManager.ClueCollected += HandleClue;
            Refresh();
        }

        void OnDisable()
        {
            ClueManager.ClueCollected -= HandleClue;
        }

        public void Refresh()
        {
            foreach (GameObject entry in entries)
                Destroy(entry);
            entries.Clear();

            ClueManager clues = ClueManager.Instance;
            List<ClueData> collected = clues != null ? clues.GetCollectedClues() : new List<ClueData>();
            if (emptyState != null)
                emptyState.SetActive(collected.Count == 0);
            if (counterText != null)
                counterText.text = clues != null ? $"CLUES FOUND: {clues.CollectedCount} / {clues.TotalClues}" : string.Empty;

            foreach (ClueData clue in collected)
            {
                GameObject entry = Instantiate(entryTemplate, content);
                entry.name = "Entry_" + clue.Id;
                SetText(entry, "Title", clue.Title);
                SetText(entry, "Meta", $"#{clue.Order:00}  //  {clue.Location}");
                SetText(entry, "Description", clue.Description);
                SetText(entry, "Significance", "WHY IT MATTERS: " + clue.StorySignificance);
                entry.SetActive(true);
                entries.Add(entry);
            }

            if (scrollRect != null)
                scrollRect.verticalNormalizedPosition = 1f;
        }

        static void SetText(GameObject entry, string child, string value)
        {
            Transform t = entry.transform.Find(child);
            if (t != null && t.TryGetComponent(out TMP_Text text))
                text.text = value;
        }

        void HandleClue(ClueData clue)
        {
            if (isActiveAndEnabled)
                Refresh();
        }
    }
}
