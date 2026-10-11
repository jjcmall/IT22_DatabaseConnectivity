using System;
using System.Collections.Generic;
using UnityEngine;

namespace CodeEscape.Cloud
{
    /// <summary>
    /// The progression record stored in Unity Cloud Save under the authenticated player (key
    /// <see cref="CloudProgressService.KeyProgression"/>). Field names match the project's data design.
    /// Serialised with JsonUtility into one Cloud Save string value.
    /// </summary>
    [Serializable]
    public class PlayerProgressData
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        /// <summary>Authenticated Unity Player ID this record belongs to.</summary>
        public string player_id = string.Empty;
        /// <summary>Level ID (LevelInfo.levelId) of the level currently reached.</summary>
        public string current_level = string.Empty;
        /// <summary>Level ID of the furthest level unlocked in any language.</summary>
        public string highest_unlocked_level = string.Empty;
        /// <summary>Unique IDs of cleared levels across all languages (never duplicated).</summary>
        public List<string> completed_levels = new List<string>();
        /// <summary>Accumulated score: +100 per distinct solved challenge, -25 per distinct hinted question.</summary>
        public int total_score;
        public List<string> achievements = new List<string>();
        /// <summary>UTC time of the latest progression change, ISO 8601 ("o").</summary>
        public string last_played = string.Empty;

        // Bookkeeping that makes the score idempotent: each entry is counted once, however often an event fires.
        /// <summary>"language:obstacleId" of challenges already counted in total_score.</summary>
        public List<string> scored_challenges = new List<string>();
        /// <summary>"language:questionId" of hints whose penalty is already counted in total_score.</summary>
        public List<string> penalised_hints = new List<string>();

        public PlayerProgressData Clone()
        {
            return JsonUtility.FromJson<PlayerProgressData>(JsonUtility.ToJson(this));
        }

        /// <summary>Repairs null lists (a hand-edited or older record) so the rest of the code can rely on them.</summary>
        public void Sanitize()
        {
            player_id ??= string.Empty;
            current_level ??= string.Empty;
            highest_unlocked_level ??= string.Empty;
            last_played ??= string.Empty;
            completed_levels ??= new List<string>();
            achievements ??= new List<string>();
            scored_challenges ??= new List<string>();
            penalised_hints ??= new List<string>();
            total_score = Mathf.Max(0, total_score);
        }
    }

    /// <summary>The save of one language's level map progress (LevelProgressData as JSON).</summary>
    [Serializable]
    public class LanguageLevelProgress
    {
        public int language;
        public string json = string.Empty;
    }

    /// <summary>
    /// What the game needs to resume exactly where it was: the existing local save files, as JSON, copied to
    /// Cloud Save (key <see cref="CloudProgressService.KeySnapshot"/>). Lets the existing level map, checkpoints
    /// and Continue button work unchanged after a restore.
    /// </summary>
    [Serializable]
    public class ProgressSnapshot
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public int language;
        /// <summary>ProgressData JSON of the playthrough in progress; empty when none is active.</summary>
        public string progress_json = string.Empty;
        public List<LanguageLevelProgress> level_progress = new List<LanguageLevelProgress>();
    }
}
