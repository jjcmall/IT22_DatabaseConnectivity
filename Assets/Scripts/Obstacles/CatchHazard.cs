using System.Collections.Generic;
using UnityEngine;
using CodeEscape.Clues;
using CodeEscape.Game;
using CodeEscape.Programming;
using CodeEscape.Story;
using CodeEscape.UI;

namespace CodeEscape.Obstacles
{
    /// <summary>What kind of hazard it is (for the challenge title and level design).</summary>
    public enum HazardType
    {
        PatrolBot,
        LaserGate,
        Tripwire,
        SecurityCamera,
        SlidingDoor,
        SwingingHazard,
        AlarmFloor,
        Drone
    }

    /// <summary>Optional difficulty filter for a hazard's questions.</summary>
    public enum HazardDifficulty
    {
        Any = -1,
        Easy = 0,
        Medium = 1,
        Hard = 2
    }

    /// <summary>
    /// A hazard Alex must dodge. Walking past it safely does nothing; touching one of its catch colliders
    /// (or entering its detection zone) while it is dangerous catches Alex at once: the game freezes and
    /// a programming challenge opens that can't be closed until it is answered correctly. A correct
    /// answer releases Alex at the release point, with a few seconds of grace so he isn't caught again
    /// straight away. Uses the shared ProgrammingChallengeManager and the selected language's questions.
    /// </summary>
    public class CatchHazard : MonoBehaviour, IChallengeSource
    {
        /// <summary>Question IDs starting with this are written for hazards (used when none are listed).</summary>
        public const string HazardQuestionPrefix = "HZ_";

        [Header("Identity")]
        [SerializeField] string hazardId = "hazard";
        [SerializeField] HazardType hazardType = HazardType.LaserGate;
        [SerializeField] string displayName = "Security Laser";

        [Header("Catch")]
        [SerializeField, Tooltip("Touching any of these catches Alex. Empty = every trigger collider on this object and its children.")]
        Collider2D[] catchColliders = new Collider2D[0];
        [SerializeField, Tooltip("Optional: when and whether the hazard is dangerous (laser off, door open...).")]
        HazardMotion motion;

        [Header("Challenge")]
        [SerializeField, Tooltip("Questions for this hazard. The ones in the selected language are used in turn.")]
        List<QuestionData> questions = new List<QuestionData>();
        [SerializeField, Tooltip("Without listed questions: the topic to pick from (empty = any).")]
        string topic;
        [SerializeField] HazardDifficulty difficulty = HazardDifficulty.Any;

        [Header("Escape")]
        [SerializeField, Tooltip("Where Alex is put after escaping (a safe spot before the hazard).")]
        Transform releasePoint;
        [SerializeField, Min(0f), Tooltip("Seconds after escaping during which no hazard can catch Alex.")]
        float graceSeconds = 2f;

        [Header("Rewards (optional, first escape only)")]
        [SerializeField] ClueData clueReward;
        [SerializeField, Tooltip("Story event completed the first time Alex escapes, e.g. to update the objective.")]
        string completeStoryEvent;

        static readonly Dictionary<string, int> NextQuestion = new Dictionary<string, int>();
        static float graceUntil = -1f;

        Collider2D playerCollider;
        Rigidbody2D playerBody;
        int timesCaught;
        int timesEscaped;

        public string HazardId => hazardId;
        public HazardType Type => hazardType;
        public HazardMotion Motion => motion;
        public int TimesCaught => timesCaught;
        public int TimesEscaped => timesEscaped;
        public bool IsDangerous => motion == null || motion.IsDangerous;
        /// <summary>True while this hazard holds Alex (its challenge is open).</summary>
        public bool IsHoldingPlayer
        {
            get
            {
                ProgrammingChallengeManager manager = ProgrammingChallengeManager.Instance;
                return manager != null && manager.IsChallengeOpen && ReferenceEquals(manager.ActiveSource, this);
            }
        }
        public static bool InGracePeriod => Time.unscaledTime < graceUntil;

        // ---------- IChallengeSource ----------

        public string ObstacleId => hazardId;
        public string DisplayName => displayName;
        public bool IsMandatory => true;
        public bool CountsAsObstacle => false;
        public bool HasClueReward => clueReward != null && ClueManager.Instance != null && !ClueManager.Instance.HasClue(clueReward.Id);
        public string ClueRewardId => clueReward != null ? clueReward.Id : null;
        public string ClueRewardText => HasClueReward ? $"{clueReward.Title} - {clueReward.Description}" : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            NextQuestion.Clear();
            graceUntil = -1f;
        }

        void Awake()
        {
            if (catchColliders == null || catchColliders.Length == 0)
            {
                var found = new List<Collider2D>();
                foreach (Collider2D c in GetComponentsInChildren<Collider2D>(true))
                {
                    if (c.isTrigger)
                        found.Add(c);
                }
                catchColliders = found.ToArray();
            }
        }

        void FixedUpdate()
        {
            if (!CanCatchNow())
                return;
            foreach (Collider2D c in catchColliders)
            {
                if (c != null && c.enabled && c.gameObject.activeInHierarchy && c.Distance(playerCollider).isOverlapped)
                {
                    Catch();
                    return;
                }
            }
        }

