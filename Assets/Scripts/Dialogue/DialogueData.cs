using System;
using System.Collections.Generic;
using UnityEngine;

namespace CodeEscape.Dialogue
{
    public enum PortraitSide
    {
        Left,
        Right
    }

    /// <summary>One line of dialogue. An empty speaker is narration (no name plate or portrait).</summary>
    [Serializable]
    public class DialogueLine
    {
        [Tooltip("Name on the name plate. Leave empty for narration.")]
        public string speaker;
        public Color nameColor = new Color(0.13f, 0.83f, 0.93f);
        public Sprite portrait;
        public PortraitSide side = PortraitSide.Left;
        [TextArea(2, 5), Tooltip("\"{LANGUAGE}\" is replaced with the selected programming language.")]
        public string text;
        [Tooltip("Optional: full-screen background shown from this line on.")]
        public Sprite background;
    }

    /// <summary>A block of dialogue lines, played by the DialogueManager (usually from a cutscene).</summary>
    [CreateAssetMenu(fileName = "Dialogue", menuName = "Code Escape/Dialogue Data")]
    public class DialogueData : ScriptableObject
    {
        [SerializeField] string id;
        [SerializeField] List<DialogueLine> lines = new List<DialogueLine>();

        public string Id => id;
        public IReadOnlyList<DialogueLine> Lines => lines;
    }
}
