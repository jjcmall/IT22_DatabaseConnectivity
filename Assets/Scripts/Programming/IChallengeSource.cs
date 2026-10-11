using System.Collections.Generic;

namespace CodeEscape.Programming
{
    /// <summary>
    /// Something that opens a programming challenge in ProgrammingChallengeManager: a locked
    /// obstacle (opened with E) or a hazard that caught Alex (opened at once, and must be solved).
    /// </summary>
    public interface IChallengeSource
    {
        /// <summary>Unique ID (obstacle ID or hazard ID).</summary>
        string ObstacleId { get; }
        string DisplayName { get; }
        /// <summary>A caught challenge can't be closed until it is answered correctly.</summary>
        bool IsMandatory { get; }
        /// <summary>True for a locked obstacle that counts as a solved challenge (score, results, level exits).</summary>
        bool CountsAsObstacle { get; }
        bool HasClueReward { get; }
        string ClueRewardId { get; }
        string ClueRewardText { get; }

        /// <summary>The question to ask in the selected language.</summary>
        QuestionData ResolveQuestion(ProgrammingLanguage language, QuestionDatabase database, ICollection<string> completedQuestionIds);

        /// <summary>Called when the challenge closes after a correct answer.</summary>
        void OnChallengeSolved();
    }
}
