using UnityEngine;

namespace CodeEscape.Clues
{
    /// <summary>One clue Alex can find. Collected clues appear in the Clue Journal and are saved.</summary>
    [CreateAssetMenu(fileName = "Clue", menuName = "Code Escape/Clue Data")]
    public class ClueData : ScriptableObject
    {
        [SerializeField, Tooltip("Unique ID, e.g. C01_FOLLOW_THE_CODE.")]
        string id;
        [SerializeField] string title;
        [SerializeField, TextArea(2, 6)] string description;
        [SerializeField, Tooltip("Where the clue is found, e.g. Home.")]
        string location;
        [SerializeField, Tooltip("Position in the story (journal sort order).")]
        int order;
        [SerializeField, Tooltip("Obstacle ID of the challenge that rewards this clue (empty if found by inspecting).")]
        string relatedChallenge;
        [SerializeField, TextArea(2, 4), Tooltip("Why this clue matters for the investigation.")]
        string storySignificance;

        public string Id => id;
        public string Title => title;
        public string Description => description;
        public string Location => location;
        public int Order => order;
        public string RelatedChallenge => relatedChallenge;
        public string StorySignificance => storySignificance;
    }
}
