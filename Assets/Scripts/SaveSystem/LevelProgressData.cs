using System;
using System.Collections.Generic;

namespace CodeEscape.SaveSystem
{
    /// <summary>The saved playthrough state a level starts from (taken when the level before it was cleared).</summary>
    [Serializable]
    public class LevelCheckpoint
    {
        public string levelId;
        public ProgressData state;
    }

    /// <summary>
    /// Level map progress for one programming language: the IDs of its cleared levels and the state each
    /// unlocked level starts from. Stored as JSON by SaveManager under a key per language. Uses stable
    /// level IDs (LevelInfo.levelId), never display names or scene order.
    /// </summary>
    [Serializable]
    public class LevelProgressData
    {
        public int version = 1;
        public string language;
        public List<string> completedLevels = new List<string>();
        public string lastPlayedLevel;
        public List<LevelCheckpoint> checkpoints = new List<LevelCheckpoint>();

        public bool IsCompleted(string levelId)
        {
            return !string.IsNullOrEmpty(levelId) && completedLevels.Contains(levelId);
        }

        /// <summary>Records a cleared level. Returns false if it was already recorded (no duplicates).</summary>
        public bool MarkCompleted(string levelId)
        {
            if (string.IsNullOrEmpty(levelId) || completedLevels.Contains(levelId))
                return false;
            completedLevels.Add(levelId);
            return true;
        }

        public LevelCheckpoint GetCheckpoint(string levelId)
        {
            foreach (LevelCheckpoint checkpoint in checkpoints)
            {
                if (checkpoint != null && checkpoint.levelId == levelId)
                    return checkpoint;
            }
            return null;
        }

        /// <summary>Stores (or replaces) the state the level starts from.</summary>
        public void SetCheckpoint(string levelId, ProgressData state)
        {
            if (string.IsNullOrEmpty(levelId) || state == null)
                return;
            LevelCheckpoint checkpoint = GetCheckpoint(levelId);
            if (checkpoint == null)
            {
                checkpoint = new LevelCheckpoint { levelId = levelId };
                checkpoints.Add(checkpoint);
            }
            checkpoint.state = state;
        }

        /// <summary>Drops unknown level IDs, duplicates and empty entries (e.g. after editing the level list).</summary>
        public void Sanitize(Predicate<string> isKnownLevel)
        {
            if (completedLevels == null)
                completedLevels = new List<string>();
            if (checkpoints == null)
                checkpoints = new List<LevelCheckpoint>();

            var seen = new HashSet<string>();
            completedLevels.RemoveAll(id => string.IsNullOrEmpty(id) || !isKnownLevel(id) || !seen.Add(id));
            seen.Clear();
            checkpoints.RemoveAll(c => c == null || c.state == null || !isKnownLevel(c.levelId) || !seen.Add(c.levelId));
        }
    }
}
