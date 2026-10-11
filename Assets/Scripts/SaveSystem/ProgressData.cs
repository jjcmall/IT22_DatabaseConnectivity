using System;
using System.Collections.Generic;

namespace CodeEscape.SaveSystem
{
    /// <summary>Everything needed to continue a playthrough. Stored as JSON by SaveManager.</summary>
    [Serializable]
    public class ProgressData
    {
        /// <summary>First version that records <see cref="activeLevel"/> (saves from before the level map are version 1).</summary>
        public const int LevelMapVersion = 2;

        public int version = LevelMapVersion;
        public int language;
        public int storyStage;
        public string currentLevel;
        public string currentObjective;
        /// <summary>Level ID (not scene name) of the level started from the level map; empty when none is being played.</summary>
        public string activeLevel = string.Empty;
        public List<string> completedEvents = new List<string>();
        public List<string> unlockedLevels = new List<string>();
        public List<string> completedLevels = new List<string>();
        public List<string> completedChallenges = new List<string>();
        public List<string> completedQuestions = new List<string>();
        public List<string> hintedQuestions = new List<string>();
        public List<string> collectedClues = new List<string>();
        public int score;
        public int hintsUsed;
        public float timePlayed;
    }
}
