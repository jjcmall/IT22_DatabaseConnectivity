using System;
using UnityEngine;

namespace CodeEscape.Story
{
    /// <summary>Holds the current objective shown on the HUD. StoryManager decides what it is.</summary>
    public class ObjectiveManager : MonoBehaviour
    {
        public static ObjectiveManager Instance { get; private set; }

        /// <summary>Raised with the new objective text.</summary>
        public static event Action<string> ObjectiveChanged;

        [SerializeField, Tooltip("Shown for debugging.")]
        string currentObjective = string.Empty;

        public string CurrentObjective => currentObjective;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
            ObjectiveChanged = null;
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

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void SetObjective(string objective)
        {
            objective = objective ?? string.Empty;
            if (objective == currentObjective)
                return;

            currentObjective = objective;
            Debug.Log($"[ObjectiveManager] Objective: {objective}");
            ObjectiveChanged?.Invoke(objective);
        }
    }
}
