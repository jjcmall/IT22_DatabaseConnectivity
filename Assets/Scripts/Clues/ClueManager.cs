using System;
using System.Collections.Generic;
using UnityEngine;
using CodeEscape.Programming;
using CodeEscape.SaveSystem;

namespace CodeEscape.Clues
{
    /// <summary>
    /// Keeps the clues collected in this playthrough. A clue can only be collected once.
    /// Lives on the persistent GameSystems object; loads its list from the save on start-up.
    /// </summary>
    public class ClueManager : MonoBehaviour
    {
        public static ClueManager Instance { get; private set; }

        /// <summary>Raised the first time a clue is collected.</summary>
        public static event Action<ClueData> ClueCollected;

        [SerializeField, Tooltip("Every clue in the game (used to restore saved clues by ID).")]
        List<ClueData> allClues = new List<ClueData>();

        readonly HashSet<string> collected = new HashSet<string>();

        public int CollectedCount => collected.Count;
        public int TotalClues => allClues.Count;
        public IEnumerable<string> CollectedIds => collected;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
            ClueCollected = null;
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

        /// <summary>Restores the collected clues from a saved state (none when there is no save).</summary>
        public void ApplyProgress(ProgressData progress)
        {
            collected.Clear();
            if (progress == null)
                return;
            foreach (string id in progress.collectedClues)
            {
                if (GetClue(id) != null)
                    collected.Add(id);
            }
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

        public bool HasClue(string id)
        {
            return !string.IsNullOrEmpty(id) && collected.Contains(id);
        }

        public ClueData GetClue(string id)
        {
            foreach (ClueData clue in allClues)
            {
                if (clue != null && clue.Id == id)
                    return clue;
            }
            return null;
        }

        /// <summary>Collected clues in story order.</summary>
        public List<ClueData> GetCollectedClues()
        {
            var list = new List<ClueData>();
            foreach (ClueData clue in allClues)
            {
                if (clue != null && collected.Contains(clue.Id))
                    list.Add(clue);
            }
            list.Sort((a, b) => a.Order.CompareTo(b.Order));
            return list;
        }

        /// <summary>Adds the clue to the journal. Returns false if it was already collected.</summary>
        public bool TryCollect(ClueData clue)
        {
            if (clue == null || string.IsNullOrEmpty(clue.Id))
                return false;
            if (!collected.Add(clue.Id))
            {
                Debug.Log($"[ClueManager] '{clue.Title}' is already in the journal (duplicate ignored).");
                return false;
            }

            Debug.Log($"[ClueManager] Collected clue {clue.Id}: {clue.Title} ({collected.Count}/{allClues.Count})");
            ClueCollected?.Invoke(clue);
            return true;
        }

        public void ResetProgress()
        {
            collected.Clear();
        }

        void HandleNewGame(ProgrammingLanguage language)
        {
            // Choosing a language starts a new playthrough.
            ResetProgress();
        }
    }
}