        bool CanCatchNow()
        {
            if (!IsDangerous || InGracePeriod)
                return false;
            if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Playing)
                return false; // paused, cutscene, another challenge, menus
            if (SceneTransitionManager.Instance != null && SceneTransitionManager.Instance.IsTransitioning)
                return false;
            ProgrammingChallengeManager manager = ProgrammingChallengeManager.Instance;
            if (manager == null || manager.IsChallengeOpen)
                return false; // one catch at a time
            if (playerCollider == null && !FindPlayer())
                return false;
            return playerCollider.enabled && playerCollider.gameObject.activeInHierarchy;
        }

        bool FindPlayer()
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player == null)
                return false;
            playerBody = player.GetComponent<Rigidbody2D>();
            foreach (Collider2D c in player.GetComponents<Collider2D>())
            {
                if (!c.isTrigger)
                {
                    playerCollider = c;
                    break;
                }
            }
            return playerCollider != null;
        }

        /// <summary>Catches Alex: stops him and opens the challenge he must solve to escape.</summary>
        public bool Catch()
        {
            ProgrammingChallengeManager manager = ProgrammingChallengeManager.Instance;
            if (manager == null || manager.IsChallengeOpen)
                return false;
            if (playerBody == null)
                FindPlayer();
            if (playerBody != null)
                playerBody.linearVelocity = Vector2.zero;

            if (!manager.TryOpenChallenge(this))
            {
                // No question could be shown: don't keep retrying every physics step.
                graceUntil = Time.unscaledTime + 1f;
                Debug.LogWarning($"[Hazard] '{displayName}' caught Alex but no challenge could open.");
                return false;
            }
            timesCaught++;
            Debug.Log($"[Hazard] Caught by '{displayName}' ({hazardId}, {hazardType})");
            return true;
        }

        public QuestionData ResolveQuestion(ProgrammingLanguage language, QuestionDatabase database, ICollection<string> completedQuestionIds)
        {
            var candidates = new List<QuestionData>();
            foreach (QuestionData q in questions)
            {
                if (q != null && q.Language == language)
                    candidates.Add(q);
            }
            if (candidates.Count == 0 && database != null)
            {
                // Hazard questions first, then the rest of the language's questions.
                foreach (bool hazardOnly in new[] { true, false })
                {
                    foreach (QuestionData q in database.GetQuestions(language))
                    {
                        if (hazardOnly != q.Id.StartsWith(HazardQuestionPrefix))
                            continue;
                        if (!string.IsNullOrEmpty(topic) && !string.Equals(q.Topic, topic, System.StringComparison.OrdinalIgnoreCase))
                            continue;
                        if (difficulty != HazardDifficulty.Any && (int)q.Difficulty != (int)difficulty)
                            continue;
                        candidates.Add(q);
                    }
                    if (candidates.Count > 0)
                        break;
                }
                if (candidates.Count == 0)
                    candidates.AddRange(database.GetQuestions(language));
            }
            if (candidates.Count == 0)
                return null;

            // Each catch asks the next question in turn, so getting caught twice isn't the same question.
            // Every hazard starts at a different place in the list, so a level's hazards don't all open
            // with the same question.
            string key = hazardId + "/" + language;
            if (!NextQuestion.TryGetValue(key, out int index))
                index = StableHash(hazardId);
            NextQuestion[key] = index + 1;
            return candidates[index % candidates.Count];
        }

        static int StableHash(string text)
        {
            unchecked
            {
                int hash = 23;
                foreach (char c in text ?? string.Empty)
                    hash = hash * 31 + c;
                return hash & 0x7fffffff;
            }
        }

        /// <summary>The caught challenge was answered correctly: release Alex.</summary>
        public void OnChallengeSolved()
        {
            timesEscaped++;
            graceUntil = Time.unscaledTime + graceSeconds;
            if (playerBody == null)
                FindPlayer();
            if (playerBody != null)
            {
                if (releasePoint != null)
                {
                    playerBody.position = releasePoint.position;
                    playerBody.transform.position = new Vector3(releasePoint.position.x, releasePoint.position.y, playerBody.transform.position.z);
                }
                playerBody.linearVelocity = Vector2.zero;
            }

            if (timesEscaped == 1)
            {
                if (clueReward != null && ClueManager.Instance != null)
                    ClueManager.Instance.TryCollect(clueReward);
                if (!string.IsNullOrEmpty(completeStoryEvent) && StoryManager.Instance != null)
                    StoryManager.Instance.CompleteEvent(completeStoryEvent);
            }
            if (NotificationUI.Instance != null)
                NotificationUI.Instance.Show("ESCAPED!", $"Watch the {displayName.ToLowerInvariant()} - time your move.", new Color(0.29f, 0.87f, 0.5f), 2.5f);
            Debug.Log($"[Hazard] Escaped from '{displayName}' ({hazardId})");
        }

        void OnDrawGizmos()
        {
            if (releasePoint == null)
                return;
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(releasePoint.position, 0.3f);
            Gizmos.DrawLine(transform.position, releasePoint.position);
        }
    }
}
